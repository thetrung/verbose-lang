open Ast
(* ==============================================================================
   2. TOKENS DEFINITIONS
   ============================================================================== *)
type token =
  | KEYWORD of string
  | TYPE of data_type
  | ID of string
  | NUMBER of string
  | ASSIGN
  | OP of string
  | LPAREN
  | RPAREN
  | NEWLINE
  | DOT
  | EOF
