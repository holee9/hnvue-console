# hnvue-hal tests
cmake_minimum_required(VERSION 3.25)

# Find GoogleTest and GoogleMock
find_package(GTest REQUIRED)

# Test executables
add_executable(test_command_queue
    test_command_queue.cpp
)

target_link_libraries(test_command_queue
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::hal
)

add_executable(test_generator_simulator
    test_generator_simulator.cpp
)

target_link_libraries(test_generator_simulator
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::hal
)

add_executable(test_detector_plugin_loader
    test_detector_plugin_loader.cpp
)

target_link_libraries(test_detector_plugin_loader
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::hal
)

# DMA Ring Buffer tests (FR-HAL-09, NFR-HAL-01)
add_executable(test_dma_ring_buffer
    test_dma_ring_buffer.cpp
)

target_link_libraries(test_dma_ring_buffer
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::hal
)

# AEC Controller tests (FR-HAL-07)
add_executable(test_aec_controller
    test_aec_controller.cpp
)

target_link_libraries(test_aec_controller
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::hal
)

# DeviceManager tests (FR-HAL-01, FR-HAL-03, FR-HAL-08)
add_executable(test_device_manager
    test_device_manager.cpp
)

target_link_libraries(test_device_manager
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::hal
)

# =============================================================================
# HAL Interface Unit Tests with Google Mock (NFR-HAL-03)
# =============================================================================

# ICollimator interface tests
add_executable(test_icollimator
    test_icollimator.cpp
)

target_link_libraries(test_icollimator
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::hal
)

# IPatientTable interface tests
add_executable(test_ipatienttable
    test_ipatienttable.cpp
)

target_link_libraries(test_ipatienttable
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::hal
)

# IDoseMonitor interface tests
add_executable(test_idosemonitor
    test_idosemonitor.cpp
)

target_link_libraries(test_idosemonitor
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::hal
)

# ISafetyInterlock interface tests
add_executable(test_isafetyinterlock
    test_isafetyinterlock.cpp
)

target_link_libraries(test_isafetyinterlock
    PRIVATE
        GTest::gtest
        GTest::gtest_main
        GTest::gmock
        GTest::gmock_main
        HnVue::hal
)

# Discover tests
include(GoogleTest)
gtest_discover_tests(test_command_queue)
gtest_discover_tests(test_generator_simulator)
gtest_discover_tests(test_detector_plugin_loader)
gtest_discover_tests(test_dma_ring_buffer)
gtest_discover_tests(test_aec_controller)
gtest_discover_tests(test_device_manager)
gtest_discover_tests(test_icollimator)
gtest_discover_tests(test_ipatienttable)
gtest_discover_tests(test_idosemonitor)
gtest_discover_tests(test_isafetyinterlock)
