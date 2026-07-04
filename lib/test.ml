open Ast
open Token
open Lexer
open Parser
open Formatter
open CodeGen
(* ==============================================================================
   6. REAL COMPILER TEST EXECUTION (Parsing True String Source)
   ============================================================================== *)
let source_code = "
' This is a native code comment. The lexer will completely drop it!
Public Structure Player
  Public ID As Integer
  Public FixedName(31) As Byte
End Structure
  
Function UpdatePlayer(p As Player) As Integer
  p.ID = 777
  Return p.ID
End Function

Function InitializeMainEngine() As Integer
  Dim localPlayer As Player = New
  localPlayer.ID = 100
'  UpdatePlayer(localPlayer)
  
  Dim result As Integer = 0
  result = localPlayer.ID
  Return result
End Function

'Function Main()
'  Dim error as Integer = InitializeMainEngine()
'  Return error
'End Function
"

let run() =
  print_endline "==================================================";
  print_endline "  Running OCaml String Lexer -> Parser Pipeline  " ;
  print_endline "==================================================";
try(* 1. Lex the raw source string down into individual token structures *)
let token_list = lex source_code in
    print_endline "✓ Lexing Phase: SUCCESS";
    print_string source_code;
    print_string "\n";

(* 2. Feed token stream right into our state machine *)
let state = { tokens = token_list } in
let abstract_syntax_tree = parse_program state in 
    print_endline "✓ Parsing Phase: SUCCESS\n";
print_ast abstract_syntax_tree;

(* 3. Emit LLVM-IR *)
print_endline "--- [ Generated LLVM-IR Text Code Output ] ---";
let llvm_ir_output = 
      emit_llvm abstract_syntax_tree
      in print_string llvm_ir_output;

print_endline "----------------------------------------------\n";
print_endline "✓ Code Generation Phase Complete";

(* N. Output validation summary *)
print_endline "==================================================";
print_endline "✓ End-to-End Execution Complete" 
    with
    | LexError msg   -> Printf.eprintf "❌ Lexer Error: %s\n" msg; exit 1
    | ParseError msg -> Printf.eprintf "❌ Parser Error: %s\n" msg; exit 1
    | Failure msg    -> Printf.eprintf "❌ Code Generation error: %s\n" msg; exit 1
    (* | msg               -> Printf.eprintf "❌ Code Generation Error: %s\n" msg.contents; exit 1 *)

