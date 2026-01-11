#!/bin/bash
# Build kexengine and copy to lib folder

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
KEXENGINE_DIR="$(dirname "$PROJECT_DIR")/kexengine"
LIB_DIR="$PROJECT_DIR/kexengine/lib"

echo "Building kexengine..."
cd "$KEXENGINE_DIR"
cargo build --release --features ffi

echo "Copying library..."
mkdir -p "$LIB_DIR"

# Copy based on platform
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" || "$OSTYPE" == "cygwin" ]]; then
    cp "$KEXENGINE_DIR/target/release/kexengine.dll" "$LIB_DIR/"
    echo "Copied kexengine.dll"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    cp "$KEXENGINE_DIR/target/release/libkexengine.dylib" "$LIB_DIR/"
    echo "Copied libkexengine.dylib"
else
    cp "$KEXENGINE_DIR/target/release/libkexengine.so" "$LIB_DIR/"
    echo "Copied libkexengine.so"
fi

echo "Done!"
