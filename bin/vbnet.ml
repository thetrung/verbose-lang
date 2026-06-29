open Llvm

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
  (* | ID target -> *)
  (*     state.tokens <- List.tl state.tokens; *)
  (*     consume state ASSIGN; *)
  (*     let expr = parse_expr state in *)
  (*     consume state NEWLINE; *)
  (*     Assign (target, expr) *)
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

(* ==============================================================================
   5. VISUAL PRINT TREE FORMATTER
   ============================================================================== *)
let print_ast program =
  List.iter (fun element ->
    match element with
    | Structure (name, fields) ->
        Printf.printf "\n-> Native Struct Found: '%s'\n" name;
        List.iter (fun (f_name, f_type, array_size) ->
          match array_size with
          | Some size -> Printf.printf "   * Buffer field: %s As %s[%d bytes]\n" f_name (print_type f_type) size
          | None      -> Printf.printf "   * Standard field: %s As %s\n" f_name (print_type f_type)
        ) fields
    | Function (name, params, ret_type, stmt_list) ->
        Printf.printf "\n-> Native Function Found: '%s' returning %s\n" name (print_type ret_type);
        Printf.printf "   * Dynamic inline parameters count: %d\n" (List.length params);
        List.iter (fun (stmt) -> 
          match stmt with
          | Assign(FieldAccess (obj, field), _) -> Printf.printf "   * -> Access %s.%s \n" obj field;
          | _ -> Printf.printf ""
        ) stmt_list;
  ) program;
  Printf.printf "\n"

  exception CodeGenError of string

(* Maps our AST primitive types to the corresponding low-level LLVM notation *)
let llvm_type_of = function
  | Integer -> "i32"
  | Byte    -> "i8"
  | Long    -> "i64"
  | Single  -> "float"
  | Double  -> "double"
  | Custom s -> "%struct." ^ s

(* Code Generator Context to track the current virtual SSA registers *)
type codegen_ctx = {
  mutable reg_counter : int;
  local_vars  : (string, string) Hashtbl.t;       
  local_types : (string, data_type) Hashtbl.t;    
  struct_maps : (string, (string * int) list) Hashtbl.t; (* Maps struct_name -> (field_name * index) *)
}

let new_reg ctx =
  ctx.reg_counter <- ctx.reg_counter + 1;
  "%" ^ string_of_int ctx.reg_counter

(* ==============================================================================
   5. DEFINITIVE NORMALIZED EMITTER GENERATION LOGIC
   ============================================================================== *)
let rec codegen_expr ctx out_buf = function
  | Literal n -> n
  | Variable name -> 
      let l_name = String.lowercase_ascii name in
      if Hashtbl.mem ctx.local_vars l_name then
        let alloc_reg = Hashtbl.find ctx.local_vars l_name in
        let val_reg = new_reg ctx in
        let base_dt = Hashtbl.find ctx.local_types l_name in
        let typ = llvm_type_of base_dt in
        Buffer.add_string out_buf (Printf.sprintf "    %s = load %s, ptr %s, align 4\n" val_reg typ alloc_reg);
        val_reg
      else "%" ^ l_name
  | FieldAccess (obj_raw, field_raw) ->
      let obj_name = String.lowercase_ascii obj_raw in
      let field_name = String.lowercase_ascii field_raw in
      let obj_ptr = Hashtbl.find ctx.local_vars obj_name in
      let struct_type = Hashtbl.find ctx.local_types obj_name in
      let struct_name = match struct_type with 
        | Custom s -> String.lowercase_ascii s 
        | _ -> failwith "Not a structure object" 
      in
      let fields = Hashtbl.find ctx.struct_maps struct_name in
      let idx = List.assoc field_name fields in
      let element_ptr_reg = new_reg ctx in
      let val_reg = new_reg ctx in
      Buffer.add_string out_buf (Printf.sprintf "    %s = getelementptr inbounds %%struct.%s, ptr %s, i32 0, i32 %d\n" element_ptr_reg struct_name obj_ptr idx);
      Buffer.add_string out_buf (Printf.sprintf "    %s = load i32, ptr %s, align 4\n" val_reg element_ptr_reg);
      val_reg
  | BinOp (left, op, right) ->
      let l_val = codegen_expr ctx out_buf left in
      let r_val = codegen_expr ctx out_buf right in
      let res_reg = new_reg ctx in
      let llvm_op = match op with "+" -> "add nsw" | "-" -> "sub nsw" | "*" -> "mul nsw" | "/" -> "sdiv" | _ -> failwith "Op error" in
      Buffer.add_string out_buf (Printf.sprintf "    %s = %s i32 %s, %s\n" res_reg llvm_op l_val r_val);
      res_reg

