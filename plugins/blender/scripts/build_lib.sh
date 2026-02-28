#!/bin/bash
# Build kexengine and copy to blender addon lib folder

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BLENDER_DIR="$(dirname "$SCRIPT_DIR")"
REPO_ROOT="$(dirname "$(dirname "$BLENDER_DIR")")"
CORE_DIR="$REPO_ROOT/packages/core"
LIB_DIR="$BLENDER_DIR/kexedit/lib"

echo "Building kexengine..."
cd "$CORE_DIR"
cargo build --release --features ffi

echo "Copying library..."
mkdir -p "$LIB_DIR"

if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" || "$OSTYPE" == "cygwin" ]]; then
    cp "$CORE_DIR/target/release/kexengine.dll" "$LIB_DIR/"
    echo "Copied kexengine.dll"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    cp "$CORE_DIR/target/release/libkexengine.dylib" "$LIB_DIR/"
    echo "Copied libkexengine.dylib"
else
    cp "$CORE_DIR/target/release/libkexengine.so" "$LIB_DIR/"
    echo "Copied libkexengine.so"
fi

echo "Done!"
