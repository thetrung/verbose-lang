open Ast

exception Error of string

(* Context to generate unique local registers and block labels *)
type context = {
  buf: Buffer.t;
  mutable reg_counter: int;
  mutable label_counter: int;
  (* Maps variable names to their exact virtual register strings (e.g., "%1") *)
  variables: (string, string) Hashtbl.t;
  mutable globals: string list;
}

let create_context () = {
  buf = Buffer.create 2048;
  reg_counter = 1;
  label_counter = 1;
  variables = Hashtbl.create 50;
  globals = [];
}
(* ✅ Added metadata environments *)
let struct_fields = Hashtbl.create 10
let struct_field_types = Hashtbl.create 10
let var_types = Hashtbl.create 50

let emit ctx str = Buffer.add_string ctx.buf (str ^ "\n")

let next_reg ctx =
  let r = "%" ^ string_of_int ctx.reg_counter in
  ctx.reg_counter <- ctx.reg_counter + 1;
  r

let next_label ctx prefix =
  let l = prefix ^ "_" ^ string_of_int ctx.label_counter in
  ctx.label_counter <- ctx.label_counter + 1;
  l

(* Map custom data types to standard LLVM IR strings *)
let string_of_dt = function
  | Int -> "i32"
  | Byte -> "i8"
  | Nothing -> "void"
  | Pointer -> "ptr"

