cmake_minimum_required(VERSION 3.25)

# hnvue-infra.Tests - Infrastructure unit tests
# SPEC-INFRA-001: Automated test execution requirement

project(hnvue-infra-tests
    VERSION 0.1.0
    DESCRIPTION "HnVue infrastructure utilities unit tests"
    LANGUAGES CXX
)

# C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Test discovery
enable_testing()
include(CTest)

# Google Test dependency
find_package(GTest REQUIRED)

# Test executable
add_executable(hnvue-infra.Tests
    test_directory_structure.cpp
)

# Link against Google Test
target_link_libraries(hnvue-infra.Tests
    PRIVATE
        GTest::gtest
        GTest::gtest_main
)

# Include directories
target_include_directories(hnvue-infra.Tests
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}
)

# Register tests with CTest
include(GoogleTest)
gtest_discover_tests(hnvue-infra.Tests)
