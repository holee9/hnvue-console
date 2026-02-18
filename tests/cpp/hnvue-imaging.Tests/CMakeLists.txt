cmake_minimum_required(VERSION 3.25)

# hnvue-imaging.Tests - Imaging unit tests
# SPEC-IMAGING-001: Test coverage for image processing pipeline
# SPEC-TEST-001: Automated test execution with Google Test

project(hnvue-imaging-tests
    VERSION 0.1.0
    DESCRIPTION "HnVue imaging unit and integration tests"
    LANGUAGES CXX
)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Find dependencies
find_package(GTest REQUIRED)
find_package(OpenCV 4.0 REQUIRED COMPONENTS core imgproc)
find_package(FFTW3 REQUIRED)

# Common include directory for all tests
set(IMAGING_INCLUDE_DIR ${CMAKE_SOURCE_DIR}/libs/hnvue-imaging/include)

# =============================================================================
# Data Structure Tests (ImagingTypes.h)
# =============================================================================

add_executable(test_imaging_types
    src/test_imaging_types.cpp
)

target_link_libraries(test_imaging_types
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        HnVue::imaging
)

target_include_directories(test_imaging_types
    PRIVATE
        ${CMAKE_SOURCE_DIR}/libs/hnvue-imaging/include
)

# =============================================================================
# Interface Tests (IImageProcessingEngine.h)
# =============================================================================

add_executable(test_engine_interface
    src/test_engine_interface.cpp
)

target_link_libraries(test_engine_interface
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        HnVue::imaging
)

target_include_directories(test_engine_interface
    PRIVATE
        ${CMAKE_SOURCE_DIR}/libs/hnvue-imaging/include
)

# =============================================================================
# Default Engine Tests (DefaultImageProcessingEngine)
# =============================================================================

add_executable(test_default_engine
    src/test_default_engine.cpp
)

target_link_libraries(test_default_engine
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        HnVue::imaging
        ${OpenCV_LIBS}
        FFTW3::FFTW3
)

target_include_directories(test_default_engine
    PRIVATE
        ${CMAKE_SOURCE_DIR}/libs/hnvue-imaging/include
)

# =============================================================================
# Calibration Manager Tests
# =============================================================================

add_executable(test_calibration_manager
    src/test_calibration_manager.cpp
)

target_link_libraries(test_calibration_manager
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        HnVue::imaging
)

target_include_directories(test_calibration_manager
    PRIVATE
        ${CMAKE_SOURCE_DIR}/libs/hnvue-imaging/include
)

# =============================================================================
# Engine Factory Tests
# =============================================================================

add_executable(test_engine_factory
    src/test_engine_factory.cpp
)

target_link_libraries(test_engine_factory
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        HnVue::imaging
)

target_include_directories(test_engine_factory
    PRIVATE
        ${CMAKE_SOURCE_DIR}/libs/hnvue-imaging/include
)

# =============================================================================
# Integration Tests (Full Pipeline)
# =============================================================================

add_executable(test_integration_pipeline
    src/test_integration_pipeline.cpp
)

target_link_libraries(test_integration_pipeline
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        HnVue::imaging
        ${OpenCV_LIBS}
        FFTW3::FFTW3
)

target_include_directories(test_integration_pipeline
    PRIVATE
        ${CMAKE_SOURCE_DIR}/libs/hnvue-imaging/include
)

# =============================================================================
# Performance Tests (NFR-IMG-01, NFR-IMG-02)
# =============================================================================

add_executable(test_performance
    src/test_performance.cpp
)

target_link_libraries(test_performance
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        HnVue::imaging
        ${OpenCV_LIBS}
        FFTW3::FFTW3
)

target_include_directories(test_performance
    PRIVATE
        ${CMAKE_SOURCE_DIR}/libs/hnvue-imaging/include
)

# =============================================================================
# Error Handling Tests
# =============================================================================

add_executable(test_error_handling
    src/test_error_handling.cpp
)

target_link_libraries(test_error_handling
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        HnVue::imaging
)

target_include_directories(test_error_handling
    PRIVATE
        ${CMAKE_SOURCE_DIR}/libs/hnvue-imaging/include
)

# =============================================================================
# Test Discovery
# =============================================================================
enable_testing()
include(GoogleTest)

gtest_discover_tests(test_imaging_types)
gtest_discover_tests(test_engine_interface)
gtest_discover_tests(test_default_engine)
gtest_discover_tests(test_calibration_manager)
gtest_discover_tests(test_engine_factory)
gtest_discover_tests(test_integration_pipeline)
gtest_discover_tests(test_performance)
gtest_discover_tests(test_error_handling)

# =============================================================================
# Coverage Configuration (for gcov/lcov)
# =============================================================================
option(COVERAGE "Enable coverage reporting" OFF)

if(COVERAGE)
    target_compile_options(test_imaging_types PRIVATE --coverage)
    target_link_options(test_imaging_types PRIVATE --coverage)

    target_compile_options(test_engine_interface PRIVATE --coverage)
    target_link_options(test_engine_interface PRIVATE --coverage)

    target_compile_options(test_default_engine PRIVATE --coverage)
    target_link_options(test_default_engine PRIVATE --coverage)

    target_compile_options(test_calibration_manager PRIVATE --coverage)
    target_link_options(test_calibration_manager PRIVATE --coverage)

    target_compile_options(test_engine_factory PRIVATE --coverage)
    target_link_options(test_engine_factory PRIVATE --coverage)

    target_compile_options(test_integration_pipeline PRIVATE --coverage)
    target_link_options(test_integration_pipeline PRIVATE --coverage)

    target_compile_options(test_performance PRIVATE --coverage)
    target_link_options(test_performance PRIVATE --coverage)

    target_compile_options(test_error_handling PRIVATE --coverage)
    target_link_options(test_error_handling PRIVATE --coverage)
endif()
