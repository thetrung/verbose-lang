open Ast
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

