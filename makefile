TARGET ?= prototype
PREFIX ?= /usr/local
DESTDIR ?=
VERSION ?= 0.5.1
BOOTSTRAP ?=

UNAME_S := $(shell uname -s 2>/dev/null || echo Linux)
UNAME_M := $(shell uname -m 2>/dev/null || echo x86_64)

ifeq ($(UNAME_S),Darwin)
  PLATFORM ?= macos-arm64
  EXE :=
  STRIP_CMD := strip -x
  SHA_CMD := shasum -a 256
else ifneq (,$(findstring MINGW,$(UNAME_S))$(findstring MSYS,$(UNAME_S))$(findstring CYGWIN,$(UNAME_S)))
  PLATFORM ?= windows-x86_64
  EXE := .exe
  STRIP_CMD := strip
  SHA_CMD := sha256sum
else ifneq (,$(filter aarch64 arm64,$(UNAME_M)))
  PLATFORM ?= linux-arm64
  EXE :=
  STRIP_CMD := strip
  SHA_CMD := sha256sum
else
  PLATFORM ?= linux-x86_64
  EXE :=
  STRIP_CMD := strip
  SHA_CMD := sha256sum
endif

BIN := verbose$(EXE)
DIST_NAME := verbose-$(PLATFORM)$(EXE)
RELEASE_URL ?= https://github.com/thetrung/verbose-lang/releases/latest/download/$(DIST_NAME)

.PHONY: default build run test test-all install uninstall dist clean

default: build run

# Build self-hosted ./verbose via Stage 1 -> Stage 2 -> Stage 3 fixpoint verification
# Uses BOOTSTRAP if set, else existing ./verbose, else `verbose` in PATH, else downloads the latest release binary.
build:
	@BOOT="$(BOOTSTRAP)"; \
	if [ -n "$$BOOT" ] && [ -x "$$BOOT" ]; then \
		echo "=== Bootstrap compiler: $$BOOT ==="; \
	elif [ -x ./$(BIN) ]; then \
		cp ./$(BIN) ./.verbose.boot$(EXE); \
		BOOT="./.verbose.boot$(EXE)"; \
		echo "=== Bootstrap compiler: existing ./$(BIN) ==="; \
	elif command -v verbose >/dev/null 2>&1; then \
		BOOT="$$(command -v verbose)"; \
		echo "=== Bootstrap compiler: $$BOOT (from PATH) ==="; \
	else \
		echo "=== Downloading bootstrap compiler from $(RELEASE_URL) ==="; \
		curl -fsSL "$(RELEASE_URL)" -o ./.verbose.boot$(EXE) || { \
			echo "Error: failed to download bootstrap binary for $(PLATFORM). Pass BOOTSTRAP=/path/to/verbose."; \
			exit 1; \
		}; \
		chmod +x ./.verbose.boot$(EXE); \
		BOOT="./.verbose.boot$(EXE)"; \
	fi; \
	echo "=== Stage 1 ($(PLATFORM)): $$BOOT -> ./verbose1$(EXE) ==="; \
	"$$BOOT" src/verbose.vb --emit-only -o verbose1.ll && clang -O2 verbose1.ll -o verbose1$(EXE) || exit 1
	@echo "=== Stage 2 ($(PLATFORM)): verbose1$(EXE) self-compiles src/verbose.vb -> ./$(BIN) ==="
	./verbose1$(EXE) src/verbose.vb --emit-only -o verbose2.ll
	clang -O2 verbose2.ll -o $(BIN)
	@echo "=== Stage 3 ($(PLATFORM)): Fixpoint check (verbose2.ll == verbose3.ll) ==="
	./$(BIN) src/verbose.vb --emit-only -o verbose3.ll
	diff -u verbose2.ll verbose3.ll && echo "[OK] Stage 3 self-hosting fixpoint verified (byte-for-byte identical)!"
	@rm -f ./.verbose.boot ./.verbose.boot.exe verbose1 verbose1.exe src/verbose.ll verbose.ll verbose1.ll verbose2.ll verbose3.ll
	@echo "=== Running Self-Hosting Feature Suite ==="
	./$(BIN) test tests/bootstrap_suite.vb
	@echo "[READY] Self-hosted compiler binary built at ./$(BIN)"

# Compile and directly execute $(TARGET).vb using self-hosted ./verbose
run:
	@if [ ! -x ./$(BIN) ]; then $(MAKE) build; fi
	./$(BIN) $(TARGET).vb

# Run all pure-VerBose Test/Assert suites under tests/
test:
	@if [ ! -x ./$(BIN) ]; then $(MAKE) build; fi
	./$(BIN) test

# Full verification: self-hosted build fixpoint + full test suite
test-all: build test

# Install ./verbose into $(DESTDIR)$(PREFIX)/bin/verbose
install:
	@if [ ! -x ./$(BIN) ]; then $(MAKE) build; fi
	install -d "$(DESTDIR)$(PREFIX)/bin"
	install -m 755 ./$(BIN) "$(DESTDIR)$(PREFIX)/bin/$(BIN)"
	@echo "[INSTALLED] $(DESTDIR)$(PREFIX)/bin/$(BIN)"

uninstall:
	rm -f "$(DESTDIR)$(PREFIX)/bin/$(BIN)"
	@echo "[UNINSTALLED] $(DESTDIR)$(PREFIX)/bin/$(BIN)"

# Package standalone release artifacts in dist/ for the current host platform
dist:
	@if [ ! -x ./$(BIN) ]; then $(MAKE) build; fi
	@mkdir -p dist
	cp ./$(BIN) dist/$(DIST_NAME)
	$(STRIP_CMD) dist/$(DIST_NAME) 2>/dev/null || true
	@if [ "$(PLATFORM)" = "linux-x86_64" ]; then \
		cp dist/$(DIST_NAME) dist/verbose; \
		cd dist && $(SHA_CMD) verbose $(DIST_NAME) > SHA256SUMS.txt; \
	else \
		cd dist && $(SHA_CMD) $(DIST_NAME) > SHA256SUMS.txt; \
	fi
	@ls -lh dist/

clean:
	rm -rf dist cross-ir \
		verbose verbose.exe verbose1 verbose1.exe verbose.new .verbose.boot .verbose.boot.exe verbose*.ll src/*.ll \
		$(TARGET).ll $(TARGET).s $(TARGET) $(TARGET).exe \
		tests/raylib/bouncing_ball.ll tests/raylib/raylib.ll tests/raylib/bouncing_ball
	@for dir in tests/byref tests/syntax tests/arrays tests/static_heap tests/arena tests/type_analyzer tests/modules tests/c_interop; do \
		if [ -d "$$dir" ]; then \
			find "$$dir" -maxdepth 1 -type f ! -name '*.vb' ! -name '*.c' -delete; \
		fi; \
	done
	@find tests -maxdepth 2 -type f \( -name '*.o' -o -name '*.ll' -o -name '*.s' \) ! -name 'camera3d_draft.ll' -delete 2>/dev/null || true
