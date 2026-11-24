TARGET=prototype
PREFIX ?= /usr/local
DESTDIR ?=
VERSION ?= 0.2.0
BOOTSTRAP ?=
RELEASE_URL ?= https://github.com/thetrung/verbose-lang/releases/latest/download/verbose

.PHONY: default build run demo test test-all install uninstall dist clean

default: build run

# Build self-hosted ./verbose via Stage 1 -> Stage 2 -> Stage 3 fixpoint verification
# Uses BOOTSTRAP if set, else existing ./verbose, else `verbose` in PATH, else downloads the latest release binary.
# (To rebuild from the archival OCaml Stage 0 compiler, see the `stage0` git branch.)
build:
	@BOOT="$(BOOTSTRAP)"; \
	if [ -n "$$BOOT" ] && [ -x "$$BOOT" ]; then \
		echo "=== Bootstrap compiler: $$BOOT ==="; \
	elif [ -x ./verbose ]; then \
		cp ./verbose ./.verbose.boot; \
		BOOT="./.verbose.boot"; \
		echo "=== Bootstrap compiler: existing ./verbose ==="; \
	elif command -v verbose >/dev/null 2>&1; then \
		BOOT="$$(command -v verbose)"; \
		echo "=== Bootstrap compiler: $$BOOT (from PATH) ==="; \
	else \
		echo "=== Downloading bootstrap compiler from $(RELEASE_URL) ==="; \
		curl -fsSL "$(RELEASE_URL)" -o ./.verbose.boot || { \
			echo "Error: failed to download bootstrap binary. Pass BOOTSTRAP=/path/to/verbose or build from the 'stage0' branch."; \
			exit 1; \
		}; \
		chmod +x ./.verbose.boot; \
		BOOT="./.verbose.boot"; \
	fi; \
	echo "=== Stage 1: $$BOOT -> ./verbose1 ==="; \
	"$$BOOT" src/verbose.vb --emit-only -o verbose1.ll && clang -O2 verbose1.ll -o verbose1 || exit 1
	@echo "=== Stage 2: verbose1 self-compiles src/verbose.vb -> ./verbose (Stage 2) ==="
	./verbose1 src/verbose.vb --emit-only -o verbose2.ll
	clang -O2 verbose2.ll -o verbose
	@echo "=== Stage 3: Fixpoint check (verbose2.ll == verbose3.ll) ==="
	./verbose src/verbose.vb --emit-only -o verbose3.ll
	diff -u verbose2.ll verbose3.ll && echo "[OK] Stage 3 self-hosting fixpoint verified (byte-for-byte identical)!"
	@rm -f ./.verbose.boot verbose1 src/verbose.ll verbose.ll verbose1.ll verbose2.ll verbose3.ll
	@echo "=== Running Self-Hosting Feature Suite ==="
	VERBOSE_COMPILER=./verbose python3 test/run_bootstrap_tests.py
	@echo "[READY] Self-hosted compiler binary built at ./verbose"

# Compile and directly execute $(TARGET).vb using self-hosted ./verbose
run:
	@if [ ! -x ./verbose ]; then $(MAKE) build; fi
	./verbose ${TARGET}.vb

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
		./verbose "$$f" -o /tmp/vb_demo || exit 1; \
		rm -f "$${f%.vb}.ll"; \
	done
	@rm -f /tmp/vb_demo

# Full self-hosted ./verbose test suite (CLI + 14 examples + all 8 regression suites = 105 tests)
test:
	@if [ ! -x ./verbose ]; then $(MAKE) build; fi
	VERBOSE_COMPILER=./verbose python3 test/run_verbose_tests.py
	VERBOSE_COMPILER=./verbose python3 test/run_syntax_tests.py
	VERBOSE_COMPILER=./verbose python3 test/run_byref_tests.py
	VERBOSE_COMPILER=./verbose python3 test/run_array_tests.py
	VERBOSE_COMPILER=./verbose python3 test/run_static_heap_tests.py
	VERBOSE_COMPILER=./verbose python3 test/run_arena_tests.py
	VERBOSE_COMPILER=./verbose python3 test/run_ta_tests.py
	VERBOSE_COMPILER=./verbose python3 test/run_module_tests.py
	VERBOSE_COMPILER=./verbose python3 test/run_c_interop_tests.py

# Full verification: self-hosted build fixpoint + ./verbose test suite + standalone demos
test-all: build test demo

# Install ./verbose into $(DESTDIR)$(PREFIX)/bin/verbose
install:
	@if [ ! -x ./verbose ]; then $(MAKE) build; fi
	install -Dm755 ./verbose "$(DESTDIR)$(PREFIX)/bin/verbose"
	@echo "[INSTALLED] $(DESTDIR)$(PREFIX)/bin/verbose"

uninstall:
	rm -f "$(DESTDIR)$(PREFIX)/bin/verbose"
	@echo "[UNINSTALLED] $(DESTDIR)$(PREFIX)/bin/verbose"

# Package standalone release artifacts in dist/
dist:
	@if [ ! -x ./verbose ]; then $(MAKE) build; fi
	@mkdir -p dist
	cp ./verbose dist/verbose
	cp ./verbose dist/verbose-linux-x86_64
	strip dist/verbose dist/verbose-linux-x86_64 2>/dev/null || true
	cd dist && sha256sum verbose verbose-linux-x86_64 > SHA256SUMS.txt
	@ls -lh dist/

clean:
	rm -rf _build dist \
		verbose verbose1 verbose.new .verbose.boot verbose*.ll src/*.ll \
		${TARGET}.ll ${TARGET}.s ${TARGET} \
		examples/*.ll examples/raylib/bouncing_ball.ll examples/raylib/raylib.ll examples/raylib/bouncing_ball
	@for dir in test/byref test/syntax test/arrays test/static_heap test/arena test/type_analyzer test/modules test/c_interop; do \
		if [ -d "$$dir" ]; then \
			find "$$dir" -maxdepth 1 -type f ! -name '*.vb' ! -name '*.c' -delete; \
		fi; \
	done
