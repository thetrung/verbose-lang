open VerboseLang
let () =
  if Array.length Sys.argv < 2 then
    print_endline "Usage: dune exec bin/main.exe -- <filename>.vb"
  else
    let filename = Sys.argv.(1) in
    let in_channel = open_in filename in
    let lexbuf = Lexing.from_channel in_channel in
    
    (* 1. Perform ALL operations that read the file inside the try block *)
    try
      let ast = Parser.program Lexer.tokenize lexbuf in
      (* print_endline " --------------------------------------"; *)
      print_endline " --------- Parsed Source Code ---------";
      (* print_endline " --------------------------------------"; *)
      (* Safely close the input file pointer immediately after a successful parse *)
      close_in in_channel;
      print_endline (Printer.print_program ast);
      
      (* Generate and print our clean text patching string output *)
      let ir_output = Codegen.generate_program ast in
      (* print_endline "; --- GENERATED LLVM IR CODE ---"; *)
      (* print_endline ir_output *)
     
      (* print_endline " --------------------------------------"; *)
      print_endline " -------- Executed Code Result --------";
      (* print_endline " --------------------------------------"; *)
      let binary = (List.nth (String.split_on_char '.' filename) 0) in 
      let f_output = binary ^ ".ll" in
      Out_channel.with_open_text f_output (fun channel -> 
      Out_channel.output_string channel ir_output );
      let llc   = Sys.command (Printf.sprintf "llc %s.ll" binary) in
      let clang = Sys.command (Printf.sprintf "clang %s.s -o %s" binary binary) in
      let exec  = Sys.command (Printf.sprintf "./%s" binary) in
      print_endline "\n ------- End of Verbose Compiler -------";
      if llc + clang + exec != 0 then 
        Printf.printf "Status: llc/clang/exec = %d %d %d\n" llc clang exec
      else 
        ignore ()

    with
    | Lexer.SyntaxError msg ->
        (* Use a try-catch pattern to prevent double-closing descriptors *)
        (try close_in in_channel with _ -> ());
        Printf.eprintf "Lexer Error: %s\n" msg;
        exit 1
        
    | Parser.Error ->
        let pos = lexbuf.lex_curr_p in
        (try close_in in_channel with _ -> ());
        Printf.eprintf "Parser Error at line %d, character %d\n" 
          pos.pos_lnum (pos.pos_cnum - pos.pos_bol);
        exit 1
        
    | e ->
        (try close_in in_channel with _ -> ());
        Printf.eprintf "Unexpected Error: %s\n" (Printexc.to_string e);
        exit 1
