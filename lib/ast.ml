type data_type =
  | Int
  | Byte
  | Short  
  | Long   
  | Single 
  | Double
  | Boolean
  | Nothing
  | Pointer
  | Custom of string

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
  | FloatLit of float
  | BooleanLit of bool
  | StringLit of string
  | UnaryOp of string * expr (* For "Not" or negative numbers *)
  | BinOp of expr * op * expr
  | Call of string * expr list
  | FieldAccess of expr * string    (* variable.Kind *)
  
type stmt =
  | Dim of string * data_type * expr option
  | Assign of string * expr
  | AssignExpr of expr * expr 
  | FieldAssign of expr * string * expr (* 🆕 Added for field assignment: e.g., t.Kind = 5 *)
  | ExprStatement of expr
  | Return of expr option

  (* Conditions *)
  | If of expr * stmt list * stmt list (* If condition Then body Else body *)

  (* Case evaluation values -> statements *)
  | SelectCase of expr * (expr list * stmt list) list * stmt list option

  (* Loops *)
  | While of expr * stmt list
  | For of string * expr * expr * stmt list (* For var = start To end *)

type definition =
  | Structure of bool * string * (string * data_type) list (* added bool flag *)
  | EnumDef of bool * string * (string * int) list
  | FuncDef of bool * string * (string * data_type) list * data_type * stmt list

type program = definition list

