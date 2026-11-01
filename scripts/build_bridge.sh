#!/usr/bin/env bash
#
# build_bridge.sh - Builds the VanTools native bridge end to end.
#
# Location: scripts/build_bridge.sh, in this repository's scripts/ folder.
# Run it from anywhere; it switches to the repository root itself.
#
# Usage:
#   ./scripts/build_bridge.sh            # auto-detect the runtime identifier
#   ./scripts/build_bridge.sh osx-arm64  # use an explicit runtime identifier
#
# Steps: clean previous output, publish the C# Native AOT shared library,
# then build the C bridge with CMake linked against that AOT library.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

AOT_OUTPUT_DIR="$REPO_ROOT/out/aot"
BRIDGE_BUILD_DIR="$REPO_ROOT/src/VanTools.Bridge/build"

for tool in dotnet cmake; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "error: '$tool' was not found in PATH." >&2
        exit 1
    fi
done

# Maps the host OS and architecture to a .NET runtime identifier.
detect_rid() {
    local os arch

    case "$(uname -s)" in
        Darwin) os="osx" ;;
        Linux) os="linux" ;;
        MINGW*|MSYS*|CYGWIN*) os="win" ;;
        *)
            echo "error: cannot detect a runtime identifier for OS '$(uname -s)'." >&2
            return 1
            ;;
    esac

    case "$(uname -m)" in
        arm64|aarch64) arch="arm64" ;;
        x86_64|amd64) arch="x64" ;;
        i386|i686) arch="x86" ;;
        *)
            echo "error: cannot detect a runtime identifier for architecture '$(uname -m)'." >&2
            return 1
            ;;
    esac

    echo "${os}-${arch}"
}

RID="${1:-}"
if [ -z "$RID" ]; then
    RID="$(detect_rid)"
fi
echo "Runtime identifier: $RID"

# macOS only: the Native AOT linker always passes -lssl and -lcrypto, and
# Homebrew keeps those libraries outside the default linker search path.
if [ "$(uname -s)" = "Darwin" ]; then
    for openssl_lib_dir in \
        /opt/homebrew/opt/openssl@3/lib \
        /opt/homebrew/opt/openssl@4/lib \
        /usr/local/opt/openssl/lib \
        /opt/homebrew/lib; do
        if [ -e "$openssl_lib_dir/libssl.dylib" ]; then
            export LIBRARY_PATH="$openssl_lib_dir${LIBRARY_PATH:+:$LIBRARY_PATH}"
            break
        fi
    done
fi

echo "Cleaning previous build output..."
rm -rf "$AOT_OUTPUT_DIR" "$BRIDGE_BUILD_DIR"

echo "Publishing the C# Native AOT library..."
dotnet publish src/VanTools.Bridge.Native/VanTools.Bridge.Native.csproj \
    -c Release \
    -r "$RID" \
    /p:NativeLib=Shared \
    -o "$AOT_OUTPUT_DIR"

echo "Building the C bridge..."
cmake -B "$BRIDGE_BUILD_DIR" -S src/VanTools.Bridge -DAOT_DIR="$AOT_OUTPUT_DIR"
cmake --build "$BRIDGE_BUILD_DIR"

for bridge_library in \
    libVanTools.Bridge.dylib \
    libVanTools.Bridge.so \
    VanTools.Bridge.dll; do
    if [ -f "$BRIDGE_BUILD_DIR/$bridge_library" ]; then
        echo "Native AOT library: $AOT_OUTPUT_DIR"
        echo "C bridge library:   $BRIDGE_BUILD_DIR/$bridge_library"
        exit 0
    fi
done

echo "error: the C bridge library was not found in '$BRIDGE_BUILD_DIR'." >&2
exit 1
