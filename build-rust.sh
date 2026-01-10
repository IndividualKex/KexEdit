#!/bin/bash
set -e

echo "Building Rust library..."

cd "$(dirname "$0")/rust-backend"

cargo build --release

PLUGIN_DIR="../Assets/Runtime/Plugins"
mkdir -p "$PLUGIN_DIR"

if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" ]]; then
    echo "Copying Windows DLL..."
    cp target/release/kexengine.dll "$PLUGIN_DIR/"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    echo "Copying macOS dylib..."
    cp target/release/libkexengine.dylib "$PLUGIN_DIR/"
else
    echo "Copying Linux shared object..."
    cp target/release/libkexengine.so "$PLUGIN_DIR/"
fi

echo "Build complete! Library copied to $PLUGIN_DIR"
