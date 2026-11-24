open Ast

exception LoadError of string

(* Pure OCaml canonical path normalization (handles relative paths, '.', '..') *)
let normalize_path path =
  let abs_path =
    if Filename.is_relative path then Filename.concat (Sys.getcwd ()) path
    else path
  in
  let parts = String.split_on_char '/' abs_path in
  let rec resolve acc = function
    | [] -> List.rev acc
    | "" :: rest | "." :: rest -> resolve acc rest
    | ".." :: rest ->
        let acc' = match acc with _ :: tl -> tl | [] -> [] in
        resolve acc' rest
    | seg :: rest -> resolve (seg :: acc) rest
  in
  "/" ^ String.concat "/" (resolve [] parts)

let resolve_include ~from_file ~include_dirs raw_path =
  let base_dir = Filename.dirname from_file in
  let candidates =
    if not (Filename.is_relative raw_path) then [raw_path]
    else
      Filename.concat base_dir raw_path
      :: List.map (fun dir -> Filename.concat dir raw_path) include_dirs
  in
  let rec find = function
    | [] ->
        raise (LoadError (Printf.sprintf "Included file not found: %S (included from %s)" raw_path from_file))
    | p :: rest ->
        if Sys.file_exists p && not (Sys.is_directory p) then normalize_path p
        else find rest
  in
  find candidates

let parse_file path =
  let ic =
    try open_in path
    with Sys_error msg -> raise (LoadError ("Cannot open file '" ^ path ^ "': " ^ msg))
  in
  let lexbuf = Lexing.from_channel ic in
  try
    let ast = Parser.program Lexer.tokenize lexbuf in
    close_in ic;
    ast
  with
  | Lexer.SyntaxError msg ->
      (try close_in ic with _ -> ());
      let pos = lexbuf.lex_curr_p in
      raise (LoadError (Printf.sprintf "Lexer Error in %s at line %d: %s" path pos.pos_lnum msg))
  | Parser.Error ->
      (try close_in ic with _ -> ());
      let pos = lexbuf.lex_curr_p in
      raise (LoadError (Printf.sprintf "Parser Error in %s at line %d, character %d"
                          path pos.pos_lnum (pos.pos_cnum - pos.pos_bol)))
  | e ->
      (try close_in ic with _ -> ());
      raise e

