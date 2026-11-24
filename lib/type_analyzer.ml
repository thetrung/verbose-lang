open Ast

exception TypeError of string

type func_sig = {
  params: (param_mode * data_type) list;
  ret: data_type;
  is_variadic: bool;
}

type env = {
  locals: (string, data_type) Hashtbl.t;
  globals: (string, data_type) Hashtbl.t;
  structs: (string, (string * data_type) list) Hashtbl.t;
  enums: (string, string list) Hashtbl.t;
  functions: (string, func_sig) Hashtbl.t;
  ret_type: data_type;
}

let create_env () = {
  locals = Hashtbl.create 50;
  globals = Hashtbl.create 32;
  structs = Hashtbl.create 16;
  enums = Hashtbl.create 16;
  functions = Hashtbl.create 32;
  ret_type = Nothing;
}

let copy_env ?ret_type env = {
  locals = Hashtbl.copy env.locals;
  globals = env.globals;
  structs = env.structs;
  enums = env.enums;
  functions = env.functions;
  ret_type = (match ret_type with Some rt -> rt | None -> env.ret_type);
}

let is_enum env = function
  | Custom name -> Hashtbl.mem env.enums name
  | _ -> false

let is_struct env = function
  | Custom name -> Hashtbl.mem env.structs name
  | _ -> false

let is_int_type env = function
  | Byte | Short | Int | Long -> true
  | Custom name -> Hashtbl.mem env.enums name
  | _ -> false

let is_float_type = function
  | Single | Double -> true
  | _ -> false

let normalize_type env = function
  | Custom name when Hashtbl.mem env.enums name -> Int
  | dt -> dt

let rec types_equal env t1 t2 =
  match normalize_type env t1, normalize_type env t2 with
  | Custom s1, Custom s2 -> s1 = s2
  | Array (e1, n1), Array (e2, n2) -> n1 = n2 && types_equal env e1 e2
  | a, b -> a = b

let is_lvalue = function
  | Id _ | FieldAccess _ | IndexAccess _ -> true
  | _ -> false

let lookup_var env name =
  match Hashtbl.find_opt env.locals name with
  | Some dt -> Some dt
  | None -> Hashtbl.find_opt env.globals name

let rec validate_type env = function
  | Custom name ->
      if not (Hashtbl.mem env.structs name || Hashtbl.mem env.enums name) then
        raise (TypeError ("Unknown type '" ^ name ^ "'"))
  | Array (et, n) ->
      if n <> -1 && n <= 0 then
        raise (TypeError "Array size must be greater than 0");
      validate_type env et
  | _ -> ()

let is_assignable env target_dt actual_dt rhs_expr =
  let t = normalize_type env target_dt in
  let a = normalize_type env actual_dt in
  if types_equal env t a then true
  else match t, a with
  | (Byte | Short | Int | Long), (Byte | Short | Int | Long) -> true
  | (Single | Double), (Single | Double) -> true
  | Pointer, Array (_, _) -> true
  | Array (_, -1), Pointer -> true
  | Array (et1, -1), Array (et2, _) -> types_equal env et1 et2
  | Pointer, (Byte | Short | Int | Long) -> true
  | Array (_, -1), _ -> (match rhs_expr with IntLit 0 -> true | _ -> false)
  | _ -> false

