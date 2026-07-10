
%{
open Ast
%}

%token <int> INT_LIT
%token <string> STRING_LIT
%token <string> ID

/* Core structural tokens */
%token PUBLIC STRUCTURE FUNCTION SUB END AS DIM RETURN NOTHING POINTER_TYPE
%token INT_TYPE BYTE_TYPE EOF LPAREN RPAREN COMMA NEWLINE DOT

/* Operators and Control Flow */
%token IF THEN ELSE SELECT CASE WHILE DO FOR TO
%token PLUS MINUS TIMES DIVIDE MOD AND OR NOT XOR SHL SHR
%token EQUALS NOT_EQUALS LESS GREATER LESS_EQUALS GREATER_EQUALS

/* Define Math Precedence Layer (Lowest to Highest) */
%left OR XOR
%left AND
%nonassoc NOT
%left EQUALS NOT_EQUALS LESS GREATER LESS_EQUALS GREATER_EQUALS
%left SHL SHR
%left PLUS MINUS
%left TIMES DIVIDE MOD
%left DOT

/* Program Entry Point */
%start <Ast.program> program
%%

/* Fixes line 2: The program can start, finish, or separate items with blank rows */
program:
  | optional_newlines defs = definition_list EOF { defs }
;

definition_list:
  | /* empty */ { [] }
  | d = definition dl = definition_list { d :: dl }
;

(* A simple layout checking if the 'Public' keyword is present *)
visibility:
  | PUBLIC    { true }
  | /* empty */ { false }
;

definition:
  | vis=visibility STRUCTURE name=ID mandatory_newlines fields=list(struct_field) END STRUCTURE mandatory_newlines
    { Ast.Structure(vis, name, fields) }
  | vis=visibility FUNCTION name=ID LPAREN params=separated_list(COMMA, param) RPAREN AS ret=data_type mandatory_newlines body=block END FUNCTION mandatory_newlines
    { Ast.FuncDef(vis, name, params, ret, body) }
  | vis=visibility SUB name=ID LPAREN params=separated_list(COMMA, param) RPAREN mandatory_newlines body=block END SUB mandatory_newlines
    { Ast.FuncDef(vis, name, params, Ast.Nothing, body) }
;

struct_field:
  | PUBLIC name=ID AS t=data_type mandatory_newlines { (name, t) }
;

param:
  | name=ID AS t=data_type { (name, t) }
;

data_type:
  | INT_TYPE     { Ast.Int }
  | BYTE_TYPE    { Ast.Byte }
  | NOTHING      { Ast.Nothing }
  | POINTER_TYPE { Ast.Pointer }
;

block:
  | body=stmt_list { body }
;

stmt_list:
  | /* empty */          { [] }
  | s=stmt sl=stmt_list  { s :: sl }
;

stmt:
  | s=code_stmt mandatory_newlines { s }
;

code_stmt:
  | DIM name=ID AS t=data_type EQUALS e=expr { Ast.Dim(name, t, e) }
  | DIM name=ID AS struct_name=ID 
    LPAREN args=separated_list(COMMA, expr) RPAREN
    { 
      Ast.Dim(name, Ast.Pointer, Ast.Call(struct_name, args)) 
    }
  | name=ID EQUALS e=expr                    { Ast.Assign(name, e) }
  | e=expr                                   { Ast.ExprStatement(e) }
  | RETURN e=option(expr)                    { Ast.Return(e) }
  
  | IF cond=expr THEN mandatory_newlines body=block els=if_else_block END IF
    { Ast.If(cond, body, els) }

  (* | SELECT CASE target=expr mandatory_newlines cases=list(case_block) def=option(case_else_block) END SELECT *)
  (*   { Ast.SelectCase(target, cases, def) } *)

  | WHILE cond=expr DO mandatory_newlines body=block END WHILE
    { Ast.While(cond, body) }

  | FOR var=ID EQUALS start=expr TO finish=expr mandatory_newlines body=block END FOR
    { Ast.For(var, start, finish, body) }

  (* Fix: Select Case now parses a unified list of case branches *)
  | SELECT CASE target=expr mandatory_newlines branches=case_branches END SELECT
    { 
      let explicit_cases, default_case = branches in
      Ast.SelectCase(target, explicit_cases, default_case) 
    }
;
(* This rule parses the entire inner body of the Select Case statement deterministically *)
case_branches:
  | /* empty */ 
    { ([], None) }
    
  | CASE ELSE mandatory_newlines body=block 
    { ([], Some(body)) }
    
  | CASE exprs=separated_nonempty_list(COMMA, expr) mandatory_newlines body=block rest=case_branches 
    { 
      let explicit_rest, default_opt = rest in
      ((exprs, body) :: explicit_rest, default_opt) 
    }
;
if_else_block:
  | /* empty */                         { [] }
  | ELSE mandatory_newlines body=block  { body }
;

expr:
  | id=ID                                                   { Ast.Id(id) }
  | i=INT_LIT                                               { Ast.IntLit(i) }
  | s=STRING_LIT                                            { Ast.StringLit(s) }
  | LPAREN e=expr RPAREN                                    { e }
  | NOT e=expr                                              { Ast.UnaryOp("Not", e) }
  | name=ID LPAREN args=separated_list(COMMA, expr) RPAREN  { Ast.Call(name, args) }
  
  /* FieldAccess */
  | e1=expr DOT field=ID           { Ast.FieldAccess(e1, field) }
  | e1=expr PLUS e2=expr           { Ast.BinOp(e1, Ast.Add, e2) }
  | e1=expr MINUS e2=expr          { Ast.BinOp(e1, Ast.Sub, e2) }
  | e1=expr TIMES e2=expr          { Ast.BinOp(e1, Ast.Mul, e2) }
  | e1=expr DIVIDE e2=expr         { Ast.BinOp(e1, Ast.Div, e2) }
  | e1=expr MOD e2=expr            { Ast.BinOp(e1, Ast.Mod, e2) }
  | e1=expr AND e2=expr            { Ast.BinOp(e1, Ast.And, e2) }
  | e1=expr OR e2=expr             { Ast.BinOp(e1, Ast.Or, e2) }
  | e1=expr XOR e2=expr            { Ast.BinOp(e1, Ast.Xor, e2) }
  | e1=expr SHL e2=expr            { Ast.BinOp(e1, Ast.Shl, e2) }
  | e1=expr SHR e2=expr            { Ast.BinOp(e1, Ast.Shr, e2) }
  | e1=expr EQUALS e2=expr         { Ast.BinOp(e1, Ast.Equal, e2) }
  | e1=expr GREATER e2=expr        { Ast.BinOp(e1, Ast.Greater, e2) }
  | e1=expr LESS e2=expr           { Ast.BinOp(e1, Ast.Less, e2) }
  | e1=expr GREATER_EQUALS e2=expr { Ast.BinOp(e1, Ast.GreaterEqual, e2) }
  | e1=expr LESS_EQUALS e2=expr    { Ast.BinOp(e1, Ast.LessEqual, e2) }
  | e1=expr NOT_EQUALS e2=expr     { Ast.BinOp(e1, Ast.NotEqual, e2) }
;

/* Require at least one newline */
mandatory_newlines:
  | NEWLINE                     { () }
  | NEWLINE mandatory_newlines  { () }
;

/* Accept zero or more optional newlines safely */
optional_newlines:
  | /* empty */                { () }
  | NEWLINE optional_newlines  { () }
;

