(* ==============================================================================
   1. ABSTRACT SYNTAX TREE (AST) DEFINITIONS
   ============================================================================== *)
type data_type = Integer | Single | Double | Byte | Long | Custom of string

type expr =
  | Literal of string
  | Variable of string
  | BinOp of expr * string * expr
  | FieldAccess of string * string (* object.field *)

type stmt =
  | VarDecl of string * data_type * expr option
  | Assign of expr * expr
  | Return of expr

type field_decl = string * data_type * int option

type program_element =
  | Structure of string * field_decl list
  | Function of string * (string * data_type) list * data_type * stmt list

type program = program_element list

let rec print_type = function
  | Integer -> "Integer" 
  | Single -> "Single" 
  | Double -> "Double" 
  | Byte -> "Byte" 
  | Long -> "Long"
  | Custom t -> "Structure " ^ t
