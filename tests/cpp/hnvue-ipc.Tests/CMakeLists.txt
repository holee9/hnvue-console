cmake_minimum_required(VERSION 3.25)

# hnvue-ipc.Tests - IPC unit tests
# SPEC-INFRA-001: Automated test execution

project(hnvue-ipc-tests
    VERSION 0.1.0
    DESCRIPTION "HnVue IPC unit tests"
    LANGUAGES CXX
)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

find_package(GTest REQUIRED)

add_executable(hnvue-ipc.Tests)
target_link_libraries(hnvue-ipc.Tests PRIVATE GTest::gtest GTest::gtest_main)
enable_testing()
include(GoogleTest)
gtest_discover_tests(hnvue-ipc.Tests)
