open Ast 
open Token
(* ==============================================================================
   5. DEFINITIVE NORMALIZED EMITTER GENERATION LOGIC
   ============================================================================== *)
exception CodeGenError of string

(* Maps our AST primitive types to the corresponding low-level LLVM notation *)
let llvm_type_of = function
  | Integer -> "i32"
  | Byte    -> "i8"
  | Long    -> "i64"
  | Single  -> "float"
  | Double  -> "double"
  | Custom s -> "%struct." ^ s

(* Code Generator Context to track the current virtual SSA registers *)
type codegen_ctx = {
  mutable reg_counter : int;
  local_vars  : (string, string) Hashtbl.t;       
  local_types : (string, data_type) Hashtbl.t;    
  struct_maps : (string, (string * int) list) Hashtbl.t; (* Maps struct_name -> (field_name * index) *)
}

let new_reg ctx =
  ctx.reg_counter <- ctx.reg_counter + 1;
  "%" ^ string_of_int ctx.reg_counter
let rec codegen_expr ctx out_buf = function
  | Literal n -> n
  | Variable name -> 
      let l_name = String.lowercase_ascii name in
      if Hashtbl.mem ctx.local_vars l_name then
        let alloc_reg = Hashtbl.find ctx.local_vars l_name in
        let val_reg = new_reg ctx in
        let base_dt = Hashtbl.find ctx.local_types l_name in
        let typ = llvm_type_of base_dt in
        Buffer.add_string out_buf (Printf.sprintf "    %s = load %s, ptr %s, align 4\n" val_reg typ alloc_reg);
        val_reg
      else "%" ^ l_name
  | FieldAccess (obj_raw, field_raw) ->
      let obj_name = String.lowercase_ascii obj_raw in
      let field_name = String.lowercase_ascii field_raw in
      let obj_ptr = Hashtbl.find ctx.local_vars obj_name in
      let struct_type = Hashtbl.find ctx.local_types obj_name in
      let struct_name = match struct_type with 
        | Custom s -> String.lowercase_ascii s 
        | _ -> failwith "Not a structure object" 
      in
      let fields = Hashtbl.find ctx.struct_maps struct_name in
      let idx = List.assoc field_name fields in
      let element_ptr_reg = new_reg ctx in
      let val_reg = new_reg ctx in
      Buffer.add_string out_buf (Printf.sprintf "    %s = getelementptr inbounds %%struct.%s, ptr %s, i32 0, i32 %d\n" element_ptr_reg struct_name obj_ptr idx);
      Buffer.add_string out_buf (Printf.sprintf "    %s = load i32, ptr %s, align 4\n" val_reg element_ptr_reg);
      val_reg
  | BinOp (left, op, right) ->
      let l_val = codegen_expr ctx out_buf left in
      let r_val = codegen_expr ctx out_buf right in
      let res_reg = new_reg ctx in
      let llvm_op = match op with "+" -> "add nsw" | "-" -> "sub nsw" | "*" -> "mul nsw" | "/" -> "sdiv" | _ -> failwith "Op error" in
      Buffer.add_string out_buf (Printf.sprintf "    %s = %s i32 %s, %s\n" res_reg llvm_op l_val r_val);
      res_reg