(* Returns (result_register_name, type_string) *)
let rec codegen_expr ctx = function
  | IntLit i -> (string_of_int i, "i32")
  
  | StringLit s ->
      let global_reg = "@.str_" ^ string_of_int (ctx.reg_counter) in
      ctx.reg_counter <- ctx.reg_counter + 1;
      
      let len = String.length s + 1 in
      (* Safely store the global definition for the final output wrapper *)
      let decl = Printf.sprintf "%s = private unnamed_addr constant [%d x i8] c\"%s\\00\", align 1" 
                   global_reg len s in
      ctx.globals <- decl :: ctx.globals;
      (global_reg, "ptr")

   | Id name ->
    if Hashtbl.mem ctx.variables name then
      let alloca_reg = Hashtbl.find ctx.variables name in
      let res_reg = next_reg ctx in
      
      let t_str = 
        if Hashtbl.mem var_types name then Printf.sprintf "%%struct.%s" (Hashtbl.find var_types name)
        else if name = "buffer" || name = "fileHandle" then "ptr" 
        else "i32" 
      in
      
      if Hashtbl.mem var_types name then
        (alloca_reg, "ptr")
      else begin
        emit ctx (Printf.sprintf "  %s = load %s, ptr %s, align 4" res_reg t_str alloca_reg);
        (res_reg, t_str)
      end
    else
      raise (Error ("Undefined variable reference: " ^ name))

  | FieldAccess (base_expr, field_name) ->
      let base_ptr, _ = codegen_expr ctx base_expr in
      let struct_name = match base_expr with
        | Id name -> Hashtbl.find var_types name
        | _ -> raise (Error "Property lookups require an explicit instance identifier")
      in
      let fields = Hashtbl.find struct_fields struct_name in
      let field_index = List.assoc field_name fields in
      let field_types = Hashtbl.find struct_field_types struct_name in
      let actual_dt = List.assoc field_name field_types in
      let dt_str = string_of_dt actual_dt in
      
      let field_ptr = next_reg ctx in
      let struct_type_str = Printf.sprintf "%%struct.%s" struct_name in
      emit ctx (Printf.sprintf "  %s = getelementptr inbounds %s, ptr %s, i32 0, i32 %d" 
                 field_ptr struct_type_str base_ptr field_index);
      
      let res_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = load %s, ptr %s, align 4" res_reg dt_str field_ptr);
      (res_reg, dt_str)

  | UnaryOp ("Not", e) ->
      let v, t = codegen_expr ctx e in
      let res_reg = next_reg ctx in
      (* Bitwise XOR with -1 flips all bits, achieving a logical/bitwise 'Not' *)
      emit ctx (Printf.sprintf "  %s = xor %s %s, -1" res_reg t v);
      (res_reg, t)
  | UnaryOp (_, _) -> raise (Error "Unsupported unary operation")

  | BinOp (e1, op, e2) ->
      let v1, t1 = codegen_expr ctx e1 in
      let v2, _  = codegen_expr ctx e2 in
      let res_reg = next_reg ctx in
      let op_str = match op with
        | Add -> "add nsw i32"
        | Sub -> "sub nsw i32"
        | Mul -> "mul nsw i32"
        | Div -> "sdiv i32"
        | Mod -> "srem i32"
        | And -> "and i32"
        | Or  -> "or i32"
        | Xor -> "xor i32"
        | Shl -> "shl i32"
        | Shr -> "lshr i32"
        (* Comparison operations emit an i1 (boolean) condition *)
        | Equal        -> "icmp eq i32"
        | Greater      -> "icmp sgt i32"
        | Less         -> "icmp slt i32"
        | GreaterEqual -> "icmp sge i32"
        | LessEqual    -> "icmp sle i32"
        | NotEqual     -> "icmp ne i32"
      in
      emit ctx (Printf.sprintf "  %s = %s %s, %s" res_reg op_str v1 v2);
      (* Comparisons result in an i1 type, math results in the input type *)
      let ret_type = match op with

        | Equal | Greater | Less | GreaterEqual | LessEqual | NotEqual -> "i1"
        | _ -> t1
      in
      (res_reg, ret_type)

  | Call ("printf", args) ->
      let evaluated_args = List.map (codegen_expr ctx) args in
      (* Format parameters array *)
      let arg_strs = List.map (fun (v, t) -> t ^ " " ^ v) evaluated_args in
      let args_joined = String.concat ", " arg_strs in
      let res_reg = next_reg ctx in
      
      (* FIX: Emit the correct variadic call format containing parameters *)
      emit ctx (Printf.sprintf "  %s = call i32 (ptr, ...) @printf(%s)" res_reg args_joined);
      (res_reg, "i32")
  | Call (name, args) ->
      if Hashtbl.mem struct_fields name then begin
        let struct_type_str = Printf.sprintf "%%struct.%s" name in
        let alloca_reg = next_reg ctx in
        emit ctx (Printf.sprintf "  %s = alloca %s, align 8" alloca_reg struct_type_str);
        
        let fields = Hashtbl.find struct_fields name in
        let field_types = Hashtbl.find struct_field_types name in
        
        List.iteri (fun idx arg_expr ->
          let (field_name, _) = List.find (fun (_, i) -> i = idx) fields in
          let actual_dt = List.assoc field_name field_types in
          let dt_str = string_of_dt actual_dt in
          
          let v, _ = codegen_expr ctx arg_expr in
          let field_ptr = next_reg ctx in
          emit ctx (Printf.sprintf "  %s = getelementptr inbounds %s, ptr %s, i32 0, i32 %d" 
                     field_ptr struct_type_str alloca_reg idx);
          emit ctx (Printf.sprintf "  store %s %s, ptr %s, align 4" dt_str v field_ptr)
        ) args;
        (alloca_reg, "ptr")
      end else begin
        let evaluated_args = List.map (codegen_expr ctx) args in
        let arg_strs = List.map (fun (v, t) -> 
          let checked_type = if name = "free" then "ptr" else t in
          checked_type ^ " " ^ v) evaluated_args in
        let args_joined = String.concat ", " arg_strs in
        let res_reg = next_reg ctx in
        let ret_type = match name with

          | "malloc" | "fopen" -> "ptr"
          | "free" -> "void"
          | _ -> "i32"
        in
        if ret_type = "void" then begin
          emit ctx (Printf.sprintf "  call void @%s(%s)" name args_joined);
          ("", "void")
        end else begin
          emit ctx (Printf.sprintf "  %s = call %s @%s(%s)" res_reg ret_type name args_joined);
          (res_reg, ret_type)
        end
      end



