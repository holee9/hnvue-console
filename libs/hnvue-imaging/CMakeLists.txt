cmake_minimum_required(VERSION 3.25)

# hnvue-imaging - Image processing pipeline
# SPEC-IMAGING-001: Replaceable image processing engine

project(hnvue-imaging
    VERSION 0.1.0
    DESCRIPTION "HnVue image processing pipeline"
    LANGUAGES CXX
)

# C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Find dependencies
find_package(OpenCV 4.0 REQUIRED COMPONENTS core imgproc)
find_package(FFTW3 REQUIRED)

# Source files
set(IMAGING_SOURCES
    src/EngineFactory.cpp
    src/DefaultImageProcessingEngine.cpp
    src/CalibrationManager.cpp
)

set(IMAGING_HEADERS
    include/hnvue/imaging/ImagingTypes.h
    include/hnvue/imaging/IImageProcessingEngine.h
    include/hnvue/imaging/DefaultImageProcessingEngine.h
    include/hnvue/imaging/CalibrationManager.h
)

# Static library
add_library(${PROJECT_NAME} STATIC
    ${IMAGING_SOURCES}
    ${IMAGING_HEADERS}
)

# Public include directory
target_include_directories(${PROJECT_NAME}
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:include>
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src
)

# Link dependencies
target_link_libraries(${PROJECT_NAME}
    PUBLIC
        ${OpenCV_LIBS}
        FFTW3::FFTW3
)

# Compiler warnings
if(MSVC)
    target_compile_options(${PROJECT_NAME} PRIVATE /W4)
else()
    target_compile_options(${PROJECT_NAME} PRIVATE -Wall -Wextra -Wpedantic)
endif()

# Alias target
add_library(HnVue::imaging ALIAS ${PROJECT_NAME})

# Default engine plugin (shared library)
add_library(default-engine SHARED
    engines/default-engine/DefaultEnginePlugin.cpp
)

target_include_directories(default-engine
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(default-engine
    PRIVATE
        ${PROJECT_NAME}
        ${OpenCV_LIBS}
        FFTW3::FFTW3
)

# Set DLL export properties
if(WIN32)
    set_target_properties(default-engine PROPERTIES
        SUFFIX ".dll"
        OUTPUT_NAME "hnvue-default-engine"
    )
endif()

# =============================================================================
# Installation
# =============================================================================

install(TARGETS ${PROJECT_NAME} default-engine
    ARCHIVE DESTINATION lib
    LIBRARY DESTINATION lib
    RUNTIME DESTINATION bin
)

install(DIRECTORY include/ DESTINATION include)

# Install default engine plugin
install(TARGETS default-engine
    LIBRARY DESTINATION lib/hnvue-imaging/engines
    RUNTIME DESTINATION bin/hnvue-imaging/engines
)
