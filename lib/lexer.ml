  open Ast
  open Token
(* ==============================================================================
   3. THE REAL CHARACTER-BY-CHARACTER LEXER (From String)
   ============================================================================== *)
exception LexError of string

let is_alpha c = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c = '_'
let is_digit c = c >= '0' && c <= '9'
let is_space c = c = ' ' || c = '\t' || c = '\r'

let lex code =
  let len = String.length code in
  let pos = ref 0 in
  let tokens = ref [] in

  let peek_char () = if !pos < len then Some code.[!pos] else None in
  let advance () = incr pos in

  while !pos < len do
    match peek_char() with
    | None -> ()
    | Some c when is_space c -> advance ()
    
    (* Handle Comments: In VB, a single quote (') skips the whole line *)
    | Some '\'' ->
        while !pos < len && code.[!pos] <> '\n' do advance () done
        
    (* Handle Newlines explicitly *)
    | Some '\n' ->
        tokens := NEWLINE :: !tokens;
        advance ()
        
    (* Symbols & Operators *)
    | Some '=' -> tokens := ASSIGN :: !tokens; advance ()
    | Some '(' -> tokens := LPAREN :: !tokens; advance ()
    | Some ')' -> tokens := RPAREN :: !tokens; advance ()
    | Some '.' -> tokens := DOT :: !tokens; advance ()

    | Some ('+' | '-' | '*' | '/' | '%' as op) -> 
        tokens := OP (String.make 1 op) :: !tokens; 
        advance ()
        
    (* Numbers *)
    | Some c when is_digit c ->
        let start = !pos in
        while !pos < len && is_digit code.[!pos] do advance () done;
        let num = String.sub code start (!pos - start) in
        tokens := NUMBER num :: !tokens
        
    (* Identifiers, Keywords, and Data Types *)
    | Some c when is_alpha c ->
        let start = !pos in
        while !pos < len && (is_alpha code.[!pos] || is_digit code.[!pos]) do advance () done;
        let word = String.sub code start (!pos - start) |> String.lowercase_ascii in
        
        let tok = match word with

          | "structure" | "end" | "function" | "as" | "dim" | "public" | "if" | "then" | "else" | "return" -> KEYWORD word
          | "integer" -> TYPE Integer
          | "single"  -> TYPE Single
          | "double"  -> TYPE Double
          | "byte"    -> TYPE Byte
          | "long"    -> TYPE Long
          | _ -> ID word
        in
        tokens := tok :: !tokens
        
    | Some unknown -> 
        raise (LexError (Printf.sprintf "Unknown character encountered: '%c'" unknown))
  done;
  List.rev (EOF :: !tokens)

