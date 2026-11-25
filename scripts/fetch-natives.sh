#!/usr/bin/env bash
# fetch-natives.sh — Build the 7-Zip native shared library for the current platform.
# Places the output in src/SevenZipSharper.Native/runtimes/<RID>/native/.
#
# Usage:
#   ./scripts/fetch-natives.sh           # uses version from scripts/7zip-version
#   ./scripts/fetch-natives.sh 2601      # override version
#
# Requirements: curl, tar (with xz support), make, a C++ compiler (clang or g++)
# On Windows use fetch-natives.ps1 instead (it needs Visual Studio with the C++ tools).
#
# The build leaves out the RAR code (DISABLE_RAR=1), see the make call below.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
VERSION="${1:-$(cat "$SCRIPT_DIR/7zip-version")}"
VERSION_DOTTED="${VERSION:0:2}.${VERSION:2}"   # "2601" -> "26.01"
BASE_URL="https://github.com/ip7z/7zip/releases/download/$VERSION_DOTTED"

RUNTIMES_DIR="$REPO_ROOT/src/SevenZipSharper.Native/runtimes"
WORK_DIR="$REPO_ROOT/artifacts/native-build"
mkdir -p "$WORK_DIR"

# ---------------------------------------------------------------------------
# Platform detection
# ---------------------------------------------------------------------------
OS="$(uname -s)"
ARCH="$(uname -m)"

case "$ARCH" in
  x86_64)        ARCH_RID="x64"   ;;
  arm64|aarch64) ARCH_RID="arm64" ;;
  *) echo "error: unsupported architecture '$ARCH'" >&2; exit 1 ;;
esac

case "$OS" in
  Darwin) PLATFORM="osx";   LIB_NAME="7z.dylib" ;;
  Linux)  PLATFORM="linux"; LIB_NAME="7z.so"    ;;
  *)
    echo "error: unsupported OS '$OS' — use fetch-natives.ps1 on Windows" >&2
    exit 1
    ;;
esac

# TARGET_RID can be set to override auto-detection (e.g. cross-compiling osx-x64 on arm64).
RID="${TARGET_RID:-$PLATFORM-$ARCH_RID}"
DEST_DIR="$RUNTIMES_DIR/$RID/native"
mkdir -p "$DEST_DIR"

echo "==> 7-Zip $VERSION_DOTTED  |  target: $RID"

# ---------------------------------------------------------------------------
# Download source
# ---------------------------------------------------------------------------
TARBALL="$WORK_DIR/7z${VERSION}-src.tar.xz"
SRC_DIR="$WORK_DIR/7z${VERSION}-src"

if [[ ! -f "$TARBALL" ]]; then
  echo "--> Downloading source tarball..."
  curl -fsSL --proto '=https' "$BASE_URL/7z${VERSION}-src.tar.xz" -o "$TARBALL"
fi

# Verify checksum.
echo "--> Verifying checksum..."
TARBALL_NAME="7z${VERSION}-src.tar.xz"
EXPECTED_HASH=$(grep "^[0-9a-f]*  ${TARBALL_NAME}$" "$SCRIPT_DIR/7zip-sha256" | awk '{print $1}')
if [[ -z "$EXPECTED_HASH" ]]; then
  echo "error: no checksum entry for ${TARBALL_NAME} in scripts/7zip-sha256" >&2
  exit 1
fi
if command -v sha256sum &>/dev/null; then
  ACTUAL_HASH=$(sha256sum "$TARBALL" | awk '{print $1}')
else
  ACTUAL_HASH=$(shasum -a 256 "$TARBALL" | awk '{print $1}')
fi
if [[ "$ACTUAL_HASH" != "$EXPECTED_HASH" ]]; then
  echo "error: checksum mismatch for ${TARBALL_NAME}" >&2
  echo "  expected: $EXPECTED_HASH" >&2
  echo "  actual:   $ACTUAL_HASH" >&2
  rm -f "$TARBALL"
  exit 1
fi
echo "--> Checksum OK."

if [[ ! -d "$SRC_DIR" ]]; then
  echo "--> Extracting source..."
  mkdir -p "$SRC_DIR"
  tar -xf "$TARBALL" -C "$SRC_DIR"
fi

# ---------------------------------------------------------------------------
# Build Format7zF (the shared library bundle)
# ---------------------------------------------------------------------------
FORMAT_DIR="$SRC_DIR/CPP/7zip/Bundles/Format7zF"

if [[ ! -f "$FORMAT_DIR/makefile.gcc" ]]; then
  echo "error: makefile.gcc not found at $FORMAT_DIR" >&2
  echo "       The source layout may have changed — check the 7-Zip release." >&2
  exit 1
fi

CPUS="$(nproc 2>/dev/null || sysctl -n hw.logicalcpu 2>/dev/null || echo 4)"
echo "--> Building with $CPUS CPUs..."

# CC/CXX can be overridden by the caller for cross-compilation.
# DISABLE_RAR=1 leaves out the unRAR handlers, decoders and crypto: its licence restricts the code
# to decompression and is not LGPL, so SevenZipSharper ships no RAR code at all.
make -C "$FORMAT_DIR" -f makefile.gcc -j"$CPUS" DISABLE_RAR=1

# ---------------------------------------------------------------------------
# Copy output
# ---------------------------------------------------------------------------
OUTPUT="$FORMAT_DIR/_o/7z.so"
if [[ ! -f "$OUTPUT" ]]; then
  echo "error: expected build output not found: $OUTPUT" >&2
  exit 1
fi

cp "$OUTPUT" "$DEST_DIR/$LIB_NAME"

# Strip debug symbols to reduce binary size.
if [[ "$OS" == "Darwin" ]]; then
  strip -S "$DEST_DIR/$LIB_NAME"
else
  strip --strip-debug "$DEST_DIR/$LIB_NAME"
fi

echo "==> Installed: $DEST_DIR/$LIB_NAME"
