#!/bin/bash
# Build C++ server and run integration tests
# SPEC-IPC-001: Integration test execution script

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_DIR="$REPO_ROOT/build"
SERVER_EXECUTABLE="$BUILD_DIR/hnvue-ipc-server"

echo "=== HnVue IPC Integration Test Build & Run ==="
echo "Repository root: $REPO_ROOT"
echo "Build directory: $BUILD_DIR"
echo ""

# Step 1: Build C++ server
echo "Step 1: Building C++ gRPC server..."
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --target hnvue-ipc-server

if [ ! -f "$SERVER_EXECUTABLE" ]; then
    echo "ERROR: Server executable not found at: $SERVER_EXECUTABLE"
    exit 1
fi

echo "C++ server built successfully: $SERVER_EXECUTABLE"
echo ""

# Step 2: Run C# integration tests
echo "Step 2: Running C# integration tests..."
cd "$REPO_ROOT"

dotnet test tests/integration/HnVue.Integration.Tests/HnVue.Integration.Tests.csproj \
    --logger "console;verbosity=detailed" \
    --framework "net8.0-windows"

echo ""
echo "=== Integration tests completed ==="
