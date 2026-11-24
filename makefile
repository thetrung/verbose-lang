TARGET=prototype
.PHONY: default build build-stage0 run demo bootstrap test test-vbc test-stage0 clean

default: build run

# Build the self-hosted compiler (./vbc).
# If ./vbc already exists, self-compiles from vbc.vb; otherwise bootstraps from stage0/verbose.
build:
	@if [ -x ./vbc ]; then \
		echo "=== Self-compiling ./vbc from vbc.vb ==="; \
		./vbc vbc.vb vbc.ll && clang -O2 vbc.ll -o vbc.new && mv -f vbc.new vbc && rm -f vbc.ll; \
		echo "[READY] Self-hosted compiler rebuilt at ./vbc"; \
	else \
		$(MAKE) bootstrap; \
	fi

# Rebuild the Stage 0 OCaml compiler into stage0/verbose
build-stage0:
	cd stage0 && dune build
	mv -f stage0/_build/default/bin/main.exe stage0/verbose

# Full Stage 0 -> Stage 1 -> Stage 2 -> Stage 3 self-hosting bootstrap & fixpoint verification
bootstrap:
	@if [ ! -x stage0/verbose ]; then $(MAKE) build-stage0; fi
	@echo "=== Stage 1: stage0/verbose -> ./vbc1 ==="
	./stage0/verbose vbc.vb --no-run -o vbc1
	@echo "=== Stage 2: vbc1 self-compiles vbc.vb -> ./vbc (Stage 2) ==="
	./vbc1 vbc.vb vbc2.ll
	clang -O2 vbc2.ll -o vbc
	@echo "=== Stage 3: Fixpoint check (vbc2.ll == vbc3.ll) ==="
	./vbc vbc.vb vbc3.ll
	diff -u vbc2.ll vbc3.ll && echo "[OK] Stage 3 self-hosting fixpoint verified (byte-for-byte identical)!"
	@rm -f vbc1 vbc.ll vbc2.ll vbc3.ll
	@echo "=== Running Bootstrap Self-Hosting Suite ==="
	python3 test/run_bootstrap_tests.py
	@echo "[READY] Self-hosted compiler binary built at ./vbc"

# Compile and run $(TARGET).vb using self-hosted ./vbc
run: build
	./vbc ${TARGET}.vb ${TARGET}.ll
	clang -O2 ${TARGET}.ll -o ${TARGET}
	./${TARGET}

# Compile and run all standalone examples using self-hosted ./vbc
demo: build
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
		./vbc "$$f" /tmp/vb_demo.ll && clang -O2 /tmp/vb_demo.ll -o /tmp/vb_demo && /tmp/vb_demo || exit 1; \
	done
	@rm -f /tmp/vb_demo.ll /tmp/vb_demo

# Test self-hosted ./vbc across all 68 positive tests & examples
test-vbc:
	@if [ ! -x ./vbc ]; then $(MAKE) bootstrap; fi
	python3 test/run_vbc_compat_tests.py

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

# Full verification: Stage 1-3 bootstrap fixpoint + 68/68 vbc compat + 92/92 Stage 0 suites
test: bootstrap test-vbc test-stage0

clean:
	rm -rf _build stage0/_build \
		vbc vbc1 vbc.new vbc*.ll \
		${TARGET}.ll ${TARGET}.s ${TARGET} \
		examples/raylib/*.ll examples/raylib/bouncing_ball
	@for dir in test/byref test/syntax test/arrays test/static_heap test/arena test/type_analyzer test/modules test/c_interop; do \
		if [ -d "$$dir" ]; then \
			find "$$dir" -maxdepth 1 -type f ! -name '*.vb' ! -name '*.c' -delete; \
		fi; \
	done
