cmake_minimum_required(VERSION 3.25)

# hnvue-ipc.Tests - IPC unit tests
# SPEC-IPC-001: TDD-based unit tests for gRPC services

project(hnvue-ipc-tests
    VERSION 0.1.0
    DESCRIPTION "HnVue IPC unit tests"
    LANGUAGES CXX
)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

find_package(GTest REQUIRED)
find_package(gRPC REQUIRED)
find_package(Protobuf REQUIRED)

# Test sources - organized by service
set(TEST_SOURCES
    src/test_ipc_server.cpp
    src/test_command_service.cpp
    src/test_image_service.cpp
    src/test_health_service.cpp
    src/test_config_service.cpp
)

# Integration test sources
set(INTEGRATION_TEST_SOURCES
    integration/test_integration.cpp
)

# Create unit test executable
add_executable(hnvue-ipc.Tests ${TEST_SOURCES})

# Link against GTest and the library under test
target_link_libraries(hnvue-ipc.Tests
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::ipc
        gRPC::grpc++
        protobuf::libprotobuf
)

# Include directories for generated proto headers
target_include_directories(hnvue-ipc.Tests
    PRIVATE
        ${CMAKE_BINARY_DIR}  # For generated proto headers
)

# Enable testing
enable_testing()
include(GoogleTest)
gtest_discover_tests(hnvue-ipc.Tests)

# Create integration test executable
add_executable(hnvue-ipc.IntegrationTests ${INTEGRATION_TEST_SOURCES})

# Link against GTest and the library under test
target_link_libraries(hnvue-ipc.IntegrationTests
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::ipc
        gRPC::grpc++
        protobuf::libprotobuf
)

# Include directories for generated proto headers
target_include_directories(hnvue-ipc.IntegrationTests
    PRIVATE
        ${CMAKE_BINARY_DIR}  # For generated proto headers
)

# Discover integration tests with INTEGRATION label
gtest_discover_tests(hnvue-ipc.IntegrationTests
    PROPERTIES
        LABELS "INTEGRATION"
)
