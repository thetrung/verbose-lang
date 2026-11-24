
%{
open Ast
%}

%token <int> INT_LIT
%token <string> STRING_LIT
%token <float> FLOAT_LIT
%token <bool> BOOLEAN_LIT
%token <string> ID

(* Core structural tokens *)
%token INCLUDE PUBLIC PRIVATE DECLARE WITH STATIC SIZEOF HEAP_ALLOC HEAP_REALLOC ARENA_TYPE ARENA_ALLOC STRUCTURE FUNCTION SUB BYVAL BYREF END AS DIM RETURN 
%token NOTHING POINTER_TYPE ENUM INT_TYPE BYTE_TYPE SHORT_TYPE LONG_TYPE SINGLE_TYPE DOUBLE_TYPE BOOLEAN_TYPE 
%token EOF LPAREN RPAREN LBRACE RBRACE LBRACKET RBRACKET COMMA NEWLINE DOT ELLIPSIS

(* Operators and Control Flow *)
%token IF THEN ELSE SELECT CASE WHILE DO FOR TO
%token PLUS MINUS TIMES DIVIDE MOD AND OR NOT XOR SHL SHR
%token EQUALS NOT_EQUALS LESS GREATER LESS_EQUALS GREATER_EQUALS

(* Define Math Precedence Layer (Lowest to Highest) *)
%left OR XOR
%left AND
%nonassoc NOT
%left EQUALS NOT_EQUALS LESS GREATER LESS_EQUALS GREATER_EQUALS
%left SHL SHR
%left PLUS MINUS
%left TIMES DIVIDE MOD
%left DOT LBRACKET

(* Program Entry Point *)
%start <Ast.program> program
%%

(* The program can start, finish, or separate items with blank rows *)
program:
  | optional_newlines defs = definition_list EOF { defs }
;

definition_list:
  | /* empty */ { [] }
  | d = definition dl = definition_list { d :: dl }
;

(* Visibility qualifier: Public, Private, or Default (omitted) *)
visibility:
  | PUBLIC      { Ast.Public }
  | PRIVATE     { Ast.Private }
  | /* empty */ { Ast.Default }
;

param:
  | BYVAL name=ID AS t=data_type                                 { (Ast.ByVal, name, t) }
  | BYREF name=ID AS t=data_type                                 { (Ast.ByRef, name, t) }
  | name=ID       AS t=data_type                                 { (Ast.ByVal, name, t) } (*Default: ByVal to Copy *)
  | BYVAL name=ID LBRACKET RBRACKET AS t=data_type               { (Ast.ByVal, name, Ast.Array(t, -1)) }
  | BYREF name=ID LBRACKET RBRACKET AS t=data_type               { (Ast.ByRef, name, Ast.Array(t, -1)) }
  | name=ID       LBRACKET RBRACKET AS t=data_type               { (Ast.ByVal, name, Ast.Array(t, -1)) }
  | BYVAL name=ID LBRACKET size=INT_LIT RBRACKET AS t=data_type  { (Ast.ByVal, name, Ast.Array(t, size)) }
  | BYREF name=ID LBRACKET size=INT_LIT RBRACKET AS t=data_type  { (Ast.ByRef, name, Ast.Array(t, size)) }
  | name=ID       LBRACKET size=INT_LIT RBRACKET AS t=data_type  { (Ast.ByVal, name, Ast.Array(t, size)) }
;

extern_params:
  | /* empty */                               { ([], false) }
  | ELLIPSIS                                  { ([], true) }
  | ps=extern_param_nonempty                  { ps }
;

extern_param_nonempty:
  | p=param                                   { ([p], false) }
  | p=param COMMA ELLIPSIS                    { ([p], true) }
  | p=param COMMA rest=extern_param_nonempty  { let (ps, var) = rest in (p :: ps, var) }
;