let codegen_stmt ctx out_buf = function
  | VarDecl (raw_name, dt, init_opt) ->
      let name = String.lowercase_ascii raw_name in
      let norm_dt = match dt with Custom s -> Custom (String.lowercase_ascii s) | other -> other in
      let llvm_type = llvm_type_of norm_dt in
      let alloc_reg = "%" ^ name ^ ".alloc" in
      Hashtbl.add ctx.local_vars name alloc_reg;
      Hashtbl.add ctx.local_types name norm_dt;
      Buffer.add_string out_buf (Printf.sprintf "    %s = alloca %s, align 4\n" alloc_reg llvm_type);
      (match norm_dt with
       | Custom _ -> () 
       | _ ->
            (match init_opt with
            | Some expr ->  
                let expr_reg = codegen_expr ctx out_buf expr in
                Buffer.add_string out_buf (Printf.sprintf "    store %s %s, ptr %s, align 4\n" llvm_type expr_reg alloc_reg)
            | None -> ()))
  | Assign (FieldAccess (obj_raw, field_raw), expr) ->
      let obj_name = String.lowercase_ascii obj_raw in
      let field_name = String.lowercase_ascii field_raw in
      let obj_ptr = Hashtbl.find ctx.local_vars obj_name in
      let struct_type = Hashtbl.find ctx.local_types obj_name in
      let struct_name = match struct_type with 
        | Custom s -> String.lowercase_ascii s 
        | _ -> failwith "Target mismatch" 
      in
      let fields = Hashtbl.find ctx.struct_maps struct_name in
      let idx = List.assoc field_name fields in
      let expr_reg = codegen_expr ctx out_buf expr in
      let element_ptr_reg = new_reg ctx in
      Buffer.add_string out_buf (Printf.sprintf "    %s = getelementptr inbounds %%struct.%s, ptr %s, i32 0, i32 %d\n" element_ptr_reg struct_name obj_ptr idx);
      Buffer.add_string out_buf (Printf.sprintf "    store i32 %s, ptr %s, align 4\n" expr_reg element_ptr_reg)
  | Assign (Variable raw_name, expr) ->
      let name = String.lowercase_ascii raw_name in
      let alloc_reg = Hashtbl.find ctx.local_vars name in
      let typ = llvm_type_of (Hashtbl.find ctx.local_types name) in
      let expr_reg = codegen_expr ctx out_buf expr in
      Buffer.add_string out_buf (Printf.sprintf "    store %s %s, ptr %s, align 4\n" typ expr_reg alloc_reg)
  | Return expr ->
      let ret_reg = codegen_expr ctx out_buf expr in
      Buffer.add_string out_buf (Printf.sprintf "    ret i32 %s\n" ret_reg)
  | _ -> failwith "Unsupported statement type matching pass"

(* Main top-level emit pass loop entry *)
let emit_llvm program =
  let out_buf = Buffer.create 1024 in
  let global_struct_maps = Hashtbl.create 5 in
  
  (* PASS 1: Populate global tracking index maps with uniform lowercase keys *)
  List.iter (fun element ->
    match element with
    | Structure (raw_name, fields) ->
        let name = String.lowercase_ascii raw_name in
        let indexed_fields = List.mapi (fun idx (f_name, _, _) -> 
          (String.lowercase_ascii f_name, idx)
        ) fields in
        Hashtbl.add global_struct_maps name indexed_fields;
        
        Buffer.add_string out_buf (Printf.sprintf "%%struct.%s = type { " name);
        let field_strings = List.map (fun (_, dt, array_size_opt) ->
          let base_type = llvm_type_of dt in
          match array_size_opt with 
          | Some size -> Printf.sprintf "[%d x %s]" size base_type 
          | None -> base_type
        ) fields in
        Buffer.add_string out_buf (String.concat ", " field_strings);
        Buffer.add_string out_buf " }\n\n"
    | _ -> ()
  ) program;

  (* PASS 2: Emit validated LLVM functions *)
  List.iter (fun element ->
    match element with
    | Function (raw_name, params, ret_type, body) ->
        let name = String.lowercase_ascii raw_name in
        let ctx = { 
          reg_counter = 0; 
          local_vars = Hashtbl.create 10; 
          local_types = Hashtbl.create 10; 
          struct_maps = global_struct_maps 
        } in
        
        (* Register function parameters as accessible tracking variables *)
        let param_strings = List.map (fun (p_raw_name, p_dt) -> 
          let p_name = String.lowercase_ascii p_raw_name in
          let norm_dt = match p_dt with 
            | Custom s -> Custom (String.lowercase_ascii s) 
            | other -> other 
          in
          Hashtbl.add ctx.local_vars p_name ("%" ^ p_name);
          Hashtbl.add ctx.local_types p_name norm_dt;
          match norm_dt with 
          | Custom _ -> Printf.sprintf "ptr %%%s" p_name
          | other_dt -> Printf.sprintf "%s %%%s" (llvm_type_of norm_dt) p_name
        ) params in
        
        Buffer.add_string out_buf (Printf.sprintf "define %s @%s(%s) {\nentry:\n" 
          (llvm_type_of ret_type) name (String.concat ", " param_strings));
          
        List.iter (codegen_stmt ctx out_buf) body;
        Buffer.add_string out_buf "}\n\n"
    | _ -> ()
  ) program;
  Buffer.contents out_buf
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

let () =
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

