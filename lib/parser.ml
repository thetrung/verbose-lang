  open Ast
  open Token
  open Lexer
(* ==============================================================================
   4. THE PARSER ENGINE
  ============================================================================== *)
exception ParseError of string

(* Converts a single token variant into a readable string string layout *)
let string_of_token = function
  | KEYWORD kw -> Printf.sprintf "KEYWORD(%s)" kw
  | TYPE dt    -> Printf.sprintf "TYPE(%s)" (print_type dt) (* Uses your existing print_type *)
  | ID name    -> Printf.sprintf "ID(%s)" name
  | NUMBER n   -> Printf.sprintf "NUMBER(%s)" n
  | ASSIGN     -> "ASSIGN(=)"
  | OP op      -> Printf.sprintf "OP(%s)" op
  | LPAREN     -> "LPAREN(()"
  | RPAREN     -> "RPAREN())"
  | NEWLINE    -> "NEWLINE(\\n)"
  | DOT        -> "DOT(.)"
  | EOF        -> "EOF"

(* Combines a complete list of tokens into a single clean text block *)
let string_of_token_list tokens =
  tokens 
  |> List.map string_of_token 
  |> String.concat ", "

type parser_state = { mutable tokens : token list }

let peek state = match state.tokens with [] -> EOF | t :: _ -> t

let consume state expected =
  match state.tokens with
  | t :: ts when t = expected -> state.tokens <- ts
  | e -> raise (ParseError ("Unexpected token in: \n" ^ (string_of_token_list e) ^ " mismatch error"))

let consume_id state =
  match state.tokens with ID name :: ts -> state.tokens <- ts; name | _ -> raise (ParseError "Expected an Identifier")

let consume_type state =
  match state.tokens with 
  | TYPE dt :: ts -> state.tokens <- ts; dt
  | ID name :: ts -> state.tokens <- ts; Custom name (* enforce custom struct *)
  | _ -> raise (ParseError "Expected a valid Core Type")

let consume_number state =
  match state.tokens with NUMBER n :: ts -> state.tokens <- ts; n | _ -> raise (ParseError "Expected base digits")

let rec skip_newlines state =
  match peek state with NEWLINE -> state.tokens <- List.tl state.tokens; skip_newlines state | _ -> ()

let parse_structure state =
  consume state (KEYWORD "structure");
  let struct_name = consume_id state in
  consume state NEWLINE;
  
  let rec parse_fields acc =
    skip_newlines state;
    match peek state with
    | KEYWORD "end" ->
        consume state (KEYWORD "end");
        consume state (KEYWORD "structure");
        consume state NEWLINE;
        List.rev acc
    | _ ->
        if peek state = KEYWORD "public" then consume state (KEYWORD "public");
        let field_name = consume_id state in
        let array_size = 
          if peek state = LPAREN then begin
            consume state LPAREN;
            let size = int_of_string (consume_number state) in
            consume state RPAREN;
            Some (size + 1)
          end else None 
        in
        consume state (KEYWORD "as");
        let field_type = consume_type state in
        consume state NEWLINE;
        parse_fields ((field_name, field_type, array_size) :: acc)
  in
  Structure (struct_name, parse_fields [])

let parse_primary state =
  match peek state with
  | NUMBER n -> state.tokens <- List.tl state.tokens; Literal n
  (* | ID name -> state.tokens <- List.tl state.tokens; Variable name *)
  | ID name -> 
    state.tokens <- List.tl state.tokens;
    if peek state = DOT then begin
      consume state DOT;
      let field = consume_id state in
      FieldAccess (name, field)
    end else Variable name
  | _ -> raise (ParseError "Invalid syntax expression target")

let rec parse_expr state =
  let left = parse_primary state in
  match peek state with
  | OP op ->
      state.tokens <- List.tl state.tokens;
      let right = parse_primary state in
      BinOp (left, op, right)
  | _ -> left

let parse_statement state =
  skip_newlines state;
  match peek state with
  | KEYWORD "dim" ->
      consume state (KEYWORD "dim");
      let name = consume_id state in
      consume state (KEYWORD "as");
      let dt = consume_type state in
      let init_expr = 
        if peek state = ASSIGN then begin 
          consume state ASSIGN;
          if peek state = KEYWORD "new" then
          (consume state (KEYWORD "new"); Some (Literal "new"))
          else Some (parse_expr state)
        end else None 
      in 
      consume state NEWLINE; 
      VarDecl (name, dt, init_expr)
  | KEYWORD "return" ->
      consume state (KEYWORD "return");
      let expr = parse_expr state in
      consume state NEWLINE;
      Return expr
  | _ -> 
    let lhs = parse_primary state in
    consume state ASSIGN;
    let rhs = parse_expr state in
    consume state NEWLINE; Assign (lhs, rhs)
    (* raise (ParseError "Invalid internal execution statement") *)

let parse_function state =
  consume state (KEYWORD "function");
  let func_name = consume_id state in
  consume state LPAREN;
  let rec parse_params acc =
    match peek state with
    | RPAREN -> consume state RPAREN; List.rev acc
    | ID name ->
        state.tokens <- List.tl state.tokens;
        consume state (KEYWORD "as");
        let dt = consume_type state in
        parse_params ((name, dt) :: acc)
    | _ -> raise (ParseError "Bad function parameter layout")
  in
  let params = parse_params [] in
  consume state (KEYWORD "as");
  let return_type = consume_type state in
  consume state NEWLINE;
  
  let rec parse_body acc =
    skip_newlines state;
    match peek state with
    | KEYWORD "end" ->
        consume state (KEYWORD "end");
        consume state (KEYWORD "function");
        consume state NEWLINE;
        List.rev acc
    | _ ->
        let stmt = parse_statement state in
        parse_body (stmt :: acc)
  in
  Function (func_name, params, return_type, parse_body [])

let parse_program state =
  let rec parse_elements acc =
    skip_newlines state;
    match peek state with
    | EOF -> List.rev acc
    | KEYWORD "public" -> consume state (KEYWORD "public"); parse_elements acc
    | KEYWORD "structure" -> parse_elements (parse_structure state :: acc)
    | KEYWORD "function" -> parse_elements (parse_function state :: acc)
    | _ -> raise (ParseError "Global context allows structures and functions only")
  in
  parse_elements []
