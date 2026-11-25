TARGET ?= prototype
PREFIX ?= /usr/local
DESTDIR ?=
VERSION ?= 0.6.1
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
BOOTSTRAP_CHAIN ?= bc56415 c6d6f37

.PHONY: default build bootstrap bootstrap-ir run test test-all install uninstall dist pkg-arch clean

default: build run

# Cold-bootstrap ./verbose from checked-in target-neutral verbose.ll using clang only (no existing binary or network needed)
bootstrap:
	@if [ ! -f ./verbose.ll ]; then \
		echo "Error: ./verbose.ll not found. Run 'make build' or pass BOOTSTRAP=/path/to/verbose."; \
		exit 1; \
	fi
	@echo "=== Cold bootstrap ($(PLATFORM)): compiling verbose.ll with clang ==="
	@if [ "$(PLATFORM)" = "windows-x86_64" ]; then \
		sed -e 's/@popen(/@_popen(/g' -e 's/@pclose(/@_pclose(/g' verbose.ll > ./.verbose.boot.ll; \
		clang -O2 -Wno-override-module ./.verbose.boot.ll -o ./.verbose.boot$(EXE); \
		rm -f ./.verbose.boot.ll; \
	else \
		clang -O2 -Wno-override-module verbose.ll -o ./.verbose.boot$(EXE); \
	fi
	@$(MAKE) build BOOTSTRAP=./.verbose.boot$(EXE)

# Refresh the checked-in target-neutral verbose.ll bootstrap IR from ./verbose
bootstrap-ir:
	@if [ ! -x ./$(BIN) ]; then $(MAKE) build; fi
	./$(BIN) --target linux-x86_64 src/verbose.vb --emit-only -o ./.verbose.boot.ll
	grep -v '^target ' ./.verbose.boot.ll > verbose.ll
	@rm -f ./.verbose.boot.ll
	@echo "[OK] Updated target-neutral bootstrap IR at ./verbose.ll"

# Build self-hosted ./verbose via Stage 1 -> Stage 2 -> Stage 3 fixpoint verification
# Uses BOOTSTRAP if set, else existing ./verbose, else `verbose` in PATH, else checked-in verbose.ll, else downloads the latest release binary.
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
	elif [ -f ./verbose.ll ]; then \
		echo "=== Bootstrap compiler: compiling checked-in verbose.ll ($(PLATFORM)) ==="; \
		if [ "$(PLATFORM)" = "windows-x86_64" ]; then \
			sed -e 's/@popen(/@_popen(/g' -e 's/@pclose(/@_pclose(/g' verbose.ll > ./.verbose.boot.ll; \
			clang -O2 -Wno-override-module ./.verbose.boot.ll -o ./.verbose.boot$(EXE); \
			rm -f ./.verbose.boot.ll; \
		else \
			clang -O2 -Wno-override-module verbose.ll -o ./.verbose.boot$(EXE); \
		fi; \
		BOOT="./.verbose.boot$(EXE)"; \
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
	if ! ("$$BOOT" src/verbose.vb --emit-only -o verbose1.ll && clang -O2 verbose1.ll -o verbose1$(EXE)); then \
		if [ -n "$(BOOTSTRAP_CHAIN)" ] && git rev-parse --is-inside-work-tree >/dev/null 2>&1; then \
			echo "=== Direct bootstrap failed; stepping through BOOTSTRAP_CHAIN: $(BOOTSTRAP_CHAIN) ==="; \
			CHAIN_TMP="$$(mktemp -d)"; \
			CUR_BOOT="$$BOOT"; \
			STEP=0; \
			for REV in $(BOOTSTRAP_CHAIN); do \
				STEP=$$((STEP + 1)); \
				STEP_DIR="$$CHAIN_TMP/step$$STEP"; \
				mkdir -p "$$STEP_DIR"; \
				echo "=== Bootstrap step $$STEP ($$REV) ==="; \
				git archive "$$REV" src | tar -x -C "$$STEP_DIR" || { rm -rf "$$CHAIN_TMP"; exit 1; }; \
				"$$CUR_BOOT" "$$STEP_DIR/src/verbose.vb" --emit-only -o "$$STEP_DIR/boot.ll" && clang -O2 "$$STEP_DIR/boot.ll" -o "$$STEP_DIR/boot$(EXE)" || { rm -rf "$$CHAIN_TMP"; exit 1; }; \
				CUR_BOOT="$$STEP_DIR/boot$(EXE)"; \
			done; \
			echo "=== Stage 1 ($(PLATFORM)) via stepped bootstrap -> ./verbose1$(EXE) ==="; \
			"$$CUR_BOOT" src/verbose.vb --emit-only -o verbose1.ll && clang -O2 verbose1.ll -o verbose1$(EXE) || { rm -rf "$$CHAIN_TMP"; exit 1; }; \
			rm -rf "$$CHAIN_TMP"; \
		else \
			exit 1; \
		fi; \
	fi
	@echo "=== Stage 2 ($(PLATFORM)): verbose1$(EXE) self-compiles src/verbose.vb -> ./$(BIN) ==="
	./verbose1$(EXE) src/verbose.vb --emit-only -o verbose2.ll
	clang -O2 verbose2.ll -o $(BIN)
	@echo "=== Stage 3 ($(PLATFORM)): Fixpoint check (verbose2.ll == verbose3.ll) ==="
	./$(BIN) src/verbose.vb --emit-only -o verbose3.ll
	diff -u verbose2.ll verbose3.ll && echo "[OK] Stage 3 self-hosting fixpoint verified (byte-for-byte identical)!"
	@rm -f ./.verbose.boot ./.verbose.boot.exe ./.verbose.boot.ll verbose1 verbose1.exe src/verbose.ll verbose1.ll verbose2.ll verbose3.ll
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