let rec is_const_expr env dt = function
  | None -> true
  | Some expr ->
      (match normalize_type env dt, expr with
       | Pointer, IntLit 0 -> true
       | Array (_, -1), IntLit 0 -> true
       | (Byte | Short | Int | Long), IntLit _ -> true
       | (Single | Double), FloatLit _ -> true
       | Boolean, BooleanLit _ -> true
       | Pointer, StringLit _ -> true
       | (Byte | Short | Int | Long), FieldAccess (Id ename, mname)
         when Hashtbl.mem env.enums ename ->
           List.mem mname (Hashtbl.find env.enums ename)
       | Array (elem_dt, n), ArrayLit elems when n > 0 ->
           if List.length elems > n then
             raise (TypeError (Printf.sprintf "Static array initializer has %d elements, exceeding size %d"
                                 (List.length elems) n));
           List.for_all (fun e -> is_const_expr env elem_dt (Some e)) elems
       | Custom sn, StructInit (init_sn, inits) when Hashtbl.mem env.structs sn ->
           if sn <> init_sn then
             raise (TypeError (Printf.sprintf "Static struct type mismatch: expected '%s', got '%s'" sn init_sn));
           let fields = Hashtbl.find env.structs sn in
           let seen = Hashtbl.create (List.length inits) in
           List.iter (fun (fname, fexpr) ->
             if Hashtbl.mem seen fname then
               raise (TypeError (Printf.sprintf "Duplicate field '%s' in '%s' With initializer" fname sn));
             if not (List.mem_assoc fname fields) then
               raise (TypeError (Printf.sprintf "Structure '%s' has no field named '%s'" sn fname));
             Hashtbl.add seen fname ();
             let fdt = List.assoc fname fields in
             if not (is_const_expr env fdt (Some fexpr)) then
               raise (TypeError "Static variable initializer must be a compile-time constant")
           ) inits;
           true
       | _ -> false)

let rec check_arena_arg env fn_name mode arg_expr =
  if mode <> ByRef then
    raise (TypeError (fn_name ^ " expects ByRef arena"));
  if not (is_lvalue arg_expr) then
    raise (TypeError "ByRef argument must be a variable, struct field, or array element");
  let dt = type_of_expr env arg_expr in
  if dt <> Arena then
    raise (TypeError (fn_name ^ " argument must be an Arena variable"))

