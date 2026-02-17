cmake_minimum_required(VERSION 3.25)

# hnvue-imaging.Tests - Imaging unit tests
# SPEC-INFRA-001: Automated test execution

project(hnvue-imaging-tests
    VERSION 0.1.0
    DESCRIPTION "HnVue imaging unit tests"
    LANGUAGES CXX
)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

find_package(GTest REQUIRED)

add_executable(hnvue-imaging.Tests)
target_link_libraries(hnvue-imaging.Tests PRIVATE GTest::gtest GTest::gtest_main)
enable_testing()
include(GoogleTest)
gtest_discover_tests(hnvue-imaging.Tests)
