{
open Parser
exception SyntaxError of string
}

let whitespace = [' ' '\t' '\r']+
let newline = '\n'
let id = ['a'-'z' 'A'-'Z' '_'] ['a'-'z' 'A'-'Z' '0'-'9' '_']*
let int = ['0'-'9']+

rule tokenize = parse
  | whitespace { tokenize lexbuf }
  | newline    { Lexing.new_line lexbuf; NEWLINE }
  | "'"        { comment lexbuf }

  (* System Block Keywords *)
  | "Public"         { PUBLIC }
  | "Structure"      { STRUCTURE }
  | "Function"       { FUNCTION }
  | "Sub"            { SUB }
  | "End"            { END }
  | "As"             { AS }
  | "Dim"            { DIM }
  | "Return"         { RETURN }
  | "Integer"        { INT_TYPE }
  | "Byte"           { BYTE_TYPE }
  | "Nothing"        { NOTHING }
  | "Pointer"        { POINTER_TYPE }

  (* Control Flow Keywords *)
  | "If"             { IF }
  | "Then"           { THEN }
  | "Else"           { ELSE }
  | "Select"         { SELECT }
  | "Case"           { CASE }
  (* | "Else"           { ELSE } *)
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
  | ","              { COMMA }
  | "."              { DOT }

  (* Value Literals *)
  | int as lxm       { INT_LIT(int_of_string lxm) }
  | '"'              { string_literal (Buffer.create 16) lexbuf }
  | id as lxm        { ID(lxm) }
  | eof              { EOF }

and comment = parse
  | newline { tokenize lexbuf }
  | _       { comment lexbuf }

and string_literal buf = parse
  | '"'           { STRING_LIT(Buffer.contents buf) }
  | '\\' 'n'      { Buffer.add_char buf '\n'; string_literal buf lexbuf }
  | _ as c        { Buffer.add_char buf c; string_literal buf lexbuf }
