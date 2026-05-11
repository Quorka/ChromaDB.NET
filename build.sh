#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="$SCRIPT_DIR/runtimes"

mkdir -p "$OUTPUT_DIR/win-x64/native"
mkdir -p "$OUTPUT_DIR/linux-x64/native"
mkdir -p "$OUTPUT_DIR/osx-arm64/native"

echo "Building ChromaDB C# bindings for multiple platforms..."

# Build for Linux x64
echo "Building for Linux x64..."
cargo build --release
cp "$SCRIPT_DIR/target/release/libchroma_csharp.so" "$OUTPUT_DIR/linux-x64/native/"

# Cross-compile for Windows (requires appropriate target)
if rustup target list --installed | grep -q "x86_64-pc-windows-msvc"; then
    echo "Building for Windows x64..."
    cargo build --release --target x86_64-pc-windows-msvc
    cp "$SCRIPT_DIR/target/x86_64-pc-windows-msvc/release/chroma_csharp.dll" "$OUTPUT_DIR/win-x64/native/"
else
    echo "Windows target not installed. Skipping Windows build."
    echo "To install: rustup target add x86_64-pc-windows-msvc"
fi

# Cross-compile for macOS ARM64 (requires appropriate target)
if rustup target list --installed | grep -q "aarch64-apple-darwin"; then
    echo "Building for macOS ARM64..."
    cargo build --release --target aarch64-apple-darwin
    cp "$SCRIPT_DIR/target/aarch64-apple-darwin/release/libchroma_csharp.dylib" "$OUTPUT_DIR/osx-arm64/native/"
else
    echo "macOS ARM64 target not installed. Skipping macOS ARM64 build."
    echo "To install: rustup target add aarch64-apple-darwin"
fi

echo "Build complete. Native libraries are in $OUTPUT_DIR"
