#!/usr/bin/env sh
# VerBose-Lang one-line installer for Linux (x86_64, arm64) and macOS (arm64)
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/thetrung/verbose-lang/main/install.sh | sh
#   curl -fsSL https://raw.githubusercontent.com/thetrung/verbose-lang/main/install.sh | sh -s -- --prefix /usr/local
#   curl -fsSL https://raw.githubusercontent.com/thetrung/verbose-lang/main/install.sh | sh -s -- --from-source
#
# Environment variables:
#   VERBOSE_VERSION      Release tag (e.g. v0.6.1) or "latest" (default: latest)
#   VERBOSE_INSTALL_DIR  Destination bin directory (default: ~/.local/bin, or /usr/local/bin when root)
#   VERBOSE_REPO         GitHub repo slug (default: thetrung/verbose-lang)

set -eu

REPO="${VERBOSE_REPO:-thetrung/verbose-lang}"
VERSION="${VERBOSE_VERSION:-latest}"
FROM_SOURCE=0

if [ "$(id -u)" -eq 0 ]; then
  DEFAULT_DIR="/usr/local/bin"
else
  DEFAULT_DIR="${HOME}/.local/bin"
fi
INSTALL_DIR="${VERBOSE_INSTALL_DIR:-$DEFAULT_DIR}"

usage() {
  cat <<EOF
VerBose-Lang Installer

Usage:
  install.sh [options]

Options:
  --prefix <dir>      Install into <dir>/bin (overrides VERBOSE_INSTALL_DIR)
  --bin-dir <dir>     Install directly into <dir>
  --version <tag>     Install a specific release tag (e.g. v0.6.1) or "latest"
  --from-source       Clone repository and cold-bootstrap from verbose.ll via clang
  -h, --help          Show this help message
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --prefix)
      [ "$#" -ge 2 ] || { echo "Error: --prefix requires a directory" >&2; exit 1; }
      INSTALL_DIR="${2%/}/bin"
      shift 2
      ;;
    --bin-dir)
      [ "$#" -ge 2 ] || { echo "Error: --bin-dir requires a directory" >&2; exit 1; }
      INSTALL_DIR="${2%/}"
      shift 2
      ;;
    --version)
      [ "$#" -ge 2 ] || { echo "Error: --version requires a tag" >&2; exit 1; }
      VERSION="$2"
      shift 2
      ;;
    --from-source)
      FROM_SOURCE=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Error: unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

need_cmd() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "Error: required command '$1' not found in PATH." >&2
    exit 1
  }
}

sha256_file() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print $1}'
  else
    echo ""
  fi
}

OS="$(uname -s 2>/dev/null || echo Unknown)"
ARCH="$(uname -m 2>/dev/null || echo Unknown)"

PLATFORM=""
case "$OS:$ARCH" in
  Linux:x86_64|Linux:amd64)
    PLATFORM="linux-x86_64"
    ;;
  Linux:aarch64|Linux:arm64)
    PLATFORM="linux-arm64"
    ;;
  Darwin:arm64|Darwin:aarch64)
    PLATFORM="macos-arm64"
    ;;
  *)
    if [ "$FROM_SOURCE" -eq 0 ]; then
      echo "No prebuilt release binary for ${OS}/${ARCH}; falling back to --from-source."
      FROM_SOURCE=1
    fi
    ;;
esac

TMP_DIR="$(mktemp -d 2>/dev/null || mktemp -d -t verbose-install)"
trap 'rm -rf "$TMP_DIR"' EXIT INT TERM

mkdir -p "$INSTALL_DIR"

if [ "$FROM_SOURCE" -eq 1 ]; then
  need_cmd git
  need_cmd clang
  need_cmd make
  echo "==> Cloning https://github.com/${REPO}.git ..."
  if [ "$VERSION" = "latest" ]; then
    git clone --depth 1 "https://github.com/${REPO}.git" "$TMP_DIR/src"
  else
    git clone --depth 1 --branch "$VERSION" "https://github.com/${REPO}.git" "$TMP_DIR/src"
  fi
  echo "==> Bootstrapping VerBose-Lang from verbose.ll via clang ..."
  (cd "$TMP_DIR/src" && make bootstrap)
  install -m 755 "$TMP_DIR/src/verbose" "$INSTALL_DIR/verbose"
else
  need_cmd curl
  ASSET="verbose-${PLATFORM}"
  if [ "$VERSION" = "latest" ]; then
    BASE_URL="https://github.com/${REPO}/releases/latest/download"
  else
    BASE_URL="https://github.com/${REPO}/releases/download/${VERSION}"
  fi

  echo "==> Downloading ${ASSET} (${VERSION}) from ${BASE_URL} ..."
  curl -fsSL "${BASE_URL}/${ASSET}" -o "$TMP_DIR/verbose"

  if curl -fsSL "${BASE_URL}/SHA256SUMS.txt" -o "$TMP_DIR/SHA256SUMS.txt" 2>/dev/null; then
    EXPECTED="$(awk -v f="$ASSET" '$2 == f || $2 == ("*" f) { print $1; exit }' "$TMP_DIR/SHA256SUMS.txt")"
    ACTUAL="$(sha256_file "$TMP_DIR/verbose")"
    if [ -n "$EXPECTED" ] && [ -n "$ACTUAL" ]; then
      if [ "$EXPECTED" != "$ACTUAL" ]; then
        echo "Error: SHA-256 checksum mismatch for ${ASSET}!" >&2
        echo "  expected: $EXPECTED" >&2
        echo "  actual:   $ACTUAL" >&2
        exit 1
      fi
      echo "==> Verified SHA-256 checksum (${ACTUAL})."
    fi
  fi

  chmod 755 "$TMP_DIR/verbose"
  mv "$TMP_DIR/verbose" "$INSTALL_DIR/verbose"
fi

echo "==> Installed VerBose-Lang to ${INSTALL_DIR}/verbose"
"$INSTALL_DIR/verbose" --version

if ! command -v clang >/dev/null 2>&1; then
  echo ""
  echo "Note: 'clang' was not found in PATH. VerBose-Lang requires clang to compile native executables."
fi

case ":${PATH}:" in
  *":${INSTALL_DIR}:"*) ;;
  *)
    echo ""
    echo "Note: ${INSTALL_DIR} is not currently in your PATH. Add this to your shell profile:"
    echo "  export PATH=\"${INSTALL_DIR}:\$PATH\""
    ;;
esac
