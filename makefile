TARGET ?= prototype
PREFIX ?= /usr/local
DESTDIR ?=
VERSION ?= 0.3.0
BOOTSTRAP ?=
RELEASE_URL ?= https://github.com/thetrung/verbose-lang/releases/latest/download/verbose

.PHONY: default build run test test-all install uninstall dist clean

default: build run

# Build self-hosted ./verbose via Stage 1 -> Stage 2 -> Stage 3 fixpoint verification
# Uses BOOTSTRAP if set, else existing ./verbose, else `verbose` in PATH, else downloads the latest release binary.
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
			echo "Error: failed to download bootstrap binary. Pass BOOTSTRAP=/path/to/verbose."; \
			exit 1; \
		}; \
		chmod +x ./.verbose.boot; \
		BOOT="./.verbose.boot"; \
	fi; \
	echo "=== Stage 1: $$BOOT -> ./verbose1 ==="; \
	"$$BOOT" src/verbose.vb --emit-only -o verbose1.ll && clang -O2 verbose1.ll -o verbose1 || exit 1
	@echo "=== Stage 2: verbose1 self-compiles src/verbose.vb -> ./verbose ==="
	./verbose1 src/verbose.vb --emit-only -o verbose2.ll
	clang -O2 verbose2.ll -o verbose
	@echo "=== Stage 3: Fixpoint check (verbose2.ll == verbose3.ll) ==="
	./verbose src/verbose.vb --emit-only -o verbose3.ll
	diff -u verbose2.ll verbose3.ll && echo "[OK] Stage 3 self-hosting fixpoint verified (byte-for-byte identical)!"
	@rm -f ./.verbose.boot verbose1 src/verbose.ll verbose.ll verbose1.ll verbose2.ll verbose3.ll
	@echo "=== Running Self-Hosting Feature Suite ==="
	./verbose test tests/bootstrap_suite.vb
	@echo "[READY] Self-hosted compiler binary built at ./verbose"

# Compile and directly execute $(TARGET).vb using self-hosted ./verbose
run:
	@if [ ! -x ./verbose ]; then $(MAKE) build; fi
	./verbose $(TARGET).vb

# Run all pure-VerBose Test/Assure suites under tests/
test:
	@if [ ! -x ./verbose ]; then $(MAKE) build; fi
	./verbose test

# Full verification: self-hosted build fixpoint + full test suite
test-all: build test

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
	rm -rf dist \
		verbose verbose1 verbose.new .verbose.boot verbose*.ll src/*.ll \
		$(TARGET).ll $(TARGET).s $(TARGET) \
		tests/raylib/bouncing_ball.ll tests/raylib/raylib.ll tests/raylib/bouncing_ball
	@for dir in tests/byref tests/syntax tests/arrays tests/static_heap tests/arena tests/type_analyzer tests/modules tests/c_interop; do \
		if [ -d "$$dir" ]; then \
			find "$$dir" -maxdepth 1 -type f ! -name '*.vb' ! -name '*.c' -delete; \
		fi; \
	done