# Build Arch Linux .pkg.tar.zst and pacman repo DB in packaging/arch/ from the current working tree
pkg-arch:
	@command -v makepkg >/dev/null 2>&1 || { echo "Error: makepkg not found (Arch Linux required)."; exit 1; }
	@rm -rf packaging/arch/src packaging/arch/pkg packaging/arch/*.tar.* packaging/arch/verbose-lang.db* packaging/arch/verbose-lang.files*
	@tar --exclude='.git' --exclude='dist' --exclude='cross-ir' \
		--exclude='packaging/arch/src' --exclude='packaging/arch/pkg' --exclude='packaging/arch/*.tar.*' \
		--exclude='./verbose' --exclude='./verbose.exe' --exclude='./verbose1' --exclude='./verbose1.exe' \
		--transform="s,^\.,verbose-lang-$(VERSION)," \
		-czf packaging/arch/verbose-lang-$(VERSION).tar.gz .
	@cd packaging/arch && makepkg -f --skipchecksums
	@if command -v repo-add >/dev/null 2>&1; then \
		cd packaging/arch && repo-add verbose-lang.db.tar.gz verbose-lang-$(VERSION)-*.pkg.tar.zst && \
		rm -f verbose-lang.db verbose-lang.files && \
		cp verbose-lang.db.tar.gz verbose-lang.db && \
		cp verbose-lang.files.tar.gz verbose-lang.files; \
	fi
	@ls -lh packaging/arch/verbose-lang-*.pkg.tar.zst

clean:
	rm -rf dist cross-ir \
		verbose verbose.exe verbose1 verbose1.exe verbose.new .verbose.boot .verbose.boot.exe .verbose.boot.ll verbose1.ll verbose2.ll verbose3.ll src/*.ll \
		$(TARGET).ll $(TARGET).s $(TARGET) $(TARGET).exe \
		tests/raylib/bouncing_ball.ll tests/raylib/camera3d_demo.ll tests/raylib/raylib.ll tests/raylib/bouncing_ball tests/raylib/camera3d_demo
	@for dir in tests/byref tests/syntax tests/arrays tests/static_heap tests/arena tests/type_analyzer tests/modules tests/c_interop; do \
		if [ -d "$$dir" ]; then \
			find "$$dir" -maxdepth 1 -type f ! -name '*.vb' ! -name '*.c' -delete; \
		fi; \
	done
	@find tests -maxdepth 2 -type f \( -name '*.o' -o -name '*.ll' -o -name '*.s' \) -delete 2>/dev/null || true
