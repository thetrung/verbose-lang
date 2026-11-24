open Ast
(* Pretty Print  *)

let string_of_pmode = function 
  | ByRef -> "ByRef"
  | ByVal -> "ByVal"

let rec string_of_dt = function
  | Int -> "Integer" 
  | Byte -> "Byte" 
  | Short -> "Short"   
  | Long -> "Long"
  | Single -> "Single"
  | Double -> "Double"
  | Boolean -> "Boolean"
  | Nothing -> "Nothing" 
  | Pointer -> "Pointer"
  | Arena -> "Arena"
  | Custom s -> s
  | Array (et, n) ->
      if n > 0 then Printf.sprintf "%s(%d)" (string_of_dt et) n
      else string_of_dt et ^ "()"

let string_of_op = function
  | Add -> "+" | Sub -> "-" | Mul -> "*" | Div -> "/" | Mod -> "Mod"
  | And -> "And" | Or -> "Or" | Xor -> "Xor" | Shl -> "Shl" | Shr -> "Shr"
  | Equal -> "=" | Greater -> ">" | Less -> "<" | GreaterEqual -> ">=" | LessEqual -> "<=" | NotEqual -> "<>"

let rec string_of_expr = function
  | Id id -> id
  | IntLit i -> string_of_int i
  | FloatLit f -> string_of_float f 
  | BooleanLit b -> string_of_bool b 
  | StringLit s -> (Printf.sprintf "%S" s)
  | UnaryOp (op, e) -> "(" ^ op ^ " " ^ string_of_expr e ^ ")"
  | BinOp (e1, op, e2) -> "(" ^ string_of_expr e1 ^ " " ^ string_of_op op ^ " " ^ string_of_expr e2 ^ ")"
  | Call (f, args) ->
      let fmt_arg (mode, e) =
        match mode with
        | ByRef -> "ByRef " ^ string_of_expr e
        | ByVal -> string_of_expr e
      in
      f ^ "(" ^ String.concat ", " (List.map fmt_arg args) ^ ")"
  | FieldAccess (e, field) -> string_of_expr e ^ "." ^ field
  | IndexAccess (e, idx) -> string_of_expr e ^ "(" ^ string_of_expr idx ^ ")"
  | ArrayLit elems -> "{" ^ String.concat ", " (List.map string_of_expr elems) ^ "}"
  | StructInit (name, inits) ->
      let fmt_init (f, e) = f ^ " = " ^ string_of_expr e in
      name ^ " With ( " ^ String.concat ", " (List.map fmt_init inits) ^ " )"
  | SizeOf dt -> "SizeOf(" ^ string_of_dt dt ^ ")"
  | HeapAlloc (dt, count) -> "HeapAlloc(" ^ string_of_dt dt ^ ", " ^ string_of_expr count ^ ")"
  | HeapRealloc (p, dt, count) -> "HeapRealloc(" ^ string_of_expr p ^ ", " ^ string_of_dt dt ^ ", " ^ string_of_expr count ^ ")"
  | ArenaAlloc (mode, a, dt, count) ->
      let ap = match mode with ByRef -> "ByRef " ^ string_of_expr a | ByVal -> string_of_expr a in
      "ArenaAlloc(" ^ ap ^ ", " ^ string_of_dt dt ^ ", " ^ string_of_expr count ^ ")"

