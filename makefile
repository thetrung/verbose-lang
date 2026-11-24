TARGET=prototype
.PHONY: default build build-stage0 run demo test test-stage0 test-all clean

default: build run

# Build self-hosted ./verbose via Stage 0 -> Stage 1 -> Stage 2 -> Stage 3 fixpoint verification
build:
	@if [ ! -x stage0/verbose ]; then $(MAKE) build-stage0; fi
	@echo "=== Stage 1: stage0/verbose -> ./verbose1 ==="
	./stage0/verbose src/verbose.vb --no-run -o verbose1
	@echo "=== Stage 2: verbose1 self-compiles src/verbose.vb -> ./verbose (Stage 2) ==="
	./verbose1 src/verbose.vb verbose2.ll
	clang -O2 verbose2.ll -o verbose
	@echo "=== Stage 3: Fixpoint check (verbose2.ll == verbose3.ll) ==="
	./verbose src/verbose.vb verbose3.ll
	diff -u verbose2.ll verbose3.ll && echo "[OK] Stage 3 self-hosting fixpoint verified (byte-for-byte identical)!"
	@rm -f verbose1 src/verbose.ll verbose.ll verbose2.ll verbose3.ll
	@echo "=== Running Self-Hosting Feature Suite ==="
	python3 test/run_bootstrap_tests.py
	@echo "[READY] Self-hosted compiler binary built at ./verbose"

# Rebuild the Stage 0 OCaml compiler into stage0/verbose
build-stage0:
	cd stage0 && dune build
	mv -f stage0/_build/default/bin/main.exe stage0/verbose

# Compile and run $(TARGET).vb using self-hosted ./verbose
run:
	@if [ ! -x ./verbose ]; then $(MAKE) build; fi
	./verbose ${TARGET}.vb ${TARGET}.ll
	clang -O2 ${TARGET}.ll -o ${TARGET}
	./${TARGET}

# Compile and run all standalone examples using self-hosted ./verbose
demo:
	@if [ ! -x ./verbose ]; then $(MAKE) build; fi
	@for f in \
		examples/enum.vb \
		examples/compute.vb \
		examples/boolean.vb \
		examples/byref.vb \
		examples/loop_while.vb \
		examples/numberic.vb \
		examples/read_file.vb \
		examples/structure.vb \
		examples/structure2.vb \
		examples/nested_struct.vb \
		examples/arrays.vb \
		examples/static_heap.vb \
		examples/arena.vb; do \
		echo "=== Running $$f ==="; \
		./verbose "$$f" /tmp/vb_demo.ll && clang -O2 /tmp/vb_demo.ll -o /tmp/vb_demo && /tmp/vb_demo || exit 1; \
	done
	@rm -f /tmp/vb_demo.ll /tmp/vb_demo

# Test self-hosted ./verbose across all 68 positive tests & examples
test:
	@if [ ! -x ./verbose ]; then $(MAKE) build; fi
	python3 test/run_verbose_tests.py

# Test Stage 0 OCaml compiler across all 9 regression suites (92 tests)
test-stage0:
	@if [ ! -x stage0/verbose ]; then $(MAKE) build-stage0; fi
	python3 test/run_syntax_tests.py
	python3 test/run_byref_tests.py
	python3 test/run_array_tests.py
	python3 test/run_static_heap_tests.py
	python3 test/run_arena_tests.py
	python3 test/run_ta_tests.py
	python3 test/run_module_tests.py
	python3 test/run_c_interop_tests.py
	python3 test/run_bootstrap_tests.py

# Full verification: self-hosted build + 68/68 ./verbose suite + 92/92 Stage 0 suites
test-all: build test test-stage0

clean:
	rm -rf _build stage0/_build \
		verbose verbose1 verbose.new verbose*.ll src/*.ll \
		${TARGET}.ll ${TARGET}.s ${TARGET} \
		examples/raylib/*.ll examples/raylib/bouncing_ball
	@for dir in test/byref test/syntax test/arrays test/static_heap test/arena test/type_analyzer test/modules test/c_interop; do \
		if [ -d "$$dir" ]; then \
			find "$$dir" -maxdepth 1 -type f ! -name '*.vb' ! -name '*.c' -delete; \
		fi; \
	done
