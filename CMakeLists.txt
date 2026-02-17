cmake_minimum_required(VERSION 3.25)

# HnVue Project - Top-level CMakeLists.txt
# SPEC-INFRA-001: Dual-language build system

project(HnVueCore
    VERSION 0.1.0
    DESCRIPTION "HnVue Core Engine - C++ Component"
    LANGUAGES CXX
)

# C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Options
option(BUILD_TESTING "Build test projects" ON)

# Enable testing
if(BUILD_TESTING)
    enable_testing()
    include(CTest)
endif()

# TODO: Add subdirectories as they are created
# add_subdirectory(proto)
# add_subdirectory(libs/hnvue-infra)
# add_subdirectory(libs/hnvue-hal)
# add_subdirectory(libs/hnvue-ipc)
# add_subdirectory(libs/hnvue-imaging)

# Add test directories if enabled
if(BUILD_TESTING)
    add_subdirectory(tests/cpp/hnvue-infra.Tests)
    # TODO: Enable as test sources are added
    # add_subdirectory(tests/cpp/hnvue-hal.Tests)
    # add_subdirectory(tests/cpp/hnvue-ipc.Tests)
    # add_subdirectory(tests/cpp/hnvue-imaging.Tests)
endif()
