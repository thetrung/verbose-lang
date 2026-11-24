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
  | Array of data_type * int (* element type, size; 0 = unsized slice/param *)

type op = 
  (* Numeric *)

  | Add | Sub | Mul | Div | Mod 
  (* Bitwise *)

  | And | Or | Xor | Shl | Shr
  (* Comparison *)

  | Equal | Greater | Less | GreaterEqual | LessEqual | NotEqual

type param_mode = | ByVal | ByRef

type expr =
  | Id of string
  | IntLit of int
  | FloatLit of float
  | BooleanLit of bool
  | StringLit of string
  | UnaryOp of string * expr (* For "Not" or negative numbers *)
  | BinOp of expr * op * expr
  | Call of string * (param_mode * expr) list
  | FieldAccess of expr * string    (* variable.Kind *)
  | IndexAccess of expr * expr      (* arr[i] *)
  | ArrayLit of expr list           (* [1, 2, 3] *)
  | StructInit of string * (string * expr) list (* Vector With { x = 1.0, y = 2.0 } *)
  
type stmt =
  | Dim of string * data_type * expr option
  | Assign of string * expr
  | AssignExpr of expr * expr 
  | FieldAssign of expr * string * expr (* 🆕 Added for field assignment: e.g., t.Kind = 5 *)
  | IndexAssign of expr * expr * expr   (* arr[i] = val *)
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
  | FuncDef of bool * string * (param_mode * string * data_type) list * data_type * stmt list
  | ExternDecl of bool * string * (param_mode * string * data_type) list * data_type * bool (* vis, name, params, ret, is_variadic *)

type program = definition list