(* Alpha-rename Private definitions inside an included module to provide true module encapsulation *)
let rename_module_privates mod_id defs =
  let prefix = Printf.sprintf "__mod%d_" mod_id in
  let priv_types : (string, string) Hashtbl.t = Hashtbl.create 8 in
  let priv_enums : (string, string) Hashtbl.t = Hashtbl.create 8 in
  let priv_vars  : (string, string) Hashtbl.t = Hashtbl.create 8 in
  let priv_funcs : (string, string) Hashtbl.t = Hashtbl.create 8 in
  let priv_names = ref [] in

  List.iter (function
    | Structure (Private, name, _) ->
        Hashtbl.replace priv_types name (prefix ^ name);
        priv_names := name :: !priv_names
    | EnumDef (Private, name, _) ->
        Hashtbl.replace priv_enums name (prefix ^ name);
        Hashtbl.replace priv_types name (prefix ^ name);
        priv_names := name :: !priv_names
    | GlobalVar (Private, name, _, _) ->
        Hashtbl.replace priv_vars name (prefix ^ name);
        priv_names := name :: !priv_names
    | FuncDef (Private, name, _, _, _) ->
        Hashtbl.replace priv_funcs name (prefix ^ name);
        priv_names := name :: !priv_names
    | _ -> ()
  ) defs;

  if !priv_names = [] then (defs, [])
  else begin
    let rec map_dt = function
      | Custom s ->
          (match Hashtbl.find_opt priv_types s with
           | Some s' -> Custom s'
           | None -> Custom s)
      | Array (et, n) -> Array (map_dt et, n)
      | dt -> dt
    in
    let map_type_name s =
      match Hashtbl.find_opt priv_types s with Some s' -> s' | None -> s
    in
    let rec map_expr shadowed = function
      | Id s ->
          if List.mem s shadowed then Id s
          else if Hashtbl.mem priv_vars s then Id (Hashtbl.find priv_vars s)
          else if Hashtbl.mem priv_enums s then Id (Hashtbl.find priv_enums s)
          else Id s
      | UnaryOp (op, e) -> UnaryOp (op, map_expr shadowed e)
      | BinOp (e1, op, e2) -> BinOp (map_expr shadowed e1, op, map_expr shadowed e2)
      | Call (name, args) ->
          let name' =
            match Hashtbl.find_opt priv_funcs name with
            | Some n' -> n'
            | None ->
                (match Hashtbl.find_opt priv_types name with
                 | Some n' -> n'
                 | None -> name)
          in
          Call (name', List.map (fun (m, e) -> (m, map_expr shadowed e)) args)
      | FieldAccess (Id ename, field) when Hashtbl.mem priv_enums ename && not (List.mem ename shadowed) ->
          FieldAccess (Id (Hashtbl.find priv_enums ename), field)
      | FieldAccess (e, field) -> FieldAccess (map_expr shadowed e, field)
      | IndexAccess (e1, e2) -> IndexAccess (map_expr shadowed e1, map_expr shadowed e2)
      | ArrayLit elems -> ArrayLit (List.map (map_expr shadowed) elems)
      | StructInit (sn, inits) ->
          StructInit (map_type_name sn, List.map (fun (f, e) -> (f, map_expr shadowed e)) inits)
      | SizeOf dt -> SizeOf (map_dt dt)
      | HeapAlloc (dt, c) -> HeapAlloc (map_dt dt, map_expr shadowed c)
      | HeapRealloc (p, dt, c) -> HeapRealloc (map_expr shadowed p, map_dt dt, map_expr shadowed c)
      | ArenaAlloc (m, a, dt, c) -> ArenaAlloc (m, map_expr shadowed a, map_dt dt, map_expr shadowed c)
      | (IntLit _ | FloatLit _ | BooleanLit _ | StringLit _) as lit -> lit
    in
    let rec map_stmts shadowed acc = function
      | [] -> List.rev acc
      | s :: rest ->
          let s', shadowed' =
            match s with
            | Dim (n, dt, opt_e) ->
                let opt_e' = Option.map (map_expr shadowed) opt_e in
                (Dim (n, map_dt dt, opt_e'), n :: shadowed)
            | StaticDim (n, dt, opt_e) ->
                let opt_e' = Option.map (map_expr shadowed) opt_e in
                (StaticDim (n, map_dt dt, opt_e'), n :: shadowed)
            | Assign (n, e) ->
                let n' =
                  if List.mem n shadowed then n
                  else match Hashtbl.find_opt priv_vars n with Some nv -> nv | None -> n
                in
                (Assign (n', map_expr shadowed e), shadowed)
            | AssignExpr (e1, e2) -> (AssignExpr (map_expr shadowed e1, map_expr shadowed e2), shadowed)
            | FieldAssign (e1, f, e2) -> (FieldAssign (map_expr shadowed e1, f, map_expr shadowed e2), shadowed)
            | IndexAssign (e1, e2, e3) ->
                (IndexAssign (map_expr shadowed e1, map_expr shadowed e2, map_expr shadowed e3), shadowed)
            | ExprStatement e -> (ExprStatement (map_expr shadowed e), shadowed)
            | Return opt_e -> (Return (Option.map (map_expr shadowed) opt_e), shadowed)
            | If (c, t_blk, e_blk) ->
                (If (map_expr shadowed c, map_stmts shadowed [] t_blk, map_stmts shadowed [] e_blk), shadowed)
            | While (c, body) ->
                (While (map_expr shadowed c, map_stmts shadowed [] body), shadowed)
            | For (v, s_e, f_e, body) ->
                (For (v, map_expr shadowed s_e, map_expr shadowed f_e, map_stmts (v :: shadowed) [] body), shadowed)
            | SelectCase (t, branches, def_opt) ->
                let branches' = List.map (fun (es, b) ->
                  (List.map (map_expr shadowed) es, map_stmts shadowed [] b)
                ) branches in
                let def' = Option.map (map_stmts shadowed []) def_opt in
                (SelectCase (map_expr shadowed t, branches', def'), shadowed)
          in
          map_stmts shadowed' (s' :: acc) rest
    in
    let defs' = List.map (function
      | Include p -> Include p
      | Structure (vis, name, fields) ->
          let name' = if vis = Private then Hashtbl.find priv_types name else name in
          Structure (vis, name', List.map (fun (fn, fdt) -> (fn, map_dt fdt)) fields)
      | EnumDef (vis, name, members) ->
          let name' = if vis = Private then Hashtbl.find priv_enums name else name in
          EnumDef (vis, name', members)
      | GlobalVar (vis, name, dt, opt_e) ->
          let name' = if vis = Private then Hashtbl.find priv_vars name else name in
          GlobalVar (vis, name', map_dt dt, Option.map (map_expr []) opt_e)
      | FuncDef (vis, name, params, ret_dt, body) ->
          let name' = if vis = Private then Hashtbl.find priv_funcs name else name in
          let params' = List.map (fun (m, pn, pdt) -> (m, pn, map_dt pdt)) params in
          let shadowed = List.map (fun (_, pn, _) -> pn) params in
          FuncDef (vis, name', params', map_dt ret_dt, map_stmts shadowed [] body)
      | ExternDecl (vis, name, params, ret_dt, is_var) ->
          let params' = List.map (fun (m, pn, pdt) -> (m, pn, map_dt pdt)) params in
          ExternDecl (vis, name, params', map_dt ret_dt, is_var)
    ) defs in
    (defs', !priv_names)
  end

let load_program ?(include_dirs = []) entry_path =
  let canon_entry = normalize_path entry_path in
  let loaded : (string, unit) Hashtbl.t = Hashtbl.create 16 in
  let mod_counter = ref 0 in
  let hidden_privates : (string, string) Hashtbl.t = Hashtbl.create 16 in

  let rec load_file active depth canon_path =
    if List.mem canon_path active then begin
      let cycle = String.concat " -> " (List.rev (canon_path :: active)) in
      raise (LoadError ("Circular Include detected: " ^ cycle))
    end;
    if Hashtbl.mem loaded canon_path then []
    else begin
      let raw_defs = parse_file canon_path in
      let defs_processed =
        if depth > 0 then begin
          incr mod_counter;
          let renamed, pnames = rename_module_privates !mod_counter raw_defs in
          List.iter (fun n -> Hashtbl.replace hidden_privates n canon_path) pnames;
          renamed
        end else
          raw_defs
      in
      let active' = canon_path :: active in
      let out = ref [] in
      List.iter (function
        | Include rel_path ->
            let target = resolve_include ~from_file:canon_path ~include_dirs rel_path in
            let sub_defs = load_file active' (depth + 1) target in
            out := !out @ sub_defs
        | def ->
            out := !out @ [def]
      ) defs_processed;
      Hashtbl.replace loaded canon_path ();
      !out
    end
  in
  let full_prog = load_file [] 0 canon_entry in
  (full_prog, hidden_privates)
