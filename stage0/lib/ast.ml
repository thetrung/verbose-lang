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
  | Arena
  | Custom of string
  | Array of data_type * int (* element type, size; -1 = unsized slice/param *)

type op = 
  (* Numeric *)

  | Add | Sub | Mul | Div | Mod 
  (* Bitwise *)

  | And | Or | Xor | Shl | Shr
  (* Comparison *)

  | Equal | Greater | Less | GreaterEqual | LessEqual | NotEqual

type param_mode = | ByVal | ByRef

type visibility = | Public | Private | Default

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
  | StructInit of string * (string * expr) list (* Vector With ( x = 1.0, y = 2.0 ) *)
  | SizeOf of data_type             (* SizeOf(Point) *)
  | HeapAlloc of data_type * expr   (* HeapAlloc(Integer, 10) *)
  | HeapRealloc of expr * data_type * expr (* HeapRealloc(arr, Integer, 20) *)
  | ArenaAlloc of param_mode * expr * data_type * expr (* ArenaAlloc(ByRef arena, Point, 10) *)
  
type stmt =
  | Dim of string * data_type * expr option
  | StaticDim of string * data_type * expr option (* function-local persistent Static *)
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
  | Include of string
  | Structure of visibility * string * (string * data_type) list
  | EnumDef of visibility * string * (string * int) list
  | GlobalVar of visibility * string * data_type * expr option   (* module-level Static global *)
  | FuncDef of visibility * string * (param_mode * string * data_type) list * data_type * stmt list
  | ExternDecl of visibility * string * (param_mode * string * data_type) list * data_type * bool (* vis, name, params, ret, is_variadic *)

type program = definition list