and type_of_expr env = function
  | Id name ->
      (match lookup_var env name with
       | Some dt -> dt
       | None ->
           if Hashtbl.mem env.enums name then Custom name
           else raise (TypeError ("Undefined variable or constant reference: " ^ name)))

  | IntLit _ -> Int
  | FloatLit _ -> Double
  | BooleanLit _ -> Boolean
  | StringLit _ -> Pointer

  | UnaryOp (op, e) ->
      let t = type_of_expr env e in
      if op = "Not" && (t = Boolean || is_int_type env t) then t
      else raise (TypeError ("Invalid unary operation '" ^ op ^ "' on type " ^ Printer.string_of_dt t))

  | BinOp (e1, op, e2) ->
      let t1 = normalize_type env (type_of_expr env e1) in
      let t2 = normalize_type env (type_of_expr env e2) in
      if not (types_equal env t1 t2) then
        raise (TypeError (Printf.sprintf "Type mismatch in binary operation: %s vs %s"
                            (Printer.string_of_dt t1) (Printer.string_of_dt t2)));
      if is_float_type t1 then begin
        match op with
        | Add | Sub | Mul | Div | Mod -> t1
        | Equal | Greater | Less | GreaterEqual | LessEqual | NotEqual -> Boolean
        | _ -> raise (TypeError "Unsupported bitwise operation on floating point values")
      end else begin
        match op with
        | Equal | Greater | Less | GreaterEqual | LessEqual | NotEqual -> Boolean
        | _ -> t1
      end

  | FieldAccess (base_expr, field_name) ->
      (match base_expr with
       | Id enum_name when Hashtbl.mem env.enums enum_name ->
           let variants = Hashtbl.find env.enums enum_name in
           if List.mem field_name variants then Int
           else raise (TypeError ("Enum '" ^ enum_name ^ "' has no member variant: " ^ field_name))
       | _ ->
           let base_type = type_of_expr env base_expr in
           (match base_type with
            | Custom s_name when Hashtbl.mem env.structs s_name ->
                let fields = Hashtbl.find env.structs s_name in
                (try List.assoc field_name fields
                 with Not_found ->
                   raise (TypeError (Printf.sprintf "Structure '%s' has no field named '%s'" s_name field_name)))
            | _ ->
                let details = Printer.string_of_expr base_expr ^ "." ^ field_name in
                raise (TypeError ("Field access requires a structure value before ." ^ field_name ^ " (" ^ details ^ ")"))))

  | IndexAccess (base_expr, idx_expr) ->
      let idx_dt = type_of_expr env idx_expr in
      if not (is_int_type env idx_dt) then
        raise (TypeError ("Array index must be an integer type, got " ^ Printer.string_of_dt idx_dt));
      (match type_of_expr env base_expr with
       | Array (elem_dt, _) -> elem_dt
       | Pointer -> Byte
       | _ -> raise (TypeError "Cannot index non-array, non-pointer expression"))

  | ArrayLit elems ->
      (match elems with
       | [] -> Array (Int, 0)
       | first :: rest ->
           let et = type_of_expr env first in
           List.iter (fun e -> ignore (type_of_expr env e)) rest;
           Array (et, List.length elems))

  | StructInit (name, inits) ->
      if not (Hashtbl.mem env.structs name) then
        raise (TypeError ("Unknown structure in With initializer: " ^ name));
      let fields = Hashtbl.find env.structs name in
      let seen = Hashtbl.create (List.length inits) in
      List.iter (fun (fname, fexpr) ->
        if Hashtbl.mem seen fname then
          raise (TypeError (Printf.sprintf "Duplicate field '%s' in '%s' With initializer" fname name));
        Hashtbl.add seen fname ();
        let field_dt =
          try List.assoc fname fields
          with Not_found ->
            raise (TypeError (Printf.sprintf "Structure '%s' has no field named '%s'" name fname))
        in
        let val_dt = type_of_expr env fexpr in
        if not (is_assignable env field_dt val_dt fexpr) then
          raise (TypeError (Printf.sprintf "Type mismatch for field '%s' in '%s' With initializer: expected %s, got %s"
                              fname name (Printer.string_of_dt field_dt) (Printer.string_of_dt val_dt)))
      ) inits;
      Custom name

  | SizeOf dt ->
      validate_type env dt;
      Int

  | HeapAlloc (dt, count_expr) ->
      validate_type env dt;
      if dt = Nothing then
        raise (TypeError "HeapAlloc requires a non-empty element type");
      let count_dt = type_of_expr env count_expr in
      if not (is_int_type env count_dt) then
        raise (TypeError ("HeapAlloc count must be an integer type, got " ^ Printer.string_of_dt count_dt));
      Array (dt, -1)

  | HeapRealloc (ptr_expr, dt, count_expr) ->
      validate_type env dt;
      if dt = Nothing then
        raise (TypeError "HeapRealloc requires a non-empty element type");
      let pt = type_of_expr env ptr_expr in
      (match pt with
       | Pointer | Array (_, _) -> ()
       | _ -> raise (TypeError ("HeapRealloc expects a Pointer or heap array, got " ^ Printer.string_of_dt pt)));
      let count_dt = type_of_expr env count_expr in
      if not (is_int_type env count_dt) then
        raise (TypeError ("HeapRealloc count must be an integer type, got " ^ Printer.string_of_dt count_dt));
      Array (dt, -1)

  | ArenaAlloc (mode, arena_expr, dt, count_expr) ->
      if mode <> ByRef then
        raise (TypeError "ArenaAlloc expects ByRef arena as argument 1");
      if not (is_lvalue arena_expr) then
        raise (TypeError "ByRef argument must be a variable, struct field, or array element");
      let adt = type_of_expr env arena_expr in
      if adt <> Arena then
        raise (TypeError "ArenaAlloc argument 1 must be an Arena variable");
      validate_type env dt;
      if dt = Nothing then
        raise (TypeError "ArenaAlloc requires a non-empty element type");
      let count_dt = type_of_expr env count_expr in
      if not (is_int_type env count_dt) then
        raise (TypeError ("ArenaAlloc count must be an integer type, got " ^ Printer.string_of_dt count_dt));
      Array (dt, -1)

  | Call ("IsNull", [(ByVal, ptr_expr)]) ->
      let pt = type_of_expr env ptr_expr in
      (match pt with
       | Pointer | Array (_, _) -> Boolean
       | _ -> raise (TypeError ("IsNull expects a Pointer or slice argument, got " ^ Printer.string_of_dt pt)))
  | Call ("IsNull", _) ->
      raise (TypeError "IsNull() expects exactly one ByVal pointer/slice argument")

  | Call ("ArenaCreate", [(ByVal, cap_expr)]) ->
      let ct = type_of_expr env cap_expr in
      if not (is_int_type env ct) then
        raise (TypeError ("ArenaCreate capacity must be an integer type, got " ^ Printer.string_of_dt ct));
      Arena
  | Call ("ArenaCreate", _) ->
      raise (TypeError "ArenaCreate() expects exactly one ByVal capacity argument")

  | Call ("ArenaFromBuffer", [(buf_mode, buf_expr); (ByVal, cap_expr)]) ->
      if buf_mode <> ByRef then
        raise (TypeError "ArenaFromBuffer expects ByRef buffer as argument 1");
      if not (is_lvalue buf_expr) then
        raise (TypeError "ByRef argument must be a variable, struct field, or array element");
      ignore (type_of_expr env buf_expr);
      let ct = type_of_expr env cap_expr in
      if not (is_int_type env ct) then
        raise (TypeError ("ArenaFromBuffer capacity must be an integer type, got " ^ Printer.string_of_dt ct));
      Arena
  | Call ("ArenaFromBuffer", _) ->
      raise (TypeError "ArenaFromBuffer() expects (ByRef buffer, capacity)")

  | Call ("ArenaAllocBytes", [(a_mode, a_expr); (ByVal, bytes_expr)]) ->
      if a_mode <> ByRef then
        raise (TypeError "ArenaAllocBytes expects ByRef arena as argument 1");
      if not (is_lvalue a_expr) then
        raise (TypeError "ByRef argument must be a variable, struct field, or array element");
      if type_of_expr env a_expr <> Arena then
        raise (TypeError "ArenaAllocBytes argument 1 must be an Arena variable");
      let bt = type_of_expr env bytes_expr in
      if not (is_int_type env bt) then
        raise (TypeError ("ArenaAllocBytes size must be an integer type, got " ^ Printer.string_of_dt bt));
      Pointer
  | Call ("ArenaAllocBytes", _) ->
      raise (TypeError "ArenaAllocBytes() expects (ByRef arena, byteCount)")

  | Call ("ArenaReset", [(a_mode, a_expr)]) ->
      check_arena_arg env "ArenaReset" a_mode a_expr;
      Nothing
  | Call ("ArenaReset", _) ->
      raise (TypeError "ArenaReset() expects (ByRef arena)")

  | Call ("ArenaMark", [(a_mode, a_expr)]) ->
      check_arena_arg env "ArenaMark" a_mode a_expr;
      Long
  | Call ("ArenaMark", _) ->
      raise (TypeError "ArenaMark() expects (ByRef arena)")

  | Call ("ArenaRewind", [(a_mode, a_expr); (ByVal, mark_expr)]) ->
      if a_mode <> ByRef then raise (TypeError "ArenaRewind expects ByRef arena");
      if not (is_lvalue a_expr) then
        raise (TypeError "ByRef argument must be a variable, struct field, or array element");
      if type_of_expr env a_expr <> Arena then
        raise (TypeError "ArenaRewind argument 1 must be an Arena variable");
      let mt = type_of_expr env mark_expr in
      if not (is_int_type env mt) then
        raise (TypeError ("ArenaRewind mark must be an integer type, got " ^ Printer.string_of_dt mt));
      Nothing
  | Call ("ArenaRewind", _) ->
      raise (TypeError "ArenaRewind() expects (ByRef arena, mark)")

  | Call ("ArenaUsed", [(a_mode, a_expr)]) ->
      check_arena_arg env "ArenaUsed" a_mode a_expr;
      Long
  | Call ("ArenaUsed", _) ->
      raise (TypeError "ArenaUsed() expects (ByRef arena)")

  | Call ("ArenaRemaining", [(a_mode, a_expr)]) ->
      check_arena_arg env "ArenaRemaining" a_mode a_expr;
      Long
  | Call ("ArenaRemaining", _) ->
      raise (TypeError "ArenaRemaining() expects (ByRef arena)")

  | Call ("ArenaCapacity", [(a_mode, a_expr)]) ->
      check_arena_arg env "ArenaCapacity" a_mode a_expr;
      Long
  | Call ("ArenaCapacity", _) ->
      raise (TypeError "ArenaCapacity() expects (ByRef arena)")

  | Call ("ArenaDestroy", [(a_mode, a_expr)]) ->
      check_arena_arg env "ArenaDestroy" a_mode a_expr;
      Nothing
  | Call ("ArenaDestroy", _) ->
      raise (TypeError "ArenaDestroy() expects (ByRef arena)")

  | Call ("HeapAllocBytes", [(ByVal, size_expr)]) ->
      let st = type_of_expr env size_expr in
      if not (is_int_type env st) then
        raise (TypeError ("HeapAllocBytes size must be an integer type, got " ^ Printer.string_of_dt st));
      Pointer
  | Call ("HeapAllocBytes", _) ->
      raise (TypeError "HeapAllocBytes() expects exactly one ByVal byte-count argument")

  | Call ("HeapFree", [(ByVal, ptr_expr)]) ->
      let pt = type_of_expr env ptr_expr in
      (match pt with
       | Pointer | Array (_, _) -> Nothing
       | _ -> raise (TypeError ("HeapFree expects a Pointer or heap array, got " ^ Printer.string_of_dt pt)))
  | Call ("HeapFree", _) ->
      raise (TypeError "HeapFree() expects exactly one ByVal pointer/array argument")

  | Call ("Len", [(ByVal, arg_expr)]) ->
      if not (is_lvalue arg_expr) then
        raise (TypeError "Len() requires a fixed-size array");
      (match type_of_expr env arg_expr with
       | Array (_, n) when n > 0 -> Int
       | _ -> raise (TypeError "Len() requires a fixed-size array"))
  | Call ("Len", _) ->
      raise (TypeError "Len() expects exactly one ByVal fixed-size array argument")

  | Call ("printf", args) ->
      List.iter (fun (mode, e) ->
        if mode = ByRef then raise (TypeError "printf does not accept ByRef arguments");
        ignore (type_of_expr env e)
      ) args;
      Int

  | Call (name, args) ->
      if Hashtbl.mem env.structs name then begin
        let fields = Hashtbl.find env.structs name in
        if List.length args > List.length fields then
          raise (TypeError (Printf.sprintf "Too many arguments to struct constructor '%s': expected at most %d, got %d"
                              name (List.length fields) (List.length args)));
        List.iteri (fun idx (arg_mode, arg_expr) ->
          if arg_mode = ByRef then
            raise (TypeError ("Struct constructor '" ^ name ^ "' does not accept ByRef arguments"));
          let (fname, fdt) = List.nth fields idx in
          let actual_dt = type_of_expr env arg_expr in
          if not (is_assignable env fdt actual_dt arg_expr) then
            raise (TypeError (Printf.sprintf "Type mismatch for field '%s' in '%s' constructor: expected %s, got %s"
                                fname name (Printer.string_of_dt fdt) (Printer.string_of_dt actual_dt)))
        ) args;
        Custom name
      end else if Hashtbl.mem env.functions name then begin
        let sig_ = Hashtbl.find env.functions name in
        let expected_n = List.length sig_.params in
        let got_n = List.length args in
        if (not sig_.is_variadic && got_n <> expected_n) || (sig_.is_variadic && got_n < expected_n) then
          raise (TypeError (Printf.sprintf "Function '%s' expects %s%d argument(s), got %d"
                              name (if sig_.is_variadic then "at least " else "") expected_n got_n));
        List.iteri (fun idx (arg_mode, arg_expr) ->
          let actual_dt = type_of_expr env arg_expr in
          match List.nth_opt sig_.params idx with
          | Some (ByRef, param_dt) ->
              if arg_mode <> ByRef then
                raise (TypeError (Printf.sprintf "Argument %d of '%s' expects ByRef at call site" (idx + 1) name));
              if not (is_lvalue arg_expr) then
                raise (TypeError "ByRef argument must be a variable, struct field, or array element");
              if not (is_assignable env param_dt actual_dt arg_expr) then
                raise (TypeError (Printf.sprintf "Argument %d of '%s' type mismatch: expected %s, got %s"
                                    (idx + 1) name (Printer.string_of_dt param_dt) (Printer.string_of_dt actual_dt)))
          | Some (ByVal, param_dt) ->
              if arg_mode = ByRef then
                raise (TypeError (Printf.sprintf "Argument %d of '%s' is ByVal and cannot be passed ByRef" (idx + 1) name));
              if not (is_assignable env param_dt actual_dt arg_expr) then
                raise (TypeError (Printf.sprintf "Argument %d of '%s' type mismatch: expected %s, got %s"
                                    (idx + 1) name (Printer.string_of_dt param_dt) (Printer.string_of_dt actual_dt)))
          | None ->
              if sig_.is_variadic && arg_mode = ByRef then begin
                if not (is_lvalue arg_expr) then
                  raise (TypeError "ByRef argument must be a variable, struct field, or array element")
              end else if arg_mode = ByRef then
                raise (TypeError (Printf.sprintf "Argument %d of '%s' is ByVal and cannot be passed ByRef" (idx + 1) name))
        ) args;
        sig_.ret
      end else begin
        (* Check built-in C runtime defaults *)
        match name with
        | "malloc" | "fopen" | "calloc" | "realloc" ->
            List.iter (fun (m, e) ->
              if m = ByRef then raise (TypeError (name ^ " does not accept ByRef arguments"));
              ignore (type_of_expr env e)
            ) args;
            Pointer
        | "free" ->
            List.iter (fun (m, e) ->
              if m = ByRef then raise (TypeError "free does not accept ByRef arguments");
              ignore (type_of_expr env e)
            ) args;
            Nothing
        | "getc" | "putc" ->
            List.iter (fun (m, e) ->
              if m = ByRef then raise (TypeError (name ^ " does not accept ByRef arguments"));
              ignore (type_of_expr env e)
            ) args;
            Int
        | _ ->
            raise (TypeError ("Undefined function or constructor: " ^ name))
      end

