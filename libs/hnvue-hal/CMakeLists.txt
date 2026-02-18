cmake_minimum_required(VERSION 3.25)

# hnvue-hal - Hardware Abstraction Layer
# SPEC-INFRA-001: HAL for FPGA detector and X-ray generator

project(hnvue-hal
    VERSION 0.1.0
    DESCRIPTION "HnVue hardware abstraction layer"
    LANGUAGES CXX
)

# C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Find Protobuf
find_package(Protobuf REQUIRED)

# Proto files
set(PROTO_FILES
    proto/hvg_control.proto
    proto/detector_acquisition.proto
)

# Generate protobuf sources
protobuf_generate_cpp(PROTO_SRCS PROTO_HDRS ${PROTO_FILES})

# Source files
set(SOURCE_FILES
    src/aec/AecController.cpp
    src/buffer/DmaRingBuffer.cpp
    src/DeviceManager.cpp
    src/generator/CommandQueue.cpp
    src/generator/GeneratorBase.cpp
    src/generator/GeneratorSimulator.cpp
    src/plugin/DetectorPluginLoader.cpp
    ${PROTO_SRCS}
)

# Static library
add_library(${PROJECT_NAME} ${SOURCE_FILES})

# Public include directory
target_include_directories(${PROJECT_NAME}
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/src>
        $<INSTALL_INTERFACE:include>
    PRIVATE
        ${CMAKE_CURRENT_BINARY_DIR}  # For generated proto headers
)

# Alias target
add_library(HnVue::hal ALIAS ${PROJECT_NAME})

# Find dependencies
find_package(spdlog REQUIRED)

# Link dependencies
target_link_libraries(${PROJECT_NAME}
    PUBLIC
        protobuf::libprotobuf
    PRIVATE
        spdlog::spdlog
        pthread
)

# Add tests subdirectory
if(BUILD_TESTING)
    add_subdirectory(tests)
endif()

# Install rules
install(DIRECTORY include/hnvue/hal/
    DESTINATION include/hnvue/hal
    FILES_MATCHING PATTERN "*.h"
)

# Export target
include(GNUInstallDirs)
include(CMakePackageConfigHelpers)

configure_package_config_file(
    "${CMAKE_CURRENT_SOURCE_DIR}/hnvue-hal-config.cmake.in"
    "${CMAKE_CURRENT_BINARY_DIR}/hnvue-hal-config.cmake"
    INSTALL_DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/hnvue-hal
)

write_basic_package_version_file(
    "${CMAKE_CURRENT_BINARY_DIR}/hnvue-hal-config-version.cmake"
    VERSION ${PROJECT_VERSION}
    COMPATIBILITY SameMajorVersion
)

install(FILES
    "${CMAKE_CURRENT_BINARY_DIR}/hnvue-hal-config.cmake"
    "${CMAKE_CURRENT_BINARY_DIR}/hnvue-hal-config-version.cmake"
    DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/hnvue-hal
)
