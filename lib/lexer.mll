{
open Parser
exception SyntaxError of string
}

let whitespace = [' ' '\t' '\r']+
let newline = '\n'
let id = ['a'-'z' 'A'-'Z' '_'] ['a'-'z' 'A'-'Z' '0'-'9' '_']*
let int = ['0'-'9']+
let hex = '0' ['x' 'X'] ['0'-'9' 'a'-'f' 'A'-'F']+ (* 0x1234 *)
let float_num = ['0'-'9']+ '.' ['0'-'9']* (* 3.14 or -0.5 *)

rule tokenize = parse
  | whitespace { tokenize lexbuf }
  | newline    { Lexing.new_line lexbuf; NEWLINE }
  | "'"        { comment lexbuf }

  (* System Block Keywords *)
  | "Include"        { INCLUDE }
  | "Public"         { PUBLIC }
  | "Private"        { PRIVATE }
  | "Declare"        { DECLARE }
  | "With"           { WITH }
  | "Static"         { STATIC }
  | "SizeOf"         { SIZEOF }
  | "HeapAlloc"      { HEAP_ALLOC }
  | "HeapRealloc"    { HEAP_REALLOC }
  | "Arena"          { ARENA_TYPE }
  | "ArenaAlloc"     { ARENA_ALLOC }
  | "Structure"      { STRUCTURE }
  | "Enum"           { ENUM }
  | "Function"       { FUNCTION }
  | "Sub"            { SUB }
  | "ByVal"          { BYVAL }
  | "ByRef"          { BYREF }
  | "End"            { END }
  | "As"             { AS }
  | "Dim"            { DIM }
  | "Return"         { RETURN }
  | "Integer"        { INT_TYPE }
  | "Byte"           { BYTE_TYPE }
  | "Short"          { SHORT_TYPE }
  | "Long"           { LONG_TYPE }
  | "Single"         { SINGLE_TYPE }
  | "Double"         { DOUBLE_TYPE }
  | "Boolean"        { BOOLEAN_TYPE }
  | "True"           { BOOLEAN_LIT(true) }
  | "False"          { BOOLEAN_LIT(false) }
  | "Nothing"        { NOTHING }
  | "Pointer"        { POINTER_TYPE }

  (* Control Flow Keywords *)
  | "If"             { IF }
  | "Then"           { THEN }
  | "Else"           { ELSE }
  | "Select"         { SELECT }
  | "Case"           { CASE }
  | "While"          { WHILE }
  | "Do"             { DO }
  | "For"            { FOR }
  | "To"             { TO }

  (* Operator Words *)
  | "Mod"            { MOD }
  | "And"            { AND }
  | "Or"             { OR }
  | "Not"            { NOT }
  | "Xor"            { XOR }
  | "Shl"            { SHL }
  | "Shr"            { SHR }

  (* Symbols / Operators *)
  | "+"              { PLUS }
  | "-"              { MINUS }
  | "*"              { TIMES }
  | "/"              { DIVIDE }
  | "="              { EQUALS }
  | "<>"             { NOT_EQUALS }
  | "<="             { LESS_EQUALS }
  | ">="             { GREATER_EQUALS }
  | "<"              { LESS }
  | ">"              { GREATER }
  | "("              { LPAREN }
  | ")"              { RPAREN }
  | "{"              { LBRACE }
  | "}"              { RBRACE }
  | ","              { COMMA }
  | "..."            { ELLIPSIS }
  | "."              { DOT }

  (* Value Literals *)
  | '-'? hex as lxm         { INT_LIT(int_of_string lxm) }
  | '-'? int as lxm         { INT_LIT(int_of_string lxm) } 
  | '-'? float_num as lxm   { FLOAT_LIT(float_of_string lxm) }
  | '"'                     { string_literal (Buffer.create 16) lexbuf }
  | id as lxm               { ID(lxm) }
  | eof                     { EOF }
  | _ as c                  { raise (SyntaxError (Printf.sprintf "Unexpected character '%c'" c)) }

and comment = parse
  | newline { Lexing.new_line lexbuf; NEWLINE }
  | eof     { EOF }
  | _       { comment lexbuf }

and string_literal buf = parse
  | '"'           { STRING_LIT(Buffer.contents buf) }
  | '\\' 'n'      { Buffer.add_char buf '\n'; string_literal buf lexbuf }
  | '\\' 'r'      { Buffer.add_char buf '\r'; string_literal buf lexbuf }
  | '\\' 't'      { Buffer.add_char buf '\t'; string_literal buf lexbuf }
  | '\\' '"'      { Buffer.add_char buf '"'; string_literal buf lexbuf }
  | '\\' '\\'     { Buffer.add_char buf '\\'; string_literal buf lexbuf }
  | '\\' '0'      { Buffer.add_char buf '\000'; string_literal buf lexbuf }
  | eof           { raise (SyntaxError "Unterminated string literal") }
  | _ as c        { Buffer.add_char buf c; string_literal buf lexbuf }