let rec check_stmt env = function
  | Dim (name, expected_dt, opt_expr) ->
      if Hashtbl.mem env.locals name then
        raise (TypeError ("Variable '" ^ name ^ "' is already declared"));
      let effective_dt =
        match expected_dt, opt_expr with
        | Pointer, Some (Call (sn, _)) when Hashtbl.mem env.structs sn -> Custom sn
        | Pointer, Some (StructInit (sn, _)) when Hashtbl.mem env.structs sn -> Custom sn
        | _, _ -> expected_dt
      in
      (match effective_dt with
       | Array (_, n) when n <> -1 && n <= 0 ->
           raise (TypeError ("Array size must be greater than 0 for '" ^ name ^ "'"))
       | _ -> ());
      validate_type env effective_dt;
      (match opt_expr with
       | Some (ArrayLit elems) ->
           (match effective_dt with
            | Array (elem_dt, n) when n > 0 ->
                if List.length elems > n then
                  raise (TypeError (Printf.sprintf "Array initializer for '%s' has %d elements, exceeding size %d"
                                      name (List.length elems) n));
                List.iter (fun e ->
                  let edt = type_of_expr env e in
                  if not (is_assignable env elem_dt edt e) then
                    raise (TypeError (Printf.sprintf "Array element type mismatch for '%s': expected %s, got %s"
                                        name (Printer.string_of_dt elem_dt) (Printer.string_of_dt edt)))
                ) elems
            | _ ->
                raise (TypeError ("Array literal can only initialize a fixed-size array for '" ^ name ^ "'")))
       | Some init_expr ->
           let actual_dt = type_of_expr env init_expr in
           if not (is_assignable env effective_dt actual_dt init_expr) then
             raise (TypeError (Printf.sprintf "Variable declaration assignment mismatch for '%s': expected %s, got %s"
                                 name (Printer.string_of_dt effective_dt) (Printer.string_of_dt actual_dt)))
       | None -> ());
      Hashtbl.replace env.locals name effective_dt

  | StaticDim (name, expected_dt, opt_expr) ->
      if Hashtbl.mem env.locals name then
        raise (TypeError ("Variable '" ^ name ^ "' is already declared"));
      (match expected_dt with
       | Array (_, n) when n <> -1 && n <= 0 ->
           raise (TypeError ("Array size must be greater than 0 for '" ^ name ^ "'"))
       | _ -> ());
      validate_type env expected_dt;
      (match opt_expr with
       | Some e ->
           ignore (type_of_expr env e);
           if not (is_const_expr env expected_dt opt_expr) then
             raise (TypeError "Static variable initializer must be a compile-time constant")
       | None -> ());
      Hashtbl.replace env.locals name expected_dt

  | Assign (name, expr) ->
      let target_dt =
        match lookup_var env name with
        | Some dt -> dt
        | None -> raise (TypeError ("Assignment target variable '" ^ name ^ "' is undefined"))
      in
      let value_dt = type_of_expr env expr in
      if not (is_assignable env target_dt value_dt expr) then
        raise (TypeError (Printf.sprintf "Type mismatch assignment on variable '%s': expected %s, got %s"
                            name (Printer.string_of_dt target_dt) (Printer.string_of_dt value_dt)))

  | FieldAssign (base_expr, field_name, value_expr) ->
      let target_dt = type_of_expr env (FieldAccess (base_expr, field_name)) in
      let value_dt = type_of_expr env value_expr in
      if not (is_assignable env target_dt value_dt value_expr) then
        raise (TypeError (Printf.sprintf "Type assignment mismatch inside field '%s': expected %s, got %s"
                            field_name (Printer.string_of_dt target_dt) (Printer.string_of_dt value_dt)))

  | IndexAssign (base_expr, idx_expr, value_expr) ->
      let elem_dt = type_of_expr env (IndexAccess (base_expr, idx_expr)) in
      let value_dt = type_of_expr env value_expr in
      if not (is_assignable env elem_dt value_dt value_expr) then
        raise (TypeError (Printf.sprintf "Type assignment mismatch in array index assignment: expected %s, got %s"
                            (Printer.string_of_dt elem_dt) (Printer.string_of_dt value_dt)))

  | AssignExpr (lhs_expr, rhs_expr) ->
      let lhs_dt = type_of_expr env lhs_expr in
      let rhs_dt = type_of_expr env rhs_expr in
      if not (is_assignable env lhs_dt rhs_dt rhs_expr) then
        raise (TypeError "Invalid type payload target assignment path")

  | ExprStatement expr ->
      ignore (type_of_expr env expr)

  | Return None ->
      if env.ret_type <> Nothing then
        raise (TypeError ("Function must return a value of type " ^ Printer.string_of_dt env.ret_type))

  | Return (Some e) ->
      let actual_dt = type_of_expr env e in
      if env.ret_type = Nothing then
        raise (TypeError "Sub cannot return a value");
      if not (is_assignable env env.ret_type actual_dt e) then
        raise (TypeError (Printf.sprintf "Return type mismatch: expected %s, got %s"
                            (Printer.string_of_dt env.ret_type) (Printer.string_of_dt actual_dt)))

  | If (cond, then_block, else_block) ->
      let cond_dt = type_of_expr env cond in
      if cond_dt <> Boolean && not (is_int_type env cond_dt) then
        raise (TypeError ("If condition must be Boolean, got " ^ Printer.string_of_dt cond_dt));
      List.iter (check_stmt (copy_env env)) then_block;
      List.iter (check_stmt (copy_env env)) else_block

  | While (cond, body) ->
      let cond_dt = type_of_expr env cond in
      if cond_dt <> Boolean && not (is_int_type env cond_dt) then
        raise (TypeError ("While condition must be Boolean, got " ^ Printer.string_of_dt cond_dt));
      List.iter (check_stmt (copy_env env)) body

  | For (var_name, start_e, finish_e, body) ->
      let st = type_of_expr env start_e in
      let ft = type_of_expr env finish_e in
      if not (is_int_type env st) || not (is_int_type env ft) then
        raise (TypeError "For loop bounds must be integer types");
      let nested = copy_env env in
      Hashtbl.replace nested.locals var_name Int;
      List.iter (check_stmt nested) body

  | SelectCase (target, branches, default_opt) ->
      let target_dt = normalize_type env (type_of_expr env target) in
      List.iter (fun (case_exprs, body) ->
        List.iter (fun ce ->
          let cdt = normalize_type env (type_of_expr env ce) in
          if not (is_assignable env target_dt cdt ce) then
            raise (TypeError (Printf.sprintf "Select Case value type mismatch: expected %s, got %s"
                                (Printer.string_of_dt target_dt) (Printer.string_of_dt cdt)))
        ) case_exprs;
        List.iter (check_stmt (copy_env env)) body
      ) branches;
      (match default_opt with
       | Some body -> List.iter (check_stmt (copy_env env)) body
       | None -> ())