let rec codegen_stmt ctx = function
  | Dim (name, dt, exp) ->
      (match exp with
       | Call (struct_name, _) when Hashtbl.mem struct_fields struct_name ->
           Hashtbl.add var_types name struct_name
       | _ -> ());
       
      let v, _ = codegen_expr ctx exp in
      let alloca_reg = next_reg ctx in
      
      let typ_str = 
        if Hashtbl.mem var_types name then Printf.sprintf "%%struct.%s" (Hashtbl.find var_types name)
        else string_of_dt dt 
      in
      emit ctx (Printf.sprintf "  %s = alloca %s, align 8" alloca_reg typ_str);
      
      if Hashtbl.mem var_types name then
        emit ctx (Printf.sprintf "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %s, ptr align 8 %s, i64 16, i1 false)" alloca_reg v)
      else
        emit ctx (Printf.sprintf "  store %s %s, ptr %s, align 8" typ_str v alloca_reg);
        
      Hashtbl.add ctx.variables name alloca_reg

  | Assign (name, exp) ->
      if Hashtbl.mem ctx.variables name then
        let alloca_reg = Hashtbl.find ctx.variables name in
        let v, t = codegen_expr ctx exp in
        
        if Hashtbl.mem var_types name then
          emit ctx (Printf.sprintf "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %s, ptr align 8 %s, i64 16, i1 false)" alloca_reg v)
        else
          emit ctx (Printf.sprintf "  store %s %s, ptr %s, align 4" t v alloca_reg)
      else
        raise (Error ("Assignment target undefined: " ^ name))


  | FieldAssign (base_expr, field_name, value_expr) ->
      (* 1. Evaluate the value expression that will be written *)
      let v, dt_str = codegen_expr ctx value_expr in
      
      (* 2. Code-gen the base structure object to find its layout pointer address *)
      let base_ptr, _ = codegen_expr ctx base_expr in
      
      (* 3. Look up metadata layouts using the base instance variable tracker *)
      let struct_name = match base_expr with
        | Id name -> Hashtbl.find var_types name
        | _ -> raise (Error "Field adjustments require an explicit instance identifier")
      in
      let fields = Hashtbl.find struct_fields struct_name in
      let field_index = List.assoc field_name fields in
      
      (* 4. Compute field address offset boundaries using 'getelementptr' *)
      let field_ptr = next_reg ctx in
      let struct_type_str = Printf.sprintf "%%struct.%s" struct_name in
      emit ctx (Printf.sprintf "  %s = getelementptr inbounds %s, ptr %s, i32 0, i32 %d" 
                 field_ptr struct_type_str base_ptr field_index);
      
      (* 5. Store the value safely into that field memory address *)
      emit ctx (Printf.sprintf "  store %s %s, ptr %s, align 4" dt_str v field_ptr)
  | ExprStatement exp -> 
      ignore (codegen_expr ctx exp)

  | Return None -> 
      emit ctx "  ret void"
      
  | Return (Some exp) ->
      let v, t = codegen_expr ctx exp in
      emit ctx (Printf.sprintf "  ret %s %s" t v)

  | If (cond_exp, then_stmts, else_stmts) ->
      let cond_val, _ = codegen_expr ctx cond_exp in
      let label_then = next_label ctx "then" in
      let label_else = next_label ctx "else" in
      let label_merge = next_label ctx "ifcont" in
      
      (* Helper to check if a block definitely ends with a return *)
      let ends_with_return stmts = 
        List.exists (function Return _ -> true | _ -> false) stmts 
      in
      let then_terminates = ends_with_return then_stmts in
      let else_terminates = ends_with_return else_stmts in
      
      emit ctx (Printf.sprintf "  br i1 %s, label %%%s, label %%%s" cond_val label_then label_else);
      
      (* Generate 'Then' block *)
      emit ctx (Printf.sprintf "\n%s:" label_then);
      List.iter (codegen_stmt ctx) then_stmts;
      if not then_terminates then
        emit ctx (Printf.sprintf "  br label %%%s" label_merge);
      
      (* Generate 'Else' block *)
      emit ctx (Printf.sprintf "\n%s:" label_else);
      List.iter (codegen_stmt ctx) else_stmts;
      if not else_terminates then
        emit ctx (Printf.sprintf "  br label %%%s" label_merge);
      
      (* FIX: Only append the merge label if at least one branch leaks out *)
      if not (then_terminates && else_terminates) then
        emit ctx (Printf.sprintf "\n%s:" label_merge)

  | While (cond_exp, body_stmts) ->
      let label_cond = next_label ctx "while_cond" in
      let label_body = next_label ctx "while_body" in
      let label_end = next_label ctx "while_end" in
      
      emit ctx (Printf.sprintf "  br label %%%s" label_cond);
      emit ctx (Printf.sprintf "\n%s:" label_cond);
      
      let cond_val, _ = codegen_expr ctx cond_exp in
      emit ctx (Printf.sprintf "  br i1 %s, label %%%s, label %%%s" cond_val label_body label_end);
      
      emit ctx (Printf.sprintf "\n%s:" label_body);
      List.iter (codegen_stmt ctx) body_stmts;
      emit ctx (Printf.sprintf "  br label %%%s" label_cond);
      
      emit ctx (Printf.sprintf "\n%s:" label_end)

  | SelectCase (target_exp, cases, default_stmts_opt) ->
      let target_val, t = codegen_expr ctx target_exp in
      let label_exit = next_label ctx "select_exit" in
      
      (* Determine where to branch if ALL explicit checks fail *)
      let label_default = match default_stmts_opt with
        | Some _ -> next_label ctx "case_default"
        | None -> label_exit
      in

      let rec emit_cases = function
        | [] -> emit ctx (Printf.sprintf "  br label %%%s" label_default)
        | (expr_list, stmts) :: next_cases ->
            let label_match = next_label ctx "case_match" in
            let label_next = next_label ctx "case_next" in
            let rec build_or_chain = function
              | [] -> "i1 false"
              | [e] -> let v, _ = codegen_expr ctx e in let reg = next_reg ctx in emit ctx (Printf.sprintf "  %s = icmp eq %s %s, %s" reg t target_val v); reg
              | e :: es -> let v, _ = codegen_expr ctx e in let eq_reg = next_reg ctx in emit ctx (Printf.sprintf "  %s = icmp eq %s %s, %s" eq_reg t target_val v);
                           let rest_reg = build_or_chain es in let or_reg = next_reg ctx in emit ctx (Printf.sprintf "  %s = or i1 %s, %s" or_reg eq_reg rest_reg); or_reg
            in
            let cond_reg = build_or_chain expr_list in
            emit ctx (Printf.sprintf "  br i1 %s, label %%%s, label %%%s" cond_reg label_match label_next);
            emit ctx (Printf.sprintf "\n%s:" label_match); List.iter (codegen_stmt ctx) stmts;
            emit ctx (Printf.sprintf "  br label %%%s" label_exit);
            emit ctx (Printf.sprintf "\n%s:" label_next); emit_cases next_cases
      in 
      emit_cases cases;

      (* NEW: Generate the 'Case Else' body if it was declared in the script *)
      (match default_stmts_opt with
       | Some stmts ->
           emit ctx (Printf.sprintf "\n%s:" label_default);
           List.iter (codegen_stmt ctx) stmts;
           emit ctx (Printf.sprintf "  br label %%%s" label_exit)
       | None -> ());

      emit ctx (Printf.sprintf "\n%s:" label_exit)
 
  | For (var_name, start_exp, finish_exp, body_stmts) ->
      let label_cond = next_label ctx "for_cond" in
      let label_body = next_label ctx "for_body" in
      let label_inc  = next_label ctx "for_inc" in
      let label_end  = next_label ctx "for_end" in
      
      (* 1. INITIALIZE: Create loop variable if it doesn't exist, and assign start value *)
      let v_start, _ = codegen_expr ctx start_exp in
      let alloca_reg = 
        if Hashtbl.mem ctx.variables var_name then
          Hashtbl.find ctx.variables var_name
        else
          let r = next_reg ctx in
          emit ctx (Printf.sprintf "  %s = alloca i32, align 4" r);
          Hashtbl.add ctx.variables var_name r;
          r
      in
      emit ctx (Printf.sprintf "  store i32 %s, ptr %s, align 4" v_start alloca_reg);
      emit ctx (Printf.sprintf "  br label %%%s" label_cond);
      
      (* 2. CHECK CONDITION: Compare iterator <= finish *)
      emit ctx (Printf.sprintf "\n%s:" label_cond);
      let current_val = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = load i32, ptr %s, align 4" current_val alloca_reg);
      let v_finish, _ = codegen_expr ctx finish_exp in
      let cond_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = icmp sle i32 %s, %s" cond_reg current_val v_finish);
      emit ctx (Printf.sprintf "  br i1 %s, label %%%s, label %%%s" cond_reg label_body label_end);
      
      (* 3. EXECUTE BODY: Process everything inside the loop (including your hidden IF statement!) *)
      emit ctx (Printf.sprintf "\n%s:" label_body);
      List.iter (codegen_stmt ctx) body_stmts;
      emit ctx (Printf.sprintf "  br label %%%s" label_inc);
      
      (* 4. INCREMENT: Add 1 to loop variable, then branch back to condition check *)
      emit ctx (Printf.sprintf "\n%s:" label_inc);
      let reload_val = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = load i32, ptr %s, align 4" reload_val alloca_reg);
      let inc_val = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = add nsw i32 %s, 1" inc_val reload_val);
      emit ctx (Printf.sprintf "  store i32 %s, ptr %s, align 4" inc_val alloca_reg);
      emit ctx (Printf.sprintf "  br label %%%s" label_cond);
      
      (* 5. LOOP EXIT BOUNDARY *)
      emit ctx (Printf.sprintf "\n%s:" label_end)

let codegen_def ctx = function
  | Structure (_, _, _) -> () (* Handled in standard type tables layout *)
  
  | FuncDef (_, name, params, ret_type, body) ->
      let param_strs = List.map (fun (n, t) -> string_of_dt t ^ " %" ^ n) params in
      let params_joined = String.concat ", " param_strs in
      let ret_str = string_of_dt ret_type in
      
      emit ctx (Printf.sprintf "define %s @%s(%s) {" ret_str name params_joined);
      
      (* Map structural function inputs onto standard virtual variables lookup pointers *)
      List.iter (fun (n, dt) ->
        let t_str = string_of_dt dt in
        let alloca_reg = next_reg ctx in
        emit ctx (Printf.sprintf "  %s = alloca %s, align 4" alloca_reg t_str);
        emit ctx (Printf.sprintf "  store %s %%%s, ptr %s, align 4" t_str n alloca_reg);
        Hashtbl.add ctx.variables n alloca_reg
      ) params;
      
      List.iter (codegen_stmt ctx) body;
      
      (* Auto Void fallback return protection layer *)
      if ret_type = Nothing then emit ctx "  ret void";
      emit ctx "}\n"
      
(* 🆕 Global Registrar function to define layouts on the initial parser iteration pass *)
let register_definition ctx = function
  | Structure (_, name, fields) ->
      let indexed_fields = List.mapi (fun idx (fname, _) -> (fname, idx)) fields in
      Hashtbl.add struct_fields name indexed_fields;
      Hashtbl.add struct_field_types name fields;
      
      let field_types_joined = String.concat ", " (List.map (fun (_, dt) -> string_of_dt dt) fields) in
      let struct_decl = Printf.sprintf "%%struct.%s = type { %s }" name field_types_joined in
      ctx.globals <- struct_decl :: ctx.globals
  | FuncDef (_, _, _, _, _) -> ()
let generate_program prog =
  let ctx = create_context () in
  
  (* 🆕 Step 0: Pre-pass step to map and populate structure metadata types globally first *)
  List.iter (register_definition ctx) prog;
  
  (* 1. Process and generate the main bodies of all function declarations first *)
  List.iter (codegen_def ctx) prog;
  
  (* 2. Build the final output string starting with target data layout configurations *)
  let header_buf = Buffer.create 512 in
  let emit_header str = Buffer.add_string header_buf (str ^ "\n") in
  
  (* emit_header "target datalayout = \"e-m:o-i64:64-i128:128-n32:64-S128-Fn32\""; *)
  (* emit_header "target triple = \"arm64-apple-macosx\""; *)
  (* emit_header ""; *)
  
  (* Prepend all dynamically gathered global definitions and struct layout blocks *)
  List.iter (fun g -> emit_header g) (List.rev ctx.globals);
  if ctx.globals <> [] then emit_header "";
  
  (* Include your external runtime C headers *)
  emit ctx "declare i32 @printf(ptr, ...)";
  emit_header "declare ptr @malloc(i32)";
  emit_header "declare void @free(ptr)";
  emit_header "declare ptr @fopen(ptr, ptr)";
  emit_header "declare i32 @getc(ptr)";
  emit_header "declare i32 @putc(i32, ptr)";
  (* 🆕 Required by LLVM backends to copy structures cleanly via standard stack allocations *)
  emit_header "declare void @llvm.memcpy.p0.p0.i64(ptr nocapture writeonly, ptr nocapture readonly, i64, i1 immarg)";
  emit_header "";
  
  (* Combine headers and function bodies into one final cohesive program string *)
  Buffer.contents header_buf ^ Buffer.contents ctx.buf