definition:
  | INCLUDE path=STRING_LIT mandatory_newlines
    { Ast.Include(path) }

  | vis=visibility STRUCTURE name=ID mandatory_newlines fields=list(struct_field) END STRUCTURE mandatory_newlines
    { Ast.Structure(vis, name, fields) }

  | vis=visibility ENUM name=ID mandatory_newlines members=list(enum_member) END ENUM mandatory_newlines
    { Ast.EnumDef(vis, name, members) }

  | vis=visibility STATIC name=ID AS t=data_type mandatory_newlines
    { Ast.GlobalVar(vis, name, t, None) }
  | vis=visibility STATIC name=ID AS t=data_type EQUALS e=expr mandatory_newlines
    { Ast.GlobalVar(vis, name, t, Some(e)) }
  | vis=visibility STATIC name=ID AS struct_name=ID WITH LBRACE optional_newlines inits=field_init_list RBRACE mandatory_newlines
    { Ast.GlobalVar(vis, name, Ast.Custom(struct_name), Some(Ast.StructInit(struct_name, inits))) }
  | vis=visibility STATIC name=ID LBRACKET size=INT_LIT RBRACKET AS t=data_type mandatory_newlines
    { Ast.GlobalVar(vis, name, Ast.Array(t, size), None) }
  | vis=visibility STATIC name=ID LBRACKET size=INT_LIT RBRACKET AS t=data_type EQUALS e=expr mandatory_newlines
    { Ast.GlobalVar(vis, name, Ast.Array(t, size), Some(e)) }
  | vis=visibility STATIC name=ID LBRACKET RBRACKET AS t=data_type mandatory_newlines
    { Ast.GlobalVar(vis, name, Ast.Array(t, -1), None) }
  | vis=visibility STATIC name=ID LBRACKET RBRACKET AS t=data_type EQUALS e=expr mandatory_newlines
    {
      match e with
      | Ast.ArrayLit elems -> Ast.GlobalVar(vis, name, Ast.Array(t, List.length elems), Some(e))
      | _ -> Ast.GlobalVar(vis, name, Ast.Array(t, -1), Some(e))
    }
  
  | vis=visibility DECLARE FUNCTION name=ID LPAREN ep=extern_params RPAREN AS ret=data_type mandatory_newlines
    { let (params, is_var) = ep in Ast.ExternDecl(vis, name, params, ret, is_var) }

  | vis=visibility DECLARE SUB name=ID LPAREN ep=extern_params RPAREN mandatory_newlines
    { let (params, is_var) = ep in Ast.ExternDecl(vis, name, params, Ast.Nothing, is_var) }

  | vis=visibility FUNCTION name=ID LPAREN params=separated_list(COMMA, param) RPAREN AS ret=data_type mandatory_newlines body=block END FUNCTION mandatory_newlines
    { Ast.FuncDef(vis, name, params, ret, body) }
  
  | vis=visibility SUB name=ID LPAREN params=separated_list(COMMA, param) RPAREN mandatory_newlines body=block END SUB mandatory_newlines
    { Ast.FuncDef(vis, name, params, Ast.Nothing, body) }
;

enum_member:
  | name=ID EQUALS value=INT_LIT mandatory_newlines { (name, value) }
;

struct_field:
  | DIM name=ID AS t=data_type mandatory_newlines                               { (name, t) }
  | DIM name=ID LBRACKET size=INT_LIT RBRACKET AS t=data_type mandatory_newlines { (name, Ast.Array(t, size)) }
;


