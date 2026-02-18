#!/bin/bash
# run-integration-tests.sh - Script to run IPC integration tests
# SPEC-IPC-001: Integration test automation

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
BUILD_DIR="${BUILD_DIR:-build}"
CPP_SERVER_PORT="${CPP_SERVER_PORT:-50051}"
TEST_FILTER="${TEST_FILTER:-*}"

# Function to print colored output
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check if server is running
check_server_running() {
    local port=$1
    if lsof -Pi :$port -sTCP:LISTEN -t >/dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# Function to wait for server to be ready
wait_for_server() {
    local port=$1
    local max_wait=10
    local count=0

    print_info "Waiting for server on port $port..."

    while [ $count -lt $max_wait ]; do
        if check_server_running $port; then
            print_info "Server is ready!"
            return 0
        fi
        sleep 1
        count=$((count + 1))
    done

    print_error "Server failed to start within ${max_wait} seconds"
    return 1
}

# Function to start C++ server
start_cpp_server() {
    print_info "Starting C++ gRPC server on port ${CPP_SERVER_PORT}..."

    if [ ! -f "${BUILD_DIR}/libs/hnvue-ipc/hnvue-ipc-server" ]; then
        print_error "C++ server binary not found at ${BUILD_DIR}/libs/hnvue-ipc/hnvue-ipc-server"
        print_info "Please build the server first:"
        print_info "  cmake --build ${BUILD_DIR} --target hnvue-ipc-server"
        exit 1
    fi

    # Start server in background
    "${BUILD_DIR}/libs/hnvue-ipc/hnvue-ipc-server" --port ${CPP_SERVER_PORT} > /tmp/hnvue-server.log 2>&1 &
    SERVER_PID=$!

    # Wait for server to be ready
    if ! wait_for_server ${CPP_SERVER_PORT}; then
        print_error "Failed to start C++ server"
        print_info "Server log:"
        cat /tmp/hnvue-server.log
        exit 1
    fi

    print_info "C++ server started with PID ${SERVER_PID}"
}

# Function to stop C++ server
stop_cpp_server() {
    if [ -n "${SERVER_PID}" ]; then
        print_info "Stopping C++ server (PID ${SERVER_PID})..."
        kill ${SERVER_PID} 2>/dev/null || true
        wait ${SERVER_PID} 2>/dev/null || true
        print_info "C++ server stopped"
    fi
}

# Function to run C++ integration tests
run_cpp_integration_tests() {
    print_info "Running C++ integration tests..."

    local test_binary="${BUILD_DIR}/tests/cpp/hnvue-ipc.Tests/hnvue-ipc.IntegrationTests"

    if [ ! -f "${test_binary}" ]; then
        print_error "C++ integration test binary not found at ${test_binary}"
        print_info "Please build integration tests first:"
        print_info "  cmake --build ${BUILD_DIR} --target hnvue-ipc.IntegrationTests"
        return 1
    fi

    # Run tests
    if "${test_binary}" --gtest_filter="${TEST_FILTER}"; then
        print_info "C++ integration tests PASSED"
        return 0
    else
        print_error "C++ integration tests FAILED"
        return 1
    fi
}

# Function to run C# integration tests
run_csharp_integration_tests() {
    print_info "Running C# integration tests..."

    local test_project="tests/HnVue.Ipc.Client.Tests/HnVue.Ipc.Client.Tests.csproj"

    if [ ! -f "${test_project}" ]; then
        print_error "C# test project not found at ${test_project}"
        return 1
    fi

    # Run tests
    if dotnet test "${test_project}" --filter "FullyQualifiedName~Integration" --no-build; then
        print_info "C# integration tests PASSED"
        return 0
    else
        print_error "C# integration tests FAILED"
        return 1
    fi
}

# Main script
main() {
    print_info "HnVue IPC Integration Tests"
    print_info "================================"

    # Parse command line arguments
    TEST_TYPE="${1:-all}"

    case "${TEST_TYPE}" in
        cpp)
            print_info "Running C++ integration tests only"
            start_cpp_server
            trap stop_cpp_server EXIT
            run_cpp_integration_tests
            ;;
        csharp)
            print_info "Running C# integration tests only"
            start_cpp_server
            trap stop_cpp_server EXIT
            run_csharp_integration_tests
            ;;
        all)
            print_info "Running all integration tests"
            start_cpp_server
            trap stop_cpp_server EXIT

            CPP_RESULT=0
            CSHARP_RESULT=0

            run_cpp_integration_tests || CPP_RESULT=$?
            run_csharp_integration_tests || CSHARP_RESULT=$?

            if [ ${CPP_RESULT} -ne 0 ] || [ ${CSHARP_RESULT} -ne 0 ]; then
                print_error "Some integration tests FAILED"
                exit 1
            else
                print_info "All integration tests PASSED"
            fi
            ;;
        *)
            print_error "Unknown test type: ${TEST_TYPE}"
            print_info "Usage: $0 [cpp|csharp|all]"
            exit 1
            ;;
    esac
}

# Run main function
main "$@"
