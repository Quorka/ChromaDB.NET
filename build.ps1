# PowerShell build script for ChromaDB C# bindings
$ErrorActionPreference = "Stop"

# Script directories
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$OutputDir = Join-Path $ScriptDir "runtimes"

# Create output directories for each platform
New-Item -ItemType Directory -Force -Path (Join-Path $OutputDir "win-x64\native")
New-Item -ItemType Directory -Force -Path (Join-Path $OutputDir "linux-x64\native")
New-Item -ItemType Directory -Force -Path (Join-Path $OutputDir "osx-x64\native")
New-Item -ItemType Directory -Force -Path (Join-Path $OutputDir "osx-arm64\native")

Write-Host "Building ChromaDB C# bindings for multiple platforms..."

# Check if we're on Windows
if ($IsWindows -or $env:OS -match "Windows") {
    # Build for Windows x64
    Write-Host "Building for Windows x64..."
    cargo build --release
    Copy-Item "$RootDir\target\release\chroma_csharp.dll" -Destination "$OutputDir\win-x64\native\" -Force
    
    # Check if other targets are installed
    $targets = rustup target list --installed
    
    # Cross-compile for Linux (if target installed)
    if ($targets -match "x86_64-unknown-linux-gnu") {
        Write-Host "Building for Linux x64..."
        cargo build --release --target x86_64-unknown-linux-gnu
        Copy-Item "$RootDir\target\x86_64-unknown-linux-gnu\release\libchroma_csharp.so" -Destination "$OutputDir\linux-x64\native\" -Force
    } else {
        Write-Host "Linux target not installed. Skipping Linux build."
        Write-Host "To install: rustup target add x86_64-unknown-linux-gnu"
    }
    
    # Cross-compile for macOS (if targets installed)
    if ($targets -match "x86_64-apple-darwin") {
        Write-Host "Building for macOS x64..."
        cargo build --release --target x86_64-apple-darwin
        Copy-Item "$RootDir\target\x86_64-apple-darwin\release\libchroma_csharp.dylib" -Destination "$OutputDir\osx-x64\native\" -Force
    } else {
        Write-Host "macOS x64 target not installed. Skipping macOS x64 build."
        Write-Host "To install: rustup target add x86_64-apple-darwin"
    }
    
    if ($targets -match "aarch64-apple-darwin") {
        Write-Host "Building for macOS ARM64..."
        cargo build --release --target aarch64-apple-darwin
        Copy-Item "$RootDir\target\aarch64-apple-darwin\release\libchroma_csharp.dylib" -Destination "$OutputDir\osx-arm64\native\" -Force
    } else {
        Write-Host "macOS ARM64 target not installed. Skipping macOS ARM64 build."
        Write-Host "To install: rustup target add aarch64-apple-darwin"
    }
} else {
    Write-Host "This script is optimized for Windows. Please use build.sh on Linux/macOS."
}

Write-Host "Build complete. Native libraries are in $OutputDir"