let codegen_stmt ctx out_buf = function
  | VarDecl (raw_name, dt, init_opt) ->
      let name = String.lowercase_ascii raw_name in
      let norm_dt = match dt with Custom s -> Custom (String.lowercase_ascii s) | other -> other in
      let llvm_type = llvm_type_of norm_dt in
      let alloc_reg = "%" ^ name ^ ".alloc" in
      Hashtbl.add ctx.local_vars name alloc_reg;
      Hashtbl.add ctx.local_types name norm_dt;
      Buffer.add_string out_buf (Printf.sprintf "    %s = alloca %s, align 4\n" alloc_reg llvm_type);
      (match norm_dt with
       | Custom _ -> () 
       | _ ->
            (match init_opt with
            | Some expr ->  
                let expr_reg = codegen_expr ctx out_buf expr in
                Buffer.add_string out_buf (Printf.sprintf "    store %s %s, ptr %s, align 4\n" llvm_type expr_reg alloc_reg)
            | None -> ()))
  | Assign (FieldAccess (obj_raw, field_raw), expr) ->
      let obj_name = String.lowercase_ascii obj_raw in
      let field_name = String.lowercase_ascii field_raw in
      let obj_ptr = Hashtbl.find ctx.local_vars obj_name in
      let struct_type = Hashtbl.find ctx.local_types obj_name in
      let struct_name = match struct_type with 
        | Custom s -> String.lowercase_ascii s 
        | _ -> failwith "Target mismatch" 
      in
      let fields = Hashtbl.find ctx.struct_maps struct_name in
      let idx = List.assoc field_name fields in
      let expr_reg = codegen_expr ctx out_buf expr in
      let element_ptr_reg = new_reg ctx in
      Buffer.add_string out_buf (Printf.sprintf "    %s = getelementptr inbounds %%struct.%s, ptr %s, i32 0, i32 %d\n" element_ptr_reg struct_name obj_ptr idx);
      Buffer.add_string out_buf (Printf.sprintf "    store i32 %s, ptr %s, align 4\n" expr_reg element_ptr_reg)
  | Assign (Variable raw_name, expr) ->
      let name = String.lowercase_ascii raw_name in
      let alloc_reg = Hashtbl.find ctx.local_vars name in
      let typ = llvm_type_of (Hashtbl.find ctx.local_types name) in
      let expr_reg = codegen_expr ctx out_buf expr in
      Buffer.add_string out_buf (Printf.sprintf "    store %s %s, ptr %s, align 4\n" typ expr_reg alloc_reg)
  | Return expr ->
      let ret_reg = codegen_expr ctx out_buf expr in
      Buffer.add_string out_buf (Printf.sprintf "    ret i32 %s\n" ret_reg)
  | _ -> failwith "Unsupported statement type matching pass"

(* Main top-level emit pass loop entry *)
let emit_llvm program =
  let out_buf = Buffer.create 1024 in
  let global_struct_maps = Hashtbl.create 5 in
  
  (* PASS 1: Populate global tracking index maps with uniform lowercase keys *)
  List.iter (fun element ->
    match element with
    | Structure (raw_name, fields) ->
        let name = String.lowercase_ascii raw_name in
        let indexed_fields = List.mapi (fun idx (f_name, _, _) -> 
          (String.lowercase_ascii f_name, idx)
        ) fields in
        Hashtbl.add global_struct_maps name indexed_fields;
        
        Buffer.add_string out_buf (Printf.sprintf "%%struct.%s = type { " name);
        let field_strings = List.map (fun (_, dt, array_size_opt) ->
          let base_type = llvm_type_of dt in
          match array_size_opt with 
          | Some size -> Printf.sprintf "[%d x %s]" size base_type 
          | None -> base_type
        ) fields in
        Buffer.add_string out_buf (String.concat ", " field_strings);
        Buffer.add_string out_buf " }\n\n"
    | _ -> ()
  ) program;

  (* PASS 2: Emit validated LLVM functions *)
  List.iter (fun element ->
    match element with
    | Function (raw_name, params, ret_type, body) ->
        let name = String.lowercase_ascii raw_name in
        let ctx = { 
          reg_counter = 0; 
          local_vars = Hashtbl.create 10; 
          local_types = Hashtbl.create 10; 
          struct_maps = global_struct_maps 
        } in
        
        (* Register function parameters as accessible tracking variables *)
        let param_strings = List.map (fun (p_raw_name, p_dt) -> 
          let p_name = String.lowercase_ascii p_raw_name in
          let norm_dt = match p_dt with 
            | Custom s -> Custom (String.lowercase_ascii s) 
            | other -> other 
          in
          Hashtbl.add ctx.local_vars p_name ("%" ^ p_name);
          Hashtbl.add ctx.local_types p_name norm_dt;
          match norm_dt with 
          | Custom _ -> Printf.sprintf "ptr %%%s" p_name
          | other_dt -> Printf.sprintf "%s %%%s" (llvm_type_of norm_dt) p_name
        ) params in
        
        Buffer.add_string out_buf (Printf.sprintf "define %s @%s(%s) {\nentry:\n" 
          (llvm_type_of ret_type) name (String.concat ", " param_strings));
          
        List.iter (codegen_stmt ctx out_buf) body;
        Buffer.add_string out_buf "}\n\n"
    | _ -> ()
  ) program;
  Buffer.contents out_buf
