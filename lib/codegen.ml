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

(* 🆕 Map tracking "EnumName.MemberName" -> int value constant *)
let enum_values = Hashtbl.create 50

(* 🆕 Keeps track of the literal LLVM type string for every allocated variable *)
let local_types = Hashtbl.create 50

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
  | Short -> "i16"   (* 🆕 Added *)
  | Long -> "i64"     (* 🆕 Added *)
  | Single -> "float"  (* 🆕 Added *)
  | Double -> "double" (* 🆕 Added *) 
  | Nothing -> "void"
  | Pointer -> "ptr"
  | Custom name ->
      (* If it's a registered Enum, lower it natively to a plain integer *)
      if Hashtbl.mem enum_values (name ^ "._is_enum") then "i32"
      else Printf.sprintf "%%struct.%s" name


let rec get_struct_size struct_name =
  if Hashtbl.mem struct_field_types struct_name then
    let fields = Hashtbl.find struct_field_types struct_name in
    let raw_size = List.fold_left (fun acc (_, dt) ->
      acc + (match dt with
       | Byte -> 1
       | Short -> 2

       | Int | Single -> 4
       | Long | Double | Pointer -> 8
       | Nothing -> 0
       (* Inside get_struct_size *)
       | Custom name ->
          if Hashtbl.mem enum_values (name ^ "._is_enum") then 4
          else get_struct_size name (* Calculates size of nested fields recursively *)
          )
        ) 0 fields in
        ((raw_size + 7) / 8) * 8
  else 16

(* Inside your main compilation driver function tracking definitions *)
let compile_enum_definition enum_name members =
  Hashtbl.add enum_values (enum_name ^ "._is_enum") 1; (* 👈 Anchor tag for identification *)
  List.iter (fun (member_name, value) ->
    Hashtbl.add enum_values (enum_name ^ "." ^ member_name) value
  ) members

(* Returns (result_register_name, type_string) *)
let rec codegen_expr ctx = function
  | IntLit i -> (string_of_int i, "i32") 
  | FloatLit f -> (string_of_float f, "double") (* 🆕 Default to 64-bit double literal mapping *)
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
      (match base_expr with
       (* Case A: Static Enum Name Constants (e.g., NodeType.Literal) *)
       | Id name when Hashtbl.mem enum_values (name ^ "." ^ field_name) ->
           let lookup_key = name ^ "." ^ field_name in
           let value_int = Hashtbl.find enum_values lookup_key in
           (string_of_int value_int, "i32")
           
       (* Case B: Deep Dynamic Property Fields Paths Lookup *)
       | _ ->
           (* 1. Use the helper to compute the exact inner element memory pointer *)
           let field_ptr, final_type = get_field_pointer ctx (FieldAccess(base_expr, field_name)) in
           
           (* 2. Check if the final path points to an entire embedded sub-struct block *)
           if Hashtbl.mem struct_fields final_type then
             (field_ptr, "ptr")
           else begin
             (* 3. If it's a primitive scalar (like float, double, or i64), load the data *)
             let res_reg = next_reg ctx in
             emit ctx (Printf.sprintf "  %s = load %s, ptr %s, align 8" res_reg final_type field_ptr);
             (res_reg, final_type)
           end)


  | UnaryOp ("Not", e) ->
      let v, t = codegen_expr ctx e in
      let res_reg = next_reg ctx in
      (* Bitwise XOR with -1 flips all bits, achieving a logical/bitwise 'Not' *)
      emit ctx (Printf.sprintf "  %s = xor %s %s, -1" res_reg t v);
      (res_reg, t)
  | UnaryOp (_, _) -> raise (Error "Unsupported unary operation")

 
  | BinOp (e1, op, e2) ->
      let v1, t1 = codegen_expr ctx e1 in
      let v2, t2 = codegen_expr ctx e2 in
      
      (* ⚠️ No hidden casting: types must align exactly *)
      if t1 <> t2 then raise (Error ("Type mismatch in binary operation: " ^ t1 ^ " vs " ^ t2));
      
      let res_reg = next_reg ctx in
      let is_fp = (t1 = "float" || t1 = "double") in
      
      if is_fp then begin
        (* --- FLOATING POINT OPERATIONS --- *)
        let op_str = match op with
          | Add -> "fadd"
          | Sub -> "fsub"
          | Mul -> "fmul"
          | Div -> "fdiv"
          | Mod -> "frem"
          (* FP Comparisons *)
          | Equal        -> "fcmp oeq"
          | Greater      -> "fcmp ogt"
          | Less         -> "fcmp olt"
          | GreaterEqual -> "fcmp oge"
          | LessEqual    -> "fcmp ole"
          | NotEqual     -> "fcmp one"
          | _ -> raise (Error "Unsupported operation on floating point values")
        in
        emit ctx (Printf.sprintf "  %s = %s %s %s, %s" res_reg op_str t1 v1 v2);
        let ret_type = match op with

          | Equal | Greater | Less | GreaterEqual | LessEqual | NotEqual -> "i1"
          | _ -> t1
        in
        (res_reg, ret_type)
      end else begin
        (* --- STANDARD INTEGER OPERATIONS --- *)
        let op_str = match op with
          | Add -> "add nsw i32" (* Or match t1 for i8/i16/i64 configurations *)
          | Sub -> "sub nsw i32"
          | Mul -> "mul nsw i32"
          | Div -> "sdiv i32"
          | Mod -> "srem i32"
          | And -> "and i32"
          | Or  -> "or i32"
          | Xor -> "xor i32"
          | Shl -> "shl i32"
          | Shr -> "lshr i32"
          | Equal        -> "icmp eq i32"
          | Greater      -> "icmp sgt i32"
          | Less         -> "icmp slt i32"
          | GreaterEqual -> "icmp sge i32"
          | LessEqual    -> "icmp sle i32"
          | NotEqual     -> "icmp ne i32"
        in
        emit ctx (Printf.sprintf "  %s = %s %s, %s" res_reg op_str v1 v2);
        let ret_type = match op with

          | Equal | Greater | Less | GreaterEqual | LessEqual | NotEqual -> "i1"
          | _ -> t1
        in
        (res_reg, ret_type)
      end
 
  | Call ("printf", args) ->
      let evaluated_args = List.map (codegen_expr ctx) args in
      
      (* 🆕 Fix: Promote any 'float' argument to 'double' to respect C Variadic ABI requirements *)
      let promoted_args = List.map (fun (v, t) ->
        if t = "float" then begin
          let cast_reg = next_reg ctx in
          emit ctx (Printf.sprintf "  %s = fpext float %s to double" cast_reg v);
          (cast_reg, "double")
        end else
          (v, t)
      ) evaluated_args in
      
      let arg_strs = List.map (fun (v, t) -> t ^ " " ^ v) promoted_args in
      let args_joined = String.concat ", " arg_strs in
      let res_reg = next_reg ctx in
      
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
          let expected_dt_str = string_of_dt actual_dt in 
          
          let v, dt_str = codegen_expr ctx arg_expr in 
          let field_ptr = next_reg ctx in
          emit ctx (Printf.sprintf "  %s = getelementptr inbounds %s, ptr %s, i32 0, i32 %d" 
                     field_ptr struct_type_str alloca_reg idx);
                     
          (* ✅ FIXED: If this field is a nested sub-struct, copy it via memcpy *)
          match actual_dt with
          | Custom child_struct_name when Hashtbl.mem struct_fields child_struct_name ->
              let byte_size = get_struct_size child_struct_name in
              emit ctx (Printf.sprintf "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %s, ptr align 8 %s, i64 %d, i1 false)" 
                         field_ptr v byte_size)
          | _ ->
              if expected_dt_str = "ptr" && dt_str = "i32" then begin
                let cast_reg = next_reg ctx in
                emit ctx (Printf.sprintf "  %s = inttoptr i32 %s to ptr" cast_reg v);
                emit ctx (Printf.sprintf "  store ptr %s, ptr %s, align 4" cast_reg field_ptr)
              end else
                emit ctx (Printf.sprintf "  store %s %s, ptr %s, align 4" expected_dt_str v field_ptr)
        ) args;
        (alloca_reg, "ptr")
      end else begin
        (* Keep your standard external runtime fallback function calls logic exactly the same *)
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

and get_field_pointer ctx = function
  | Id name ->
      if Hashtbl.mem ctx.variables name then
        let base_ptr = Hashtbl.find ctx.variables name in
        let struct_name = Hashtbl.find var_types name in
        (base_ptr, struct_name)
      else
        raise (Error ("Undefined variable base identifier: " ^ name))

  | FieldAccess (sub_expr, field_name) ->
      (* 1. Recursively bubble up the pointer and the custom layout type of the parent layer *)
      let parent_ptr, parent_struct = get_field_pointer ctx sub_expr in
      
      (* 2. Look up field index within the parent struct blueprint *)
      let fields = Hashtbl.find struct_fields parent_struct in
      let field_index = List.assoc field_name fields in
      
      (* 3. Fetch the metadata type of this subfield *)
      let field_types = Hashtbl.find struct_field_types parent_struct in
      let actual_dt = List.assoc field_name field_types in
      
      (* 4. Shift pointer offset using getelementptr *)
      let field_ptr = next_reg ctx in
      let struct_type_str = Printf.sprintf "%%struct.%s" parent_struct in
      emit ctx (Printf.sprintf "  %s = getelementptr inbounds %s, ptr %s, i32 0, i32 %d" 
                 field_ptr struct_type_str parent_ptr field_index);
      
      (* 5. Crucial step: Return the child structure name string as the type identifier *)
      (match actual_dt with
       | Custom child_struct -> (field_ptr, child_struct)
       | _ -> (field_ptr, string_of_dt actual_dt))

  | _ -> raise (Error "LHS evaluation expects an explicit identifier or nested property pathway")

let rec codegen_stmt ctx = function

  | Dim (name, dt, exp) ->
      (match exp with
       | Call (struct_name, _) when Hashtbl.mem struct_fields struct_name ->
           Hashtbl.add var_types name struct_name
       | _ -> ());
       
      let v, t = codegen_expr ctx exp in
      let alloca_reg = next_reg ctx in
      
      let typ_str = 
        if Hashtbl.mem var_types name then Printf.sprintf "%%struct.%s" (Hashtbl.find var_types name)
        else string_of_dt dt 
      in
      emit ctx (Printf.sprintf "  %s = alloca %s, align 8" alloca_reg typ_str);
      
      Hashtbl.add local_types name typ_str;
      
      if Hashtbl.mem var_types name then begin
        (* 👇 FIXED: Size parameter is now fully dynamic based on structure layout fields *)
        let struct_name = Hashtbl.find var_types name in
        let byte_size = get_struct_size struct_name in
        emit ctx (Printf.sprintf "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %s, ptr align 8 %s, i64 %d, i1 false)" 
                   alloca_reg v byte_size)
      end else if typ_str = "ptr" && t = "i32" then begin
        let cast_reg = next_reg ctx in
        emit ctx (Printf.sprintf "  %s = inttoptr i32 %s to ptr" cast_reg v);
        emit ctx (Printf.sprintf "  store ptr %s, ptr %s, align 8" cast_reg alloca_reg)
      end else
        emit ctx (Printf.sprintf "  store %s %s, ptr %s, align 8" typ_str v alloca_reg);
        
      Hashtbl.add ctx.variables name alloca_reg


  | Assign (name, exp) ->
      if Hashtbl.mem ctx.variables name then
        let alloca_reg = Hashtbl.find ctx.variables name in
        let v, t = codegen_expr ctx exp in
        
        if Hashtbl.mem var_types name then begin
          (* 👇 FIXED: Size parameter is now fully dynamic based on structure layout fields *)
          let struct_name = Hashtbl.find var_types name in
          let byte_size = get_struct_size struct_name in
          emit ctx (Printf.sprintf "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %s, ptr align 8 %s, i64 %d, i1 false)" 
                     alloca_reg v byte_size)
        end else begin
          let expected_type = try Hashtbl.find local_types name with _ -> "i32" in
          if expected_type = "ptr" && t = "i32" then begin
            let cast_reg = next_reg ctx in
            emit ctx (Printf.sprintf "  %s = inttoptr i32 %s to ptr" cast_reg v);
            emit ctx (Printf.sprintf "  store ptr %s, ptr %s, align 4" cast_reg alloca_reg)
          end else
            emit ctx (Printf.sprintf "  store %s %s, ptr %s, align 4" t v alloca_reg)
        end
      else
        raise (Error ("Assignment target undefined: " ^ name))

  | FieldAssign (base_expr, field_name, value_expr) ->
      let v, dt_str = codegen_expr ctx value_expr in 
      let base_ptr, _ = codegen_expr ctx base_expr in
      
      let struct_name = match base_expr with
        | Id name -> Hashtbl.find var_types name
        | _ -> raise (Error "Field adjustments require an explicit instance identifier")
      in
      let fields = Hashtbl.find struct_fields struct_name in
      let field_index = List.assoc field_name fields in
      let field_types = Hashtbl.find struct_field_types struct_name in
      let actual_dt = List.assoc field_name field_types in
      
      let field_ptr = next_reg ctx in
      let struct_type_str = Printf.sprintf "%%struct.%s" struct_name in
      emit ctx (Printf.sprintf "  %s = getelementptr inbounds %s, ptr %s, i32 0, i32 %d" 
                 field_ptr struct_type_str base_ptr field_index);
      
      (* ✅ FIXED: If the destination field is an embedded struct, issue a block memory copy *)
      (match actual_dt with
       | Custom child_struct_name when Hashtbl.mem struct_fields child_struct_name ->
           let byte_size = get_struct_size child_struct_name in
           emit ctx (Printf.sprintf "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %s, ptr align 8 %s, i64 %d, i1 false)" 
                      field_ptr v byte_size)
       | _ ->
           let expected_dt_str = string_of_dt actual_dt in
           if expected_dt_str = "ptr" && dt_str = "i32" then begin
             let cast_reg = next_reg ctx in
             emit ctx (Printf.sprintf "  %s = inttoptr i32 %s to ptr" cast_reg v);
             emit ctx (Printf.sprintf "  store ptr %s, ptr %s, align 4" cast_reg field_ptr)
           end else
             emit ctx (Printf.sprintf "  store %s %s, ptr %s, align 4" expected_dt_str v field_ptr))

  | AssignExpr (lhs_expr, rhs_expr) ->
      let rhs_val, rhs_type = codegen_expr ctx rhs_expr in
      let target_ptr, target_type = get_field_pointer ctx lhs_expr in
      
      (* ✅ FIXED: Match target type mappings against global structure blueprints *)
      if Hashtbl.mem struct_fields target_type then begin
        let byte_size = get_struct_size target_type in
        emit ctx (Printf.sprintf "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %s, ptr align 8 %s, i64 %d, i1 false)" 
                   target_ptr rhs_val byte_size)
      end else begin
        emit ctx (Printf.sprintf "  store %s %s, ptr %s, align 8" rhs_type rhs_val target_ptr)
      end



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
  | FuncDef (_, name, params, ret_type, body) ->
      (* Clear local registers and variable tables before processing each unique function body *)
      Hashtbl.clear ctx.variables;
      Hashtbl.clear local_types;
      ctx.reg_counter <- 1;

      let param_strs = List.map (fun (n, t) -> string_of_dt t ^ " %" ^ n) params in
      let params_joined = String.concat ", " param_strs in
      let ret_str = string_of_dt ret_type in
      
      emit ctx (Printf.sprintf "define %s @%s(%s) {" ret_str name params_joined);
      
      (* Map function parameters to local stack variables *)
      List.iter (fun (n, dt) ->
        let t_str = string_of_dt dt in
        let alloca_reg = next_reg ctx in
        emit ctx (Printf.sprintf "  %s = alloca %s, align 8" alloca_reg t_str);
        emit ctx (Printf.sprintf "  store %s %%%s, ptr %s, align 8" t_str n alloca_reg);
        Hashtbl.add ctx.variables n alloca_reg;
        Hashtbl.add local_types n t_str;
        
        (* Track underlying structural metadata types for parameters *)
        (match dt with
         | Custom struct_name -> Hashtbl.add var_types n struct_name
         | _ -> ())
      ) params;
      
      List.iter (codegen_stmt ctx) body;
      
      (* Fallback protection for empty or branching void blocks *)
      if ret_type = Nothing then emit ctx "  ret void";
      emit ctx "}\n"
  
  | Structure (_, _, _) -> ()
  | EnumDef (_, _, _) -> () 

(* Global Pass Registrar to define metadata structures cleanly *)
let register_definition ctx = function
  | Structure (_, name, fields) ->
      let indexed_fields = List.mapi (fun idx (fname, _) -> (fname, idx)) fields in
      Hashtbl.add struct_fields name indexed_fields;
      Hashtbl.add struct_field_types name fields;
      
      (* Delay LLVM generation of the type signature until generate_program to ensure nested types are resolved *)
      ()

  | EnumDef (_, enum_name, members) ->
      Hashtbl.add enum_values (enum_name ^ "._is_enum") 1;
      List.iter (fun (member_name, value) ->
        let lookup_key = enum_name ^ "." ^ member_name in
        Hashtbl.add enum_values lookup_key value
      ) members
      
  | FuncDef (_, _, _, _, _) -> ()

let generate_program prog =
  let ctx = create_context () in
  
  (* Step 0: Populate structural metadata indices first *)
  List.iter (register_definition ctx) prog;
  
  (* Step 1: Generate dynamic LLVM struct type layout strings (safe for nested models) *)
  List.iter (function
    | Structure (_, name, fields) ->
        let field_types_joined = String.concat ", " (List.map (fun (_, dt) -> string_of_dt dt) fields) in
        let struct_decl = Printf.sprintf "%%struct.%s = type { %s }" name field_types_joined in
        ctx.globals <- struct_decl :: ctx.globals
    | _ -> ()
  ) prog;
  
  (* Step 2: Compile all functions *)
  List.iter (codegen_def ctx) prog;
  
  (* Step 3: Emit final header layouts *)
  let header_buf = Buffer.create 512 in
  let emit_header str = Buffer.add_string header_buf (str ^ "\n") in
  
  (* Add dynamic type definitions *)
  List.iter (fun g -> emit_header g) (List.rev ctx.globals);
  if ctx.globals <> [] then emit_header "";
  
  (* Standard C Library declarations *)
  emit_header "declare i32 @printf(ptr, ...)";
  emit_header "declare ptr @malloc(i32)";
  emit_header "declare void @free(ptr)";
  emit_header "declare ptr @fopen(ptr, ptr)";
  emit_header "declare i32 @getc(ptr)";
  emit_header "declare i32 @putc(i32, ptr)";
  emit_header "declare void @llvm.memcpy.p0.p0.i64(ptr nocapture writeonly, ptr nocapture readonly, i64, i1 immarg)";
  emit_header "";
  
  Buffer.contents header_buf ^ Buffer.contents ctx.buf