data_type:
  | INT_TYPE     { Ast.Int }
  | BYTE_TYPE    { Ast.Byte }
  | NOTHING      { Ast.Nothing }
  | POINTER_TYPE { Ast.Pointer }  
  | ARENA_TYPE   { Ast.Arena }
  | SHORT_TYPE   { Ast.Short }  
  | LONG_TYPE    { Ast.Long }   
  | SINGLE_TYPE  { Ast.Single } 
  | DOUBLE_TYPE  { Ast.Double }
  | BOOLEAN_TYPE { Ast.Boolean }
  | id=ID        { Ast.Custom(id)}
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
  | DIM name=ID AS t=data_type               { Ast.Dim(name, t, None)}
  | DIM name=ID AS t=data_type EQUALS e=expr { Ast.Dim(name, t, Some(e)) }
  | DIM name=ID LBRACKET size=INT_LIT RBRACKET AS t=data_type
    { Ast.Dim(name, Ast.Array(t, size), None) }
  | DIM name=ID LBRACKET size=INT_LIT RBRACKET AS t=data_type EQUALS e=expr
    { Ast.Dim(name, Ast.Array(t, size), Some(e)) }
  | DIM name=ID LBRACKET RBRACKET AS t=data_type EQUALS e=expr
    {
      match e with
      | Ast.ArrayLit elems -> Ast.Dim(name, Ast.Array(t, List.length elems), Some(e))
      | _ -> Ast.Dim(name, Ast.Array(t, -1), Some(e))
    }
  | DIM name=ID AS struct_name=ID 
    LPAREN args=separated_list(COMMA, expr) RPAREN  
    { Ast.Dim(name, Ast.Pointer, Some(Ast.Call(struct_name, List.map (fun e -> (Ast.ByVal, e)) args))) }
  | DIM name=ID AS struct_name=ID WITH LBRACE optional_newlines inits=field_init_list RBRACE
    { Ast.Dim(name, Ast.Custom(struct_name), Some(Ast.StructInit(struct_name, inits))) }

  | STATIC name=ID AS t=data_type
    { Ast.StaticDim(name, t, None) }
  | STATIC name=ID AS t=data_type EQUALS e=expr
    { Ast.StaticDim(name, t, Some(e)) }
  | STATIC name=ID AS struct_name=ID WITH LBRACE optional_newlines inits=field_init_list RBRACE
    { Ast.StaticDim(name, Ast.Custom(struct_name), Some(Ast.StructInit(struct_name, inits))) }
  | STATIC name=ID LBRACKET size=INT_LIT RBRACKET AS t=data_type
    { Ast.StaticDim(name, Ast.Array(t, size), None) }
  | STATIC name=ID LBRACKET size=INT_LIT RBRACKET AS t=data_type EQUALS e=expr
    { Ast.StaticDim(name, Ast.Array(t, size), Some(e)) }
  | STATIC name=ID LBRACKET RBRACKET AS t=data_type
    { Ast.StaticDim(name, Ast.Array(t, -1), None) }
  | STATIC name=ID LBRACKET RBRACKET AS t=data_type EQUALS e=expr
    {
      match e with
      | Ast.ArrayLit elems -> Ast.StaticDim(name, Ast.Array(t, List.length elems), Some(e))
      | _ -> Ast.StaticDim(name, Ast.Array(t, -1), Some(e))
    }

  | obj=expr; DOT; field=ID; EQUALS; e=expr                  { Ast.FieldAssign(obj, field, e) }
  | base=expr; LBRACKET; idx=expr; RBRACKET; EQUALS; e=expr  { Ast.IndexAssign(base, idx, e) }
  | name=ID EQUALS e=expr                                    { Ast.Assign(name, e) }
  | e=expr                                    { Ast.ExprStatement(e) }
  | RETURN e=option(expr)                     { Ast.Return(e) }
  
  | IF cond=expr THEN mandatory_newlines body=block els=if_else_block END IF
    { Ast.If(cond, body, els) }

  | WHILE cond=expr DO mandatory_newlines body=block END WHILE
    { Ast.While(cond, body) }

  | FOR var=ID EQUALS start=expr TO finish=expr mandatory_newlines body=block END FOR
    { Ast.For(var, start, finish, body) }

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

call_arg:
  | BYREF e=expr { (Ast.ByRef, e) }
  | BYVAL e=expr { (Ast.ByVal, e) }
  | e=expr       { (Ast.ByVal, e) }
;

field_init:
  | field=ID EQUALS value=expr { (field, value) }
;

field_init_list:
  | /* empty */                                                                  { [] }
  | fi=field_init optional_newlines                                              { [fi] }
  | fi=field_init optional_newlines COMMA optional_newlines rest=field_init_list { fi :: rest }
;

expr:
  | id=ID                                                   { Ast.Id(id) }
  | i=INT_LIT                                               { Ast.IntLit(i) }
  | f=FLOAT_LIT                                             { Ast.FloatLit(f) }
  | b=BOOLEAN_LIT                                           { Ast.BooleanLit(b) }
  | s=STRING_LIT                                            { Ast.StringLit(s) }
  | LPAREN e=expr RPAREN                                    { e }
  | NOT e=expr                                              { Ast.UnaryOp("Not", e) }
  | name=ID LPAREN args=separated_list(COMMA, call_arg) RPAREN  { Ast.Call(name, args) }
  | name=ID WITH LBRACE optional_newlines inits=field_init_list RBRACE { Ast.StructInit(name, inits) }
  | SIZEOF LPAREN t=data_type RPAREN { Ast.SizeOf(t) }
  | HEAP_ALLOC LPAREN t=data_type RPAREN { Ast.HeapAlloc(t, Ast.IntLit(1)) }
  | HEAP_ALLOC LPAREN t=data_type COMMA count=expr RPAREN { Ast.HeapAlloc(t, count) }
  | HEAP_REALLOC LPAREN p=expr COMMA t=data_type COMMA count=expr RPAREN { Ast.HeapRealloc(p, t, count) }
  | ARENA_ALLOC LPAREN a=call_arg COMMA t=data_type RPAREN { Ast.ArenaAlloc(fst a, snd a, t, Ast.IntLit(1)) }
  | ARENA_ALLOC LPAREN a=call_arg COMMA t=data_type COMMA count=expr RPAREN { Ast.ArenaAlloc(fst a, snd a, t, count) }
  | LBRACKET optional_newlines elems=separated_list(COMMA, expr) optional_newlines RBRACKET { Ast.ArrayLit(elems) }
  
  /* FieldAccess & IndexAccess */
  | e1=expr DOT field=ID              { Ast.FieldAccess(e1, field) }
  | e1=expr LBRACKET e2=expr RBRACKET { Ast.IndexAccess(e1, e2) }
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