let analyze_program prog =
  let env = create_env () in
  (* Pass 1: register all structs, enums, globals, functions, and externs *)
  List.iter (function
    | Structure (_, name, fields) ->
        Hashtbl.replace env.structs name fields
    | EnumDef (_, name, members) ->
        Hashtbl.replace env.enums name (List.map fst members)
    | GlobalVar (_, name, dt, _) ->
        (match dt with
         | Array (_, n) when n <> -1 && n <= 0 ->
             raise (TypeError ("Array size must be greater than 0 for '" ^ name ^ "'"))
         | _ -> ());
        Hashtbl.replace env.globals name dt
    | FuncDef (_, name, params, ret_type, _) ->
        let param_specs = List.map (fun (m, _, t) -> (m, t)) params in
        Hashtbl.replace env.functions name { params = param_specs; ret = ret_type; is_variadic = false }
    | ExternDecl (_, name, params, ret_type, is_variadic) ->
        let param_specs = List.map (fun (m, _, t) -> (m, t)) params in
        Hashtbl.replace env.functions name { params = param_specs; ret = ret_type; is_variadic }
  ) prog;

  (* Pass 2: validate struct field types, global types/initializers, and function bodies *)
  List.iter (function
    | Structure (_, _, fields) ->
        List.iter (fun (_, fdt) -> validate_type env fdt) fields
    | GlobalVar (_, name, dt, opt_e) ->
        validate_type env dt;
        (match opt_e with
         | Some e ->
             (match dt, e with
              | Array (elem_dt, n), ArrayLit elems when n > 0 ->
                  if List.length elems > n then
                    raise (TypeError (Printf.sprintf "Static array initializer has %d elements, exceeding size %d"
                                        (List.length elems) n));
                  List.iter (fun el ->
                    let edt = type_of_expr env el in
                    if not (is_assignable env elem_dt edt el) then
                      raise (TypeError ("Static array element type mismatch for '" ^ name ^ "'"))
                  ) elems
              | _ ->
                  let actual_dt = type_of_expr env e in
                  if not (is_assignable env dt actual_dt e) then
                    raise (TypeError ("Static global initializer type mismatch for '" ^ name ^ "'")));
             if not (is_const_expr env dt opt_e) then
               raise (TypeError "Static variable initializer must be a compile-time constant")
         | None -> ())
    | FuncDef (_, _, params, ret_type, body) ->
        validate_type env ret_type;
        let func_env = copy_env ~ret_type env in
        List.iter (fun (_, pname, pdt) ->
          validate_type env pdt;
          Hashtbl.replace func_env.locals pname pdt
        ) params;
        List.iter (check_stmt func_env) body
    | ExternDecl (_, _, params, ret_type, _) ->
        validate_type env ret_type;
        List.iter (fun (_, _, pdt) -> validate_type env pdt) params
    | EnumDef _ -> ()
  ) prog
