#!/bin/bash
# Build kexengine and copy to blender addon lib folder.
#
# Usage:
#   build_lib.sh             Build for the host platform
#   build_lib.sh windows     Cross-compile to x86_64-pc-windows-gnu
#   build_lib.sh linux       Cross-compile to x86_64-unknown-linux-gnu
#   build_lib.sh all         Host + Windows (Linux host only)
#
# After building, mirrors the addon dir (sources + lib) to any path listed in
# the KEXEDIT_DEV_INSTALL env var (colon-separated). Useful for syncing into a
# Windows-side Blender extensions folder from WSL where cross-filesystem
# symlinks are awkward. Example:
#   KEXEDIT_DEV_INSTALL=/mnt/c/BlenderExtensions/dev/kexedit build_lib.sh all

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BLENDER_DIR="$(dirname "$SCRIPT_DIR")"
REPO_ROOT="$(dirname "$(dirname "$BLENDER_DIR")")"
CORE_DIR="$REPO_ROOT/packages/core"
ADDON_DIR="$BLENDER_DIR/kexedit"
LIB_DIR="$ADDON_DIR/lib"
FIXTURES_DIR="$ADDON_DIR/fixtures"

mkdir -p "$LIB_DIR" "$FIXTURES_DIR"

# Mirror .kex fixtures from packages/core/test-data so the addon's "Load Test
# Files" operators work in any install (dev symlink or packaged extension).
cp "$CORE_DIR/test-data/"*.kex "$FIXTURES_DIR/"

cd "$CORE_DIR"

build_host() {
    echo "Building kexengine for host..."
    cargo build --release --features ffi
    if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" || "$OSTYPE" == "cygwin" ]]; then
        cp "target/release/kexengine.dll" "$LIB_DIR/"
        echo "Copied kexengine.dll"
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        cp "target/release/libkexengine.dylib" "$LIB_DIR/"
        echo "Copied libkexengine.dylib"
    else
        cp "target/release/libkexengine.so" "$LIB_DIR/"
        echo "Copied libkexengine.so"
    fi
}

build_windows() {
    echo "Cross-compiling kexengine for x86_64-pc-windows-gnu..."
    cargo build --release --features ffi --target x86_64-pc-windows-gnu
    cp "target/x86_64-pc-windows-gnu/release/kexengine.dll" "$LIB_DIR/"
    echo "Copied kexengine.dll"
}

build_linux() {
    echo "Cross-compiling kexengine for x86_64-unknown-linux-gnu..."
    cargo build --release --features ffi --target x86_64-unknown-linux-gnu
    cp "target/x86_64-unknown-linux-gnu/release/libkexengine.so" "$LIB_DIR/"
    echo "Copied libkexengine.so"
}

case "${1:-host}" in
    host)    build_host ;;
    windows) build_windows ;;
    linux)   build_linux ;;
    all)
        build_host
        build_windows
        ;;
    *)
        echo "Unknown target: $1. Use one of: host | windows | linux | all" >&2
        exit 1
        ;;
esac

if [ -n "$KEXEDIT_DEV_INSTALL" ]; then
    IFS=':' read -ra paths <<< "$KEXEDIT_DEV_INSTALL"
    for dest in "${paths[@]}"; do
        echo "Mirroring addon -> $dest"
        mkdir -p "$dest"
        rsync -a --delete \
            --exclude='__pycache__/' \
            --exclude='*.pyc' \
            "$ADDON_DIR/" "$dest/"
    done
fi

echo "Done!"
