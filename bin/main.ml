open VerboseLang

type cli_config = {
  mutable filename: string option;
  mutable show_ast: bool;
  mutable show_ir: bool;
  mutable show_ta: bool;
  mutable skip_ta: bool;
  mutable emit_only: bool;
  mutable no_run: bool;
  mutable output_bin: string option;
  mutable clang_flags: string list;
}

let usage =
  "Usage: verbose <filename>.vb [options]\n" ^
  "  --ast        Show parsed AST\n" ^
  "  --ir         Show generated LLVM-IR\n" ^
  "  --ta         Verbose type-analyzer pass (runs automatically before codegen)\n" ^
  "  --no-ta      Skip semantic type-analyzer pass\n" ^
  "  --emit-only  Emit .ll only (skip clang & execution)\n" ^
  "  --no-run     Compile binary with clang without executing it\n" ^
  "  -o <path>    Specify output binary path\n" ^
  "  -l<lib>      Link external C library (e.g. -lm, -lraylib)\n" ^
  "  -L<dir>      Add library search path for clang\n"

let parse_args argv =
  let cfg = {
    filename = None;
    show_ast = false;
    show_ir = false;
    show_ta = false;
    skip_ta = false;
    emit_only = false;
    no_run = false;
    output_bin = None;
    clang_flags = [];
  } in
  let n = Array.length argv in
  let i = ref 1 in
  while !i < n do
    let arg = argv.(!i) in
    (match arg with
     | "--ast" -> cfg.show_ast <- true
     | "--ir" -> cfg.show_ir <- true
     | "--ta" -> cfg.show_ta <- true
     | "--no-ta" -> cfg.skip_ta <- true
     | "--emit-only" -> cfg.emit_only <- true
     | "--no-run" -> cfg.no_run <- true
     | "-o" when !i + 1 < n ->
         incr i;
         cfg.output_bin <- Some argv.(!i)
     | s when String.length s >= 2 && (String.sub s 0 2 = "-l" || String.sub s 0 2 = "-L" || String.sub s 0 2 = "-W") ->
         cfg.clang_flags <- cfg.clang_flags @ [s]
     | s when String.length s > 0 && s.[0] = '-' ->
         Printf.eprintf "Unknown option: %s\n%s" s usage;
         exit 1
     | s ->
         if cfg.filename = None then cfg.filename <- Some s
         else begin
           Printf.eprintf "Unexpected argument: %s\n%s" s usage;
           exit 1
         end);
    incr i
  done;
  cfg

let strip_vb_ext path =
  if Filename.check_suffix path ".vb" then Filename.chop_suffix path ".vb"
  else path

let () =
  if Array.length Sys.argv < 2 then begin
    print_endline usage;
    exit 0
  end;
  let cfg = parse_args Sys.argv in
  let filename =
    match cfg.filename with
    | Some f -> f
    | None ->
        print_endline usage;
        exit 1
  in
  let in_channel = open_in filename in
  let lexbuf = Lexing.from_channel in_channel in
  try
    Printf.printf "\n -------- Verbose-Lang Compiler -------\n";
    Printf.printf " build: %s\n" Version.build_time;
    Printf.printf " version: %s/%s\n\n" Version.branch Version.commit_id;

    (* 1. Parse source code *)
    let ast = Parser.program Lexer.tokenize lexbuf in
    close_in in_channel;

    if cfg.show_ast then begin
      print_endline "\n --------- Parsed Source Code ---------";
      print_endline (Printer.print_program ast)
    end;

    (* 2. Run Type Analyzer BEFORE codegen *)
    if not cfg.skip_ta then begin
      Type_analyzer.analyze_program ast;
      if cfg.show_ta then
        print_endline " [OK] Type Analyzer: semantic check passed."
    end;

    (* 3. Generate LLVM IR *)
    let ir_output = Codegen.generate_program ast in

    if cfg.show_ir then begin
      print_endline "\n ------- GENERATED LLVM IR CODE -------";
      print_endline ir_output
    end;

    let base = strip_vb_ext filename in
    let binary =
      match cfg.output_bin with
      | Some out -> out
      | None -> base
    in
    let f_output = base ^ ".ll" in
    Out_channel.with_open_text f_output (fun channel ->
      Out_channel.output_string channel ir_output
    );

    if not cfg.emit_only then begin
      let extra =
        if cfg.clang_flags = [] then ""
        else " " ^ String.concat " " (List.map Filename.quote cfg.clang_flags)
      in
      let clang_cmd = Printf.sprintf "clang -O2 %s -o %s%s" (Filename.quote f_output) (Filename.quote binary) extra in
      let clang = Sys.command clang_cmd in
      if cfg.no_run then begin
        if clang <> 0 then begin
          Printf.printf "Status: clang = %d\n" clang;
          exit 1
        end
      end else begin
        print_endline " -------- Executed Code Result --------";
        let run_path =
          if Filename.is_relative binary && not (String.contains binary '/') then "./" ^ binary
          else binary
        in
        let exec = if clang = 0 then Sys.command (Filename.quote run_path) else 1 in
        print_endline " ---------------------------------------";
        print_endline "";
        if clang + exec <> 0 then begin
          Printf.printf "Status: clang/exec = %d %d\n" clang exec;
          exit 1
        end
      end
    end
  with
  | Lexer.SyntaxError msg ->
      (try close_in in_channel with _ -> ());
      Printf.eprintf "Lexer Error: %s\n" msg;
      exit 1
  | Parser.Error ->
      let pos = lexbuf.lex_curr_p in
      (try close_in in_channel with _ -> ());
      Printf.eprintf "Parser Error at line %d, character %d\n"
        pos.pos_lnum (pos.pos_cnum - pos.pos_bol);
      exit 1
  | Type_analyzer.TypeError msg ->
      (try close_in in_channel with _ -> ());
      Printf.eprintf "Semantic Type Error: %s\n" msg;
      exit 1
  | Codegen.Error msg ->
      (try close_in in_channel with _ -> ());
      Printf.eprintf "Backend Codegen Error: %s\n" msg;
      exit 1
  | e ->
      (try close_in in_channel with _ -> ());
      Printf.eprintf "Unexpected Error: %s\n" (Printexc.to_string e);
      exit 1
