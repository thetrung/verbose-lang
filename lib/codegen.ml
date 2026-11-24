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
(* Track per-function parameter passing modes (ByVal / ByRef) *)
let func_param_modes : (string, param_mode list) Hashtbl.t = Hashtbl.create 32
(* Track user-declared external FFI functions *)
let extern_decls : (string, string) Hashtbl.t = Hashtbl.create 32
let extern_order : string list ref = ref []
let variadic_funcs : (string, string list) Hashtbl.t = Hashtbl.create 16
(* Track visibility of structures/enums *)
let visibility_table : (string, bool) Hashtbl.t = Hashtbl.create 16
(* Struct Field/Type  *)
let struct_fields = Hashtbl.create 10
let struct_field_types = Hashtbl.create 10
(* Variable, Local, Return Types  *)
let var_types = Hashtbl.create 50
let ret_types   = Hashtbl.create 50
let local_types = Hashtbl.create 50
let local_ast_types : (string, data_type) Hashtbl.t = Hashtbl.create 50
let indirect_arrays : (string, bool) Hashtbl.t = Hashtbl.create 16
let global_ast_types : (string, data_type) Hashtbl.t = Hashtbl.create 32
let global_indirect_arrays : (string, bool) Hashtbl.t = Hashtbl.create 16
let static_counter : int ref = ref 0
let current_func_name : string ref = ref ""
(* Enum Values  *)
let enum_values = Hashtbl.create 50


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
let rec string_of_dt = function
  | Int -> "i32"
  | Byte -> "i8"
  | Short -> "i16"   
  | Long -> "i64"    
  | Single -> "float" 
  | Double -> "double"
  | Boolean -> "i1"
  | Nothing -> "void"
  | Pointer -> "ptr"
  | Custom name ->
      (* If it's a registered Enum, lower it natively to a plain integer *)
      if Hashtbl.mem enum_values (name ^ "._is_enum") then "i32"
      else Printf.sprintf "%%struct.%s" name
  | Array (et, n) ->
      if n <= 0 then "ptr"
      else Printf.sprintf "[%d x %s]" n (string_of_dt et)

let is_struct_dt = function
  | Custom name -> Hashtbl.mem struct_fields name
  | _ -> false

let is_aggregate_dt = function
  | Custom name -> Hashtbl.mem struct_fields name
  | Array (_, n) -> n > 0
  | _ -> false

let llvm_param_type (mode, _, dt) =
  match dt with
  | Array _ -> "ptr"
  | Custom name when not (Hashtbl.mem enum_values (name ^ "._is_enum")) -> "ptr"
  | _ -> if mode = ByRef then "ptr" else string_of_dt dt

let rec get_type_align = function
  | Byte | Boolean -> 1
  | Short -> 2
  | Int | Single -> 4
  | Long | Double | Pointer -> 8
  | Nothing -> 1
  | Custom name ->
      if Hashtbl.mem enum_values (name ^ "._is_enum") then 4
      else get_struct_align name
  | Array (elem_dt, n) ->
      if n <= 0 then 8 else get_type_align elem_dt

and get_struct_align struct_name =
  if Hashtbl.mem struct_field_types struct_name then
    let fields = Hashtbl.find struct_field_types struct_name in
    List.fold_left (fun acc (_, dt) -> max acc (get_type_align dt)) 1 fields
  else 8

and get_type_size = function
  | Byte | Boolean -> 1
  | Short -> 2
  | Int | Single -> 4
  | Long | Double | Pointer -> 8
  | Nothing -> 0
  | Custom name ->
      if Hashtbl.mem enum_values (name ^ "._is_enum") then 4
      else get_struct_size name
  | Array (elem_dt, n) ->
      if n <= 0 then 8 else n * get_type_size elem_dt

and get_struct_size struct_name =
  if Hashtbl.mem struct_field_types struct_name then
    let fields = Hashtbl.find struct_field_types struct_name in
    let align_up off a = if a <= 1 then off else ((off + a - 1) / a) * a in
    let max_align = ref 1 in
    let off = List.fold_left (fun acc (_, dt) ->
      let a = get_type_align dt in
      if a > !max_align then max_align := a;
      let aligned = align_up acc a in
      aligned + get_type_size dt
    ) 0 fields in
    let total = align_up off !max_align in
    if total = 0 then 8 else total
  else 16

let emit_store_typed ctx expected_dt v dt_str target_ptr =
  if is_aggregate_dt expected_dt then begin
    let byte_size = get_type_size expected_dt in
    emit ctx (Printf.sprintf "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %s, ptr align 8 %s, i64 %d, i1 false)"
               target_ptr v byte_size)
  end else begin
    let expected_dt_str = string_of_dt expected_dt in
    let align = max 1 (get_type_align expected_dt) in
    let is_reg = String.length v > 0 && (v.[0] = '%' || v.[0] = '@') in
    if expected_dt_str = "ptr" && dt_str = "i32" then begin
      let cast_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = inttoptr i32 %s to ptr" cast_reg v);
      emit ctx (Printf.sprintf "  store ptr %s, ptr %s, align 8" cast_reg target_ptr)
    end else if expected_dt_str = "float" && dt_str = "double" && is_reg then begin
      let cast_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = fptrunc double %s to float" cast_reg v);
      emit ctx (Printf.sprintf "  store float %s, ptr %s, align 4" cast_reg target_ptr)
    end else if expected_dt_str = "double" && dt_str = "float" && is_reg then begin
      let cast_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = fpext float %s to double" cast_reg v);
      emit ctx (Printf.sprintf "  store double %s, ptr %s, align 8" cast_reg target_ptr)
    end else if expected_dt_str = "float" && dt_str = "double" && not is_reg then begin
      let f = float_of_string v in
      let f32 = Int32.float_of_bits (Int32.bits_of_float f) in
      let hex_bits = Printf.sprintf "0x%016LX" (Int64.bits_of_float f32) in
      emit ctx (Printf.sprintf "  store float %s, ptr %s, align 4" hex_bits target_ptr)
    end else if expected_dt_str = "i8" && (dt_str = "i32" || dt_str = "i64" || dt_str = "i16") && is_reg then begin
      let cast_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = trunc %s %s to i8" cast_reg dt_str v);
      emit ctx (Printf.sprintf "  store i8 %s, ptr %s, align 1" cast_reg target_ptr)
    end else if expected_dt_str = "i16" && (dt_str = "i32" || dt_str = "i64") && is_reg then begin
      let cast_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = trunc %s %s to i16" cast_reg dt_str v);
      emit ctx (Printf.sprintf "  store i16 %s, ptr %s, align 2" cast_reg target_ptr)
    end else if expected_dt_str = "i64" && (dt_str = "i32" || dt_str = "i16" || dt_str = "i8") && is_reg then begin
      let cast_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = sext %s %s to i64" cast_reg dt_str v);
      emit ctx (Printf.sprintf "  store i64 %s, ptr %s, align 8" cast_reg target_ptr)
    end else if expected_dt_str = "i32" && (dt_str = "i8" || dt_str = "i16") && is_reg then begin
      let cast_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = sext %s %s to i32" cast_reg dt_str v);
      emit ctx (Printf.sprintf "  store i32 %s, ptr %s, align 4" cast_reg target_ptr)
    end else
      emit ctx (Printf.sprintf "  store %s %s, ptr %s, align %d" expected_dt_str v target_ptr align)
  end

let lookup_var ctx name =
  if Hashtbl.mem ctx.variables name then
    let reg = Hashtbl.find ctx.variables name in
    let dt =
      match Hashtbl.find_opt local_ast_types name with
      | Some d -> d
      | None ->
          if Hashtbl.mem var_types name then Custom (Hashtbl.find var_types name)
          else Int
    in
    Some (reg, dt, Hashtbl.mem indirect_arrays name)
  else if Hashtbl.mem global_ast_types name then
    Some ("@" ^ name, Hashtbl.find global_ast_types name, Hashtbl.mem global_indirect_arrays name)
  else
    None

let zero_llvm_value = function
  | Int | Byte | Short | Long -> "0"
  | Single | Double -> "0.0"
  | Boolean -> "false"
  | Pointer | Array (_, -1) -> "null"
  | Custom name when Hashtbl.mem enum_values (name ^ "._is_enum") -> "0"
  | Custom _ | Array _ -> "zeroinitializer"
  | Nothing -> "0"

let rec const_llvm_value ctx dt = function
  | None -> zero_llvm_value dt
  | Some expr ->
      (match dt, expr with
       | Pointer, IntLit 0 -> "null"
       | Array (_, -1), IntLit 0 -> "null"
       | (Int | Byte | Short | Long), IntLit i -> string_of_int i
       | Custom name, IntLit i when Hashtbl.mem enum_values (name ^ "._is_enum") -> string_of_int i
       | Single, FloatLit f ->
           let f32 = Int32.float_of_bits (Int32.bits_of_float f) in
           Printf.sprintf "0x%016LX" (Int64.bits_of_float f32)
       | Double, FloatLit f -> string_of_float f
       | Boolean, BooleanLit b -> if b then "true" else "false"
       | Pointer, StringLit s ->
           incr static_counter;
           let global_reg = Printf.sprintf "@.str_g_%d" !static_counter in
           let len = String.length s + 1 in
           let decl = Printf.sprintf "%s = private unnamed_addr constant [%d x i8] c\"%s\\00\", align 1"
                        global_reg len s in
           ctx.globals <- decl :: ctx.globals;
           global_reg
       | (Int | Byte | Short | Long | Custom _), FieldAccess (Id ename, mname)
         when Hashtbl.mem enum_values (ename ^ "." ^ mname) ->
           string_of_int (Hashtbl.find enum_values (ename ^ "." ^ mname))
       | Array (elem_dt, n), ArrayLit elems when n > 0 ->
           if List.length elems > n then
             raise (Error (Printf.sprintf "Static array initializer has %d elements, exceeding size %d"
                             (List.length elems) n));
           let elem_llvm = string_of_dt elem_dt in
           let rendered = List.map (fun e -> elem_llvm ^ " " ^ const_llvm_value ctx elem_dt (Some e)) elems in
           let pad_count = n - List.length elems in
           let pads = List.init pad_count (fun _ -> elem_llvm ^ " " ^ zero_llvm_value elem_dt) in
           "[" ^ String.concat ", " (rendered @ pads) ^ "]"
       | Custom sn, StructInit (init_sn, inits) when Hashtbl.mem struct_fields sn ->
           if sn <> init_sn then
             raise (Error (Printf.sprintf "Static struct type mismatch: expected '%s', got '%s'" sn init_sn));
           let field_types = Hashtbl.find struct_field_types sn in
           let seen = Hashtbl.create (List.length inits) in
           List.iter (fun (fname, _) ->
             if Hashtbl.mem seen fname then
               raise (Error (Printf.sprintf "Duplicate field '%s' in '%s' With initializer" fname sn));
             if not (List.mem_assoc fname field_types) then
               raise (Error (Printf.sprintf "Structure '%s' has no field named '%s'" sn fname));
             Hashtbl.add seen fname ()
           ) inits;
           let rendered = List.map (fun (fname, fdt) ->
             let f_llvm = string_of_dt fdt in
             let f_val = const_llvm_value ctx fdt (List.assoc_opt fname inits) in
             f_llvm ^ " " ^ f_val
           ) field_types in
           "{ " ^ String.concat ", " rendered ^ " }"
       | _ ->
           raise (Error "Static variable initializer must be a compile-time constant"))

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
  | BooleanLit b -> 
      let bool_value = if b then "1" else "0" 
      in (bool_value, "i1")
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
    (match lookup_var ctx name with
     | Some (alloca_reg, dt, is_indirect) ->
         (match dt with
          | d when is_aggregate_dt d -> (alloca_reg, "ptr")
          | Array (_, -1) ->
              if is_indirect then begin
                let res_reg = next_reg ctx in
                emit ctx (Printf.sprintf "  %s = load ptr, ptr %s, align 8" res_reg alloca_reg);
                (res_reg, "ptr")
              end else
                (alloca_reg, "ptr")
          | d ->
              let t_str = string_of_dt d in
              let align = max 1 (get_type_align d) in
              let res_reg = next_reg ctx in
              emit ctx (Printf.sprintf "  %s = load %s, ptr %s, align %d" res_reg t_str alloca_reg align);
              (res_reg, t_str))
     | None ->
         raise (Error ("Undefined variable reference: " ^ name)))

 
  | FieldAccess (base_expr, field_name) ->
      (match base_expr with
       (* Case A: Static Enum Name Constants (e.g., NodeType.Literal) *)
       | Id name when Hashtbl.mem enum_values (name ^ "." ^ field_name) ->
           let lookup_key = name ^ "." ^ field_name in
           let value_int = Hashtbl.find enum_values lookup_key in
           (string_of_int value_int, "i32")
           
       (* Case B: Deep Dynamic Property Fields Paths Lookup *)
       | _ ->
           let field_ptr, final_dt = get_lvalue_ptr ctx (FieldAccess(base_expr, field_name)) in
           if is_aggregate_dt final_dt then
             (field_ptr, "ptr")
           else begin
             let final_type = string_of_dt final_dt in
             let align = max 1 (get_type_align final_dt) in
             let res_reg = next_reg ctx in
             emit ctx (Printf.sprintf "  %s = load %s, ptr %s, align %d" res_reg final_type field_ptr align);
             (res_reg, final_type)
           end)

  | IndexAccess (base_expr, idx_expr) ->
      let elem_ptr, elem_dt = get_lvalue_ptr ctx (IndexAccess(base_expr, idx_expr)) in
      if is_aggregate_dt elem_dt then
        (elem_ptr, "ptr")
      else begin
        let elem_type = string_of_dt elem_dt in
        let align = max 1 (get_type_align elem_dt) in
        let res_reg = next_reg ctx in
        emit ctx (Printf.sprintf "  %s = load %s, ptr %s, align %d" res_reg elem_type elem_ptr align);
        (res_reg, elem_type)
      end

  | ArrayLit _ ->
      raise (Error "Array literal must be used to initialize an array variable")


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
          | Add -> "add nsw " (* Or match t1 for i8/i16/i64 configurations *)
          | Sub -> "sub nsw "
          | Mul -> "mul nsw "
          | Div -> "sdiv "
          | Mod -> "srem "
          | And -> "and "
          | Or  -> "or "
          | Xor -> "xor "
          | Shl -> "shl "
          | Shr -> "lshr "
          | Equal        -> "icmp eq "
          | Greater      -> "icmp sgt "
          | Less         -> "icmp slt "
          | GreaterEqual -> "icmp sge "
          | LessEqual    -> "icmp sle "
          | NotEqual     -> "icmp ne "
        in
        emit ctx (Printf.sprintf "  %s = %s %s %s, %s" res_reg op_str t1 v1 v2);
        let ret_type = match op with

          | Equal | Greater | Less | GreaterEqual | LessEqual | NotEqual -> "i1"
          | _ -> t1
        in
        (res_reg, ret_type)
      end
 
  | StructInit (name, inits) ->
      if not (Hashtbl.mem struct_fields name) then
        raise (Error ("Unknown structure in With initializer: " ^ name));
      let struct_type_str = Printf.sprintf "%%struct.%s" name in
      let alloca_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = alloca %s, align 8" alloca_reg struct_type_str);
      emit ctx (Printf.sprintf "  store %s zeroinitializer, ptr %s, align 8" struct_type_str alloca_reg);

      let fields = Hashtbl.find struct_fields name in
      let field_types = Hashtbl.find struct_field_types name in
      let seen = Hashtbl.create (List.length inits) in

      List.iter (fun (field_name, arg_expr) ->
        if Hashtbl.mem seen field_name then
          raise (Error (Printf.sprintf "Duplicate field '%s' in '%s' With initializer" field_name name));
        Hashtbl.add seen field_name ();
        let idx =
          try List.assoc field_name fields
          with Not_found ->
            raise (Error (Printf.sprintf "Structure '%s' has no field named '%s'" name field_name))
        in
        let actual_dt = List.assoc field_name field_types in
        let expected_dt_str = string_of_dt actual_dt in
        let v, dt_str = codegen_expr ctx arg_expr in
        let field_ptr = next_reg ctx in
        emit ctx (Printf.sprintf "  %s = getelementptr inbounds %s, ptr %s, i32 0, i32 %d"
                   field_ptr struct_type_str alloca_reg idx);
        match actual_dt with
        | Custom child_struct_name when Hashtbl.mem struct_fields child_struct_name ->
            let byte_size = get_struct_size child_struct_name in
            emit ctx (Printf.sprintf "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %s, ptr align 8 %s, i64 %d, i1 false)"
                       field_ptr v byte_size)
        | _ ->
            let is_reg = String.length v > 0 && (v.[0] = '%' || v.[0] = '@') in
            if expected_dt_str = "ptr" && dt_str = "i32" then begin
              let cast_reg = next_reg ctx in
              emit ctx (Printf.sprintf "  %s = inttoptr i32 %s to ptr" cast_reg v);
              emit ctx (Printf.sprintf "  store ptr %s, ptr %s, align 4" cast_reg field_ptr)
            end else if expected_dt_str = "float" && dt_str = "double" && is_reg then begin
              let cast_reg = next_reg ctx in
              emit ctx (Printf.sprintf "  %s = fptrunc double %s to float" cast_reg v);
              emit ctx (Printf.sprintf "  store float %s, ptr %s, align 4" cast_reg field_ptr)
            end else if expected_dt_str = "double" && dt_str = "float" && is_reg then begin
              let cast_reg = next_reg ctx in
              emit ctx (Printf.sprintf "  %s = fpext float %s to double" cast_reg v);
              emit ctx (Printf.sprintf "  store double %s, ptr %s, align 8" cast_reg field_ptr)
            end else if expected_dt_str = "float" && dt_str = "double" && not is_reg then begin
              let f = float_of_string v in
              let f32 = Int32.float_of_bits (Int32.bits_of_float f) in
              let hex_bits = Printf.sprintf "0x%016LX" (Int64.bits_of_float f32) in
              emit ctx (Printf.sprintf "  store float %s, ptr %s, align 4" hex_bits field_ptr)
            end else
              emit ctx (Printf.sprintf "  store %s %s, ptr %s, align 4" expected_dt_str v field_ptr)
      ) inits;
      (alloca_reg, "ptr")

  | SizeOf dt ->
      (string_of_int (get_type_size dt), "i32")

  | HeapAlloc (dt, count_expr) ->
      let elem_size = get_type_size dt in
      if elem_size <= 0 then
        raise (Error "HeapAlloc requires a non-empty element type");
      let count64 = widen_index_to_i64 ctx count_expr in
      let res_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = call ptr @calloc(i64 %s, i64 %d)" res_reg count64 elem_size);
      (res_reg, "ptr")

  | HeapRealloc (ptr_expr, dt, count_expr) ->
      let elem_size = get_type_size dt in
      if elem_size <= 0 then
        raise (Error "HeapRealloc requires a non-empty element type");
      let pv, pt = codegen_expr ctx ptr_expr in
      if pt <> "ptr" then
        raise (Error ("HeapRealloc expects a Pointer or heap array, got " ^ pt));
      let count64 = widen_index_to_i64 ctx count_expr in
      let bytes_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = mul i64 %s, %d" bytes_reg count64 elem_size);
      let res_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = call ptr @realloc(ptr %s, i64 %s)" res_reg pv bytes_reg);
      (res_reg, "ptr")

  | Call ("HeapAllocBytes", [(ByVal, size_expr)]) ->
      let size64 = widen_index_to_i64 ctx size_expr in
      let res_reg = next_reg ctx in
      emit ctx (Printf.sprintf "  %s = call ptr @calloc(i64 1, i64 %s)" res_reg size64);
      (res_reg, "ptr")

  | Call ("HeapAllocBytes", _) ->
      raise (Error "HeapAllocBytes() expects exactly one ByVal byte-count argument")

  | Call ("HeapFree", [(ByVal, ptr_expr)]) ->
      let pv, pt = codegen_expr ctx ptr_expr in
      if pt <> "ptr" then
        raise (Error ("HeapFree expects a Pointer or heap array, got " ^ pt));
      emit ctx (Printf.sprintf "  call void @free(ptr %s)" pv);
      ("", "void")

  | Call ("HeapFree", _) ->
      raise (Error "HeapFree() expects exactly one ByVal pointer/array argument")

  | Call ("Len", [(ByVal, arg_expr)]) ->
      let _, dt = get_lvalue_ptr ctx arg_expr in
      (match dt with
       | Array (_, n) when n > 0 -> (string_of_int n, "i32")
       | _ -> raise (Error "Len() requires a fixed-size array"))

  | Call ("Len", _) ->
      raise (Error "Len() expects exactly one ByVal fixed-size array argument")

  | Call ("printf", args) ->
      let evaluated_args = List.map (fun (mode, e) ->
        if mode = ByRef then raise (Error "printf does not accept ByRef arguments")
        else codegen_expr ctx e
      ) args in
      
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
        
        List.iteri (fun idx (arg_mode, arg_expr) ->
          if arg_mode = ByRef then raise (Error ("Struct constructor '" ^ name ^ "' does not accept ByRef arguments"));
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
        (* Evaluate call arguments requiring explicit call-site ByRef matching callee signature *)
        let param_modes = Hashtbl.find_opt func_param_modes name in
        let eval_byref_arg arg_expr =
          match arg_expr with
          | Id var_name ->
              (match lookup_var ctx var_name with
               | Some (reg, _, true) ->
                   let loaded = next_reg ctx in
                   emit ctx (Printf.sprintf "  %s = load ptr, ptr %s, align 8" loaded reg);
                   (loaded, "ptr")
               | Some (reg, _, false) -> (reg, "ptr")
               | None ->
                   raise (Error ("Undefined variable reference for ByRef argument: " ^ var_name)))
          | FieldAccess _ | IndexAccess _ ->
              let lval_ptr, _ = get_lvalue_ptr ctx arg_expr in
              (lval_ptr, "ptr")
          | _ ->
              raise (Error "ByRef argument must be a variable, struct field, or array element")
        in
        let is_var = Hashtbl.mem variadic_funcs name in
        let evaluated_args = List.mapi (fun idx (arg_mode, arg_expr) ->
          match param_modes with
          | Some modes ->
              (match List.nth_opt modes idx with
               | Some ByRef ->
                   if arg_mode = ByRef then eval_byref_arg arg_expr
                   else raise (Error (Printf.sprintf "Argument %d of '%s' expects ByRef at call site" (idx + 1) name))
               | Some ByVal ->
                   if arg_mode = ByRef then
                     raise (Error (Printf.sprintf "Argument %d of '%s' is ByVal and cannot be passed ByRef" (idx + 1) name))
                   else codegen_expr ctx arg_expr
               | None ->
                   (* Variadic tail argument: allow ByRef for out-pointers, promote float->double for ByVal *)
                   if is_var && arg_mode = ByRef then eval_byref_arg arg_expr
                   else if arg_mode = ByRef then
                     raise (Error (Printf.sprintf "Argument %d of '%s' is ByVal and cannot be passed ByRef" (idx + 1) name))
                   else
                     let v, t = codegen_expr ctx arg_expr in
                     if is_var && t = "float" then begin
                       let cast_reg = next_reg ctx in
                       emit ctx (Printf.sprintf "  %s = fpext float %s to double" cast_reg v);
                       (cast_reg, "double")
                     end else (v, t))
          | None ->
              if arg_mode = ByRef then eval_byref_arg arg_expr
              else codegen_expr ctx arg_expr
        ) args in
        let arg_strs = List.map (fun (v, t) -> 
          let checked_type = if name = "free" then "ptr" else t in
          checked_type ^ " " ^ v) evaluated_args in
        let args_joined = String.concat ", " arg_strs in
        let res_reg = next_reg ctx in
        let ret_type =
          if Hashtbl.mem ret_types name then
            Hashtbl.find ret_types name
          else match name with
            | "malloc" | "fopen" -> "ptr"
            | "free" -> "void"
            | "getc" | "putc" -> "i32"
            | _ -> raise (Error("Can't find ReturnType of " ^ name))
        in
        let var_sig_prefix =
          if is_var then
            let fixed = Hashtbl.find variadic_funcs name in
            let inner = match fixed with
              | [] -> "..."
              | ps -> String.concat ", " ps ^ ", ..."
            in
            Printf.sprintf " (%s)" inner
          else ""
        in
        if ret_type = "void" then begin
          emit ctx (Printf.sprintf "  call void%s @%s(%s)" var_sig_prefix name args_joined);
          ("", "void")
        end else begin
          emit ctx (Printf.sprintf "  %s = call %s%s @%s(%s)" res_reg ret_type var_sig_prefix name args_joined);
          (res_reg, ret_type)
        end
      end

and widen_index_to_i64 ctx idx_expr =
  let idx_v, idx_t = codegen_expr ctx idx_expr in
  let is_reg = String.length idx_v > 0 && (idx_v.[0] = '%' || idx_v.[0] = '@') in
  match idx_t with
  | "i64" -> idx_v
  | "i32" | "i16" | "i8" ->
      if is_reg then begin
        let ext_reg = next_reg ctx in
        emit ctx (Printf.sprintf "  %s = sext %s %s to i64" ext_reg idx_t idx_v);
        ext_reg
      end else idx_v
  | _ -> raise (Error ("Array index must be an integer type, got " ^ idx_t))

and get_lvalue_ptr ctx = function
  | Id name ->
      (match lookup_var ctx name with
       | Some (base_ptr, dt, _) -> (base_ptr, dt)
       | None -> raise (Error ("Undefined variable base identifier: " ^ name)))

  | FieldAccess (sub_expr, field_name) ->
      let parent_ptr, parent_dt = get_lvalue_ptr ctx sub_expr in
      let parent_struct =
        match parent_dt with
        | Custom s when Hashtbl.mem struct_fields s -> s
        | _ -> raise (Error ("Field access requires a structure value before ." ^ field_name))
      in
      let fields = Hashtbl.find struct_fields parent_struct in
      let field_index =
        try List.assoc field_name fields
        with Not_found -> raise (Error (Printf.sprintf "Structure '%s' has no field named '%s'" parent_struct field_name))
      in
      let field_types = Hashtbl.find struct_field_types parent_struct in
      let actual_dt = List.assoc field_name field_types in
      let field_ptr = next_reg ctx in
      let struct_type_str = Printf.sprintf "%%struct.%s" parent_struct in
      emit ctx (Printf.sprintf "  %s = getelementptr inbounds %s, ptr %s, i32 0, i32 %d"
                 field_ptr struct_type_str parent_ptr field_index);
      (field_ptr, actual_dt)

  | IndexAccess (base_expr, idx_expr) ->
      let idx64 = widen_index_to_i64 ctx idx_expr in
      let is_lval = match base_expr with Id _ | FieldAccess _ | IndexAccess _ -> true | _ -> false in
      if is_lval then begin
        let base_ptr, base_dt = get_lvalue_ptr ctx base_expr in
        match base_dt with
        | Array (elem_dt, n) when n > 0 ->
            let arr_llvm = string_of_dt base_dt in
            let elem_ptr = next_reg ctx in
            emit ctx (Printf.sprintf "  %s = getelementptr inbounds %s, ptr %s, i64 0, i64 %s"
                       elem_ptr arr_llvm base_ptr idx64);
            (elem_ptr, elem_dt)
        | Array (elem_dt, -1) ->
            let raw_ptr =
              match base_expr with
              | Id var_name ->
                  (match lookup_var ctx var_name with
                   | Some (_, _, true) ->
                       let p = next_reg ctx in
                       emit ctx (Printf.sprintf "  %s = load ptr, ptr %s, align 8" p base_ptr);
                       p
                   | _ -> base_ptr)
              | _ -> base_ptr
            in
            let elem_llvm = string_of_dt elem_dt in
            let elem_ptr = next_reg ctx in
            emit ctx (Printf.sprintf "  %s = getelementptr inbounds %s, ptr %s, i64 %s"
                       elem_ptr elem_llvm raw_ptr idx64);
            (elem_ptr, elem_dt)
        | Pointer ->
            let raw_ptr = next_reg ctx in
            emit ctx (Printf.sprintf "  %s = load ptr, ptr %s, align 8" raw_ptr base_ptr);
            let elem_ptr = next_reg ctx in
            emit ctx (Printf.sprintf "  %s = getelementptr inbounds i8, ptr %s, i64 %s"
                       elem_ptr raw_ptr idx64);
            (elem_ptr, Byte)
        | _ ->
            raise (Error "Cannot index non-array, non-pointer expression")
      end else begin
        let pv, pt = codegen_expr ctx base_expr in
        if pt = "ptr" then begin
          let elem_ptr = next_reg ctx in
          emit ctx (Printf.sprintf "  %s = getelementptr inbounds i8, ptr %s, i64 %s"
                     elem_ptr pv idx64);
          (elem_ptr, Byte)
        end else
          raise (Error "Cannot index non-array, non-pointer expression")
      end

  | _ -> raise (Error "LHS evaluation expects an explicit identifier, field, or array element")

let rec codegen_stmt ctx = function

  | Dim (name, dt, opt_exp) ->
    let effective_dt =
      match dt, opt_exp with
      | Pointer, Some (Call (sn, _)) when Hashtbl.mem struct_fields sn -> Custom sn
      | Pointer, Some (StructInit (sn, _)) when Hashtbl.mem struct_fields sn -> Custom sn
      | _, _ -> dt
    in
    (match effective_dt with
    | Custom struct_name when Hashtbl.mem struct_fields struct_name ->
        Hashtbl.replace var_types name struct_name
    | _ -> ());

    (match effective_dt with
    | Array (_, -1) ->
        let alloca_reg = next_reg ctx in
        emit ctx (Printf.sprintf "  %s = alloca ptr, align 8" alloca_reg);
        Hashtbl.replace local_types name "ptr";
        Hashtbl.replace local_ast_types name effective_dt;
        Hashtbl.replace indirect_arrays name true;
        Hashtbl.replace ctx.variables name alloca_reg;
        (match opt_exp with
         | Some exp ->
             let v, t = codegen_expr ctx exp in
             emit_store_typed ctx Pointer v t alloca_reg
         | None ->
             emit ctx (Printf.sprintf "  store ptr null, ptr %s, align 8" alloca_reg))
    | Array (_, n) when n <= 0 ->
        raise (Error ("Array size must be greater than 0 for '" ^ name ^ "'"))
    | Array (elem_dt, n) ->
        let arr_llvm = string_of_dt effective_dt in
        let alloca_reg = next_reg ctx in
        emit ctx (Printf.sprintf "  %s = alloca %s, align 8" alloca_reg arr_llvm);
        emit ctx (Printf.sprintf "  store %s zeroinitializer, ptr %s, align 8" arr_llvm alloca_reg);
        Hashtbl.replace local_types name arr_llvm;
        Hashtbl.replace local_ast_types name effective_dt;
        Hashtbl.replace ctx.variables name alloca_reg;
        (match opt_exp with
         | None -> ()
         | Some (ArrayLit elems) ->
             if List.length elems > n then
               raise (Error (Printf.sprintf "Array initializer for '%s' has %d elements, exceeding size %d"
                               name (List.length elems) n));
             List.iteri (fun idx elem_expr ->
               let v, t = codegen_expr ctx elem_expr in
               let elem_ptr = next_reg ctx in
               emit ctx (Printf.sprintf "  %s = getelementptr inbounds %s, ptr %s, i64 0, i64 %d"
                          elem_ptr arr_llvm alloca_reg idx);
               emit_store_typed ctx elem_dt v t elem_ptr
             ) elems
         | Some exp ->
             let v, t = codegen_expr ctx exp in
             emit_store_typed ctx effective_dt v t alloca_reg)
    | _ ->
        let alloca_reg = next_reg ctx in
        let typ_str = string_of_dt effective_dt in
        emit ctx (Printf.sprintf "  %s = alloca %s, align 8" alloca_reg typ_str);
        Hashtbl.replace local_types name typ_str;
        Hashtbl.replace local_ast_types name effective_dt;
        Hashtbl.replace ctx.variables name alloca_reg;
        (match opt_exp with
         | Some exp ->
             let v, t = codegen_expr ctx exp in
             emit_store_typed ctx effective_dt v t alloca_reg
         | None -> ()))

  | StaticDim (name, dt, opt_exp) ->
      (match dt with
       | Array (_, n) when n <> -1 && n <= 0 ->
           raise (Error ("Array size must be greater than 0 for '" ^ name ^ "'"))
       | _ -> ());
      incr static_counter;
      let sym = Printf.sprintf "@__static_%s_%s_%d" !current_func_name name !static_counter in
      let llvm_dt = string_of_dt dt in
      let init_str = const_llvm_value ctx dt opt_exp in
      let decl = Printf.sprintf "%s = internal global %s %s, align 8" sym llvm_dt init_str in
      ctx.globals <- decl :: ctx.globals;
      Hashtbl.replace ctx.variables name sym;
      Hashtbl.replace local_types name llvm_dt;
      Hashtbl.replace local_ast_types name dt;
      (match dt with
       | Array (_, -1) -> Hashtbl.replace indirect_arrays name true
       | Custom sn when Hashtbl.mem struct_fields sn -> Hashtbl.replace var_types name sn
       | _ -> ())

  | Assign (name, exp) ->
      (match lookup_var ctx name with
       | Some (alloca_reg, expected_dt, _) ->
           let v, t = codegen_expr ctx exp in
           (match expected_dt with
            | Array (_, -1) -> emit_store_typed ctx Pointer v t alloca_reg
            | _ -> emit_store_typed ctx expected_dt v t alloca_reg)
       | None ->
           raise (Error ("Assignment target undefined: " ^ name)))

  | FieldAssign (base_expr, field_name, value_expr) ->
      let v, dt_str = codegen_expr ctx value_expr in
      let field_ptr, target_dt = get_lvalue_ptr ctx (FieldAccess(base_expr, field_name)) in
      emit_store_typed ctx target_dt v dt_str field_ptr

  | IndexAssign (base_expr, idx_expr, value_expr) ->
      let v, dt_str = codegen_expr ctx value_expr in
      let elem_ptr, elem_dt = get_lvalue_ptr ctx (IndexAccess(base_expr, idx_expr)) in
      emit_store_typed ctx elem_dt v dt_str elem_ptr

  | AssignExpr (lhs_expr, rhs_expr) ->
      let rhs_val, rhs_type = codegen_expr ctx rhs_expr in
      let target_ptr, target_dt = get_lvalue_ptr ctx lhs_expr in
      emit_store_typed ctx target_dt rhs_val rhs_type target_ptr



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
          Hashtbl.replace local_types var_name "i32";
          Hashtbl.replace local_ast_types var_name Int;
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

  | FuncDef (is_public, name, params, ret_type, body) ->
      (* Clear local registers and variable tables before processing each unique function body *)
      current_func_name := name;
      Hashtbl.clear ctx.variables;
      Hashtbl.clear local_types;
      Hashtbl.clear local_ast_types;
      Hashtbl.clear indirect_arrays;
      Hashtbl.clear var_types;
      ctx.reg_counter <- 1;

      (* 1. Structure function signature strings: System structs, arrays, or ByRef require a direct 'ptr' *)
      let param_strs = List.map (fun (mode, n, t) -> 
        llvm_param_type (mode, n, t) ^ " %" ^ n
      ) params in
      let params_joined = String.concat ", " param_strs in
      let ret_str = string_of_dt ret_type in
      
      (* Memorize ReturnType of Function -> ret_types *)
      Hashtbl.replace ret_types name ret_str;
    
      let linkage = if is_public then "" else "internal" in
      emit ctx (Printf.sprintf "define %s %s @%s(%s) {" linkage ret_str name params_joined);
      
      (* 2. Map function parameters to local stack variables based on ByRef / ByVal *)
      List.iter (fun (mode, n, dt) ->
        Hashtbl.replace local_ast_types n dt;
        match dt with
        | Custom struct_name when Hashtbl.mem struct_fields struct_name ->
            if mode = ByRef then begin
              (* ByRef Struct: Bind the argument register directly as the pointer, skip stack cloning *)
              Hashtbl.add ctx.variables n ("%" ^ n);
              Hashtbl.add var_types n struct_name;
              Hashtbl.add local_types n ("%struct." ^ struct_name)
            end else begin
              (* ByVal Struct: Create an isolated scratchpad slot and perform deep memory copy *)
              let alloca_reg = next_reg ctx in
              let struct_typ = "%struct." ^ struct_name in
              emit ctx (Printf.sprintf "  %s = alloca %s, align 8" alloca_reg struct_typ);
              let byte_size = get_struct_size struct_name in
              emit ctx (Printf.sprintf "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %s, ptr align 8 %%%s, i64 %d, i1 false)" 
                         alloca_reg n byte_size);
              Hashtbl.add ctx.variables n alloca_reg;
              Hashtbl.add var_types n struct_name;
              Hashtbl.add local_types n struct_typ
            end
        | Array (_, arr_len) ->
            let arr_typ = string_of_dt dt in
            if arr_len > 0 && mode = ByVal then begin
              (* ByVal Fixed-Size Array: isolate with stack copy *)
              let alloca_reg = next_reg ctx in
              emit ctx (Printf.sprintf "  %s = alloca %s, align 8" alloca_reg arr_typ);
              let byte_size = get_type_size dt in
              emit ctx (Printf.sprintf "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %s, ptr align 8 %%%s, i64 %d, i1 false)"
                         alloca_reg n byte_size);
              Hashtbl.add ctx.variables n alloca_reg;
              Hashtbl.add local_types n arr_typ
            end else begin
              (* ByRef Array or unsized slice arr[]: bind argument register directly *)
              Hashtbl.add ctx.variables n ("%" ^ n);
              Hashtbl.add local_types n arr_typ
            end
        | _ ->
            (* Standard Primitive Scalar Data Types *)
            let t_str = string_of_dt dt in
            if mode = ByRef then begin
              (* ByRef Primitive: Map variable name directly to incoming raw pointer register reference *)
              Hashtbl.add ctx.variables n ("%" ^ n);
              Hashtbl.add local_types n t_str
            end else begin
              (* ByVal Primitive: Store incoming value safely into an explicit alloca allocation slot *)
              let alloca_reg = next_reg ctx in
              emit ctx (Printf.sprintf "  %s = alloca %s, align 8" alloca_reg t_str);
              emit ctx (Printf.sprintf "  store %s %%%s, ptr %s, align 8" t_str n alloca_reg);
              Hashtbl.add ctx.variables n alloca_reg;
              Hashtbl.add local_types n t_str
            end
      ) params;
      
      List.iter (codegen_stmt ctx) body;
      
      (* Fallback protection for empty or branching void blocks *)
      if ret_type = Nothing then emit ctx "  ret void";
      emit ctx "}\n"

  | Structure (is_public, name, _)  -> Hashtbl.add visibility_table name is_public;
  | EnumDef (is_public, name, _)    -> Hashtbl.add visibility_table name is_public
  | GlobalVar (is_public, name, _, _) -> Hashtbl.replace visibility_table name is_public
  | ExternDecl _                    -> ()

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

  | GlobalVar (_, name, dt, _) ->
      (match dt with
       | Array (_, n) when n <> -1 && n <= 0 ->
           raise (Error ("Array size must be greater than 0 for '" ^ name ^ "'"))
       | Array (_, -1) -> Hashtbl.replace global_indirect_arrays name true
       | _ -> ());
      Hashtbl.replace global_ast_types name dt
      
  | FuncDef (_, name, params, ret_type, _) ->
      Hashtbl.replace ret_types name (string_of_dt ret_type);
      Hashtbl.replace func_param_modes name (List.map (fun (mode, _, _) -> mode) params)

  | ExternDecl (_, name, params, ret_type, is_variadic) ->
      let ret_str = string_of_dt ret_type in
      Hashtbl.replace ret_types name ret_str;
      Hashtbl.replace func_param_modes name (List.map (fun (mode, _, _) -> mode) params);
      let param_type_strs = List.map llvm_param_type params in
      if is_variadic then Hashtbl.replace variadic_funcs name param_type_strs;
      let sig_params =
        match param_type_strs, is_variadic with
        | [], true -> "..."
        | ps, true -> String.concat ", " ps ^ ", ..."
        | ps, false -> String.concat ", " ps
      in
      let decl_line = Printf.sprintf "declare %s @%s(%s)" ret_str name sig_params in
      if not (Hashtbl.mem extern_decls name) then
        extern_order := name :: !extern_order;
      Hashtbl.replace extern_decls name decl_line

let generate_program prog =
  let ctx = create_context () in
  Hashtbl.clear func_param_modes;
  Hashtbl.clear extern_decls;
  extern_order := [];
  Hashtbl.clear variadic_funcs;
  Hashtbl.clear visibility_table;
  Hashtbl.clear struct_fields;
  Hashtbl.clear struct_field_types;
  Hashtbl.clear var_types;
  Hashtbl.clear ret_types;
  Hashtbl.clear local_types;
  Hashtbl.clear local_ast_types;
  Hashtbl.clear indirect_arrays;
  Hashtbl.clear global_ast_types;
  Hashtbl.clear global_indirect_arrays;
  static_counter := 0;
  current_func_name := "";
  Hashtbl.clear enum_values;
  
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

  (* Step 1.5: Emit module-level Static global variables *)
  List.iter (function
    | GlobalVar (is_public, name, dt, opt_exp) ->
        let linkage = if is_public then "" else "internal " in
        let llvm_dt = string_of_dt dt in
        let init_str = const_llvm_value ctx dt opt_exp in
        let g_decl = Printf.sprintf "@%s = %sglobal %s %s, align 8" name linkage llvm_dt init_str in
        ctx.globals <- g_decl :: ctx.globals
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
  
  (* Standard C Library default declarations (skipped if overridden by user Declare) *)
  let builtin_externs = [
    ("printf",  "declare i32 @printf(ptr, ...)");
    ("malloc",  "declare ptr @malloc(i32)");
    ("calloc",  "declare ptr @calloc(i64, i64)");
    ("realloc", "declare ptr @realloc(ptr, i64)");
    ("free",    "declare void @free(ptr)");
    ("fopen",   "declare ptr @fopen(ptr, ptr)");
    ("getc",    "declare i32 @getc(ptr)");
    ("putc",    "declare i32 @putc(i32, ptr)");
  ] in
  List.iter (fun (fname, decl) ->
    if not (Hashtbl.mem extern_decls fname) then emit_header decl
  ) builtin_externs;
  (* User-declared external FFI declarations *)
  List.iter (fun fname ->
    emit_header (Hashtbl.find extern_decls fname)
  ) (List.rev !extern_order);
  emit_header "declare void @llvm.memcpy.p0.p0.i64(ptr nocapture writeonly, ptr nocapture readonly, i64, i1 immarg)";
  emit_header "";
  
  Buffer.contents header_buf ^ Buffer.contents ctx.buf