let rec string_of_stmt indent = function
  | Dim (v, dt, opt_e) -> 
      let init_expr = match opt_e with | Some e -> " = " ^ string_of_expr e | None -> "" in
      indent ^ "Dim " ^ v ^ " As " ^ string_of_dt dt ^ init_expr ^ "\n"
  | StaticDim (v, dt, opt_e) ->
      let init_expr = match opt_e with | Some e -> " = " ^ string_of_expr e | None -> "" in
      indent ^ "Static " ^ v ^ " As " ^ string_of_dt dt ^ init_expr ^ "\n"
  | Assign (v, e) -> indent ^ v ^ " = " ^ string_of_expr e ^ "\n"
  | FieldAssign (obj, field, e) -> indent ^ string_of_expr obj ^ "." ^ field ^ " = " ^ string_of_expr e ^ "\n"
  | IndexAssign (base, idx, e) -> indent ^ string_of_expr base ^ "[" ^ string_of_expr idx ^ "] = " ^ string_of_expr e ^ "\n"
  | AssignExpr (lhs, rhs) -> indent ^ string_of_expr lhs ^ " = " ^ string_of_expr rhs ^ "\n"
  | ExprStatement e -> indent ^ string_of_expr e ^ "\n"
  | Return None -> indent ^ "Return\n"
  | Return (Some e) -> indent ^ "Return " ^ string_of_expr e ^ "\n"
  | While (c, body) -> indent ^ "While " ^ string_of_expr c ^ "\n" ^ string_of_block (indent ^ "  ") body ^ indent ^ "End While\n"
  | For (v, s, f, body) -> indent ^ "For " ^ v ^ " = " ^ string_of_expr s ^ " To " ^ string_of_expr f ^ "\n" ^ string_of_block (indent ^ "  ") body ^ indent ^ "End For\n"
  | If (c, t, e) -> 
      let els_str = if e = [] then "" else indent ^ "Else\n" ^ string_of_block (indent ^ "  ") e in
      indent ^ "If " ^ string_of_expr c ^ " Then\n" ^ string_of_block (indent ^ "  ") t ^ els_str ^ indent ^ "End If\n"
  (* --- UPDATED: Select Case with Case Else Fallback Printing --- *)
  | SelectCase (e, cases, default_opt) ->
      let print_case (el, b) = 
        indent ^ "  Case " ^ String.concat ", " (List.map string_of_expr el) ^ "\n" ^ string_of_block (indent ^ "    ") b 
      in
      let default_str = match default_opt with
        | Some b -> indent ^ "  Case Else\n" ^ string_of_block (indent ^ "    ") b
        | None -> ""
      in
      indent ^ "Select Case " ^ string_of_expr e ^ "\n" ^ 
      String.concat "" (List.map print_case cases) ^ 
      default_str ^ 
      indent ^ "End Select\n"and string_of_block indent stmts = String.concat "" (List.map (string_of_stmt indent) stmts)

let string_of_vis = function
  | Public -> "Public "
  | Private -> "Private "
  | Default -> ""

let string_of_def = function
  | Include path -> Printf.sprintf "Include %S\n" path
  | Structure (vis, name, fields) ->
      let prefix = string_of_vis vis in
      let fl = String.concat "" (List.map (fun (n, t) -> "  Dim " ^ n ^ " As " ^ string_of_dt t ^ "\n") fields) in
      prefix ^ "Structure " ^ name ^ "\n" ^ fl ^ "End Structure\n"
  | EnumDef (vis, name, members) ->
      let prefix = string_of_vis vis in
      let ml = String.concat "" (List.map (fun (n, v) -> "  " ^ n ^ " = " ^ string_of_int v ^ "\n") members) in
      prefix ^ "Enum " ^ name ^ "\n" ^ ml ^ "End Enum\n"
  | GlobalVar (vis, name, dt, opt_e) ->
      let prefix = string_of_vis vis in
      let init_expr = match opt_e with | Some e -> " = " ^ string_of_expr e | None -> "" in
      prefix ^ "Static " ^ name ^ " As " ^ string_of_dt dt ^ init_expr ^ "\n"
  | FuncDef (vis, name, params, rt, body) ->
      let prefix = string_of_vis vis in
      let keyword = if rt = Nothing then "Sub " else "Function " in
      let as_clause = if rt = Nothing then "" else " As " ^ string_of_dt rt in
      let pl = String.concat ", " (List.map (fun (m, n, t) -> string_of_pmode m ^ " " ^ n ^ " As " ^ string_of_dt t) params) in
      prefix ^ keyword ^ name ^ "(" ^ pl ^ ")" ^ as_clause ^ "\n" ^ string_of_block "  " body ^ "End " ^ keyword ^ "\n"
  | ExternDecl (vis, name, params, rt, is_variadic) ->
      let prefix = string_of_vis vis in
      let keyword = if rt = Nothing then "Sub " else "Function " in
      let as_clause = if rt = Nothing then "" else " As " ^ string_of_dt rt in
      let param_strs = List.map (fun (m, n, t) -> string_of_pmode m ^ " " ^ n ^ " As " ^ string_of_dt t) params in
      let param_strs = if is_variadic then param_strs @ ["..."] else param_strs in
      let pl = String.concat ", " param_strs in
      prefix ^ "Declare " ^ keyword ^ name ^ "(" ^ pl ^ ")" ^ as_clause ^ "\n"

let print_program prog = String.concat "\n" (List.map string_of_def prog)
