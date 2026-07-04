type data_type =
  | Int
  | Byte
  | Nothing
  | Pointer

type op = 
  (* Numeric *)

  | Add | Sub | Mul | Div | Mod 
  (* Bitwise *)

  | And | Or | Xor | Shl | Shr
  (* Comparison *)

  | Equal | Greater | Less | GreaterEqual | LessEqual | NotEqual

type expr =
  | Id of string
  | IntLit of int
  | StringLit of string
  | UnaryOp of string * expr (* For "Not" or negative numbers *)
  | BinOp of expr * op * expr
  | Call of string * expr list

type stmt =
  | Dim of string * data_type * expr
  | Assign of string * expr
  | ExprStatement of expr
  | Return of expr option
  (* Conditions *)
  | If of expr * stmt list * stmt list (* If condition Then body Else body *)
  | SelectCase of expr * (expr list * stmt list) list (* Case evaluation values -> statements *)
  (* Loops *)
  | While of expr * stmt list
  | For of string * expr * expr * stmt list (* For var = start To end *)

type definition =
  | Structure of bool * string * (string * data_type) list (* added bool flag *)
  | FuncDef of bool * string * (string * data_type) list * data_type * stmt list

type program = definition list

(* Pretty Print  *)

let rec string_of_dt = function

  | Int -> "Integer" | Byte -> "Byte" | Nothing -> "Nothing" | Pointer -> "Pointer"

let string_of_op = function

  | Add -> "+" | Sub -> "-" | Mul -> "*" | Div -> "/" | Mod -> "Mod"
  | And -> "And" | Or -> "Or" | Xor -> "Xor" | Shl -> "Shl" | Shr -> "Shr"
  | Equal -> "=" | Greater -> ">" | Less -> "<" | GreaterEqual -> ">=" | LessEqual -> "<=" | NotEqual -> "<>"

let rec string_of_expr = function
  | Id id -> id
  | IntLit i -> string_of_int i
  | StringLit s -> "\"" ^ s ^ "\""
  | UnaryOp (op, e) -> "(" ^ op ^ " " ^ string_of_expr e ^ ")"
  | BinOp (e1, op, e2) -> "(" ^ string_of_expr e1 ^ " " ^ string_of_op op ^ " " ^ string_of_expr e2 ^ ")"
  | Call (f, args) -> f ^ "(" ^ String.concat ", " (List.map string_of_expr args) ^ ")"

let rec string_of_stmt indent = function
  | Dim (v, dt, e) -> indent ^ "Dim " ^ v ^ " As " ^ string_of_dt dt ^ " = " ^ string_of_expr e ^ "\n"
  | Assign (v, e) -> indent ^ v ^ " = " ^ string_of_expr e ^ "\n"
  | ExprStatement e -> indent ^ string_of_expr e ^ "\n"
  | Return None -> indent ^ "Return\n"
  | Return (Some e) -> indent ^ "Return " ^ string_of_expr e ^ "\n"
  | While (c, body) -> indent ^ "While " ^ string_of_expr c ^ "\n" ^ string_of_block (indent ^ "  ") body ^ indent ^ "End While\n"
  | For (v, s, f, body) -> indent ^ "For " ^ v ^ " = " ^ string_of_expr s ^ " To " ^ string_of_expr f ^ "\n" ^ string_of_block (indent ^ "  ") body ^ indent ^ "End For\n"
  | If (c, t, e) -> 
      let els_str = if e = [] then "" else indent ^ "Else\n" ^ string_of_block (indent ^ "  ") e in
      indent ^ "If " ^ string_of_expr c ^ " Then\n" ^ string_of_block (indent ^ "  ") t ^ els_str ^ indent ^ "End If\n"
  | SelectCase (e, cases) ->
      let print_case (el, b) = 
        indent ^ "  Case " ^ String.concat ", " (List.map string_of_expr el) ^ "\n" ^ string_of_block (indent ^ "    ") b 
      in
      indent ^ "Select Case " ^ string_of_expr e ^ "\n" ^ String.concat "" (List.map print_case cases) ^ indent ^ "End Select\n"

and string_of_block indent stmts = String.concat "" (List.map (string_of_stmt indent) stmts)

let string_of_def = function
  | Structure (is_public, name, fields) ->
      let prefix = if is_public then "Public " else "" in
      let fl = String.concat "" (List.map (fun (n, t) -> "  Public " ^ n ^ " As " ^ string_of_dt t ^ "\n") fields) in
      prefix ^ "Structure " ^ name ^ "\n" ^ fl ^ "End Structure\n"
  | FuncDef (is_public, name, params, rt, body) ->
      let prefix = if is_public then "Public " else "" in
      let keyword = if rt = Nothing then "Sub " else "Function " in
      let as_clause = if rt = Nothing then "" else " As " ^ string_of_dt rt in
      let pl = String.concat ", " (List.map (fun (n, t) -> n ^ " As " ^ string_of_dt t) params) in
      prefix ^ keyword ^ name ^ "(" ^ pl ^ ")" ^ as_clause ^ "\n" ^ string_of_block "  " body ^ "End " ^ keyword ^ "\n"

let print_program prog = String.concat "\n" (List.map string_of_def prog)
