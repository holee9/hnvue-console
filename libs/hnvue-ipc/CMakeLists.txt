cmake_minimum_required(VERSION 3.25)

# hnvue-ipc - IPC server (gRPC)
# SPEC-IPC-001: gRPC-based IPC server for C++ core engine

project(hnvue-ipc
    VERSION 0.1.0
    DESCRIPTION "HnVue IPC server (gRPC)"
    LANGUAGES CXX
)

# C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Find dependencies
find_package(Protobuf REQUIRED)
find_package(gRPC REQUIRED)
find_package(spdlog REQUIRED)

# Source files - organized by service
set(IPC_SERVER_SOURCES
    src/IpcServer.cpp
    src/CommandServiceImpl.cpp
    src/ImageServiceImpl.cpp
    src/HealthServiceImpl.cpp
    src/ConfigServiceImpl.cpp
)

set(IPC_SERVER_HEADERS
    include/hnvue/ipc/IpcServer.h
    include/hnvue/ipc/CommandServiceImpl.h
    include/hnvue/ipc/ImageServiceImpl.h
    include/hnvue/ipc/HealthServiceImpl.h
    include/hnvue/ipc/ConfigServiceImpl.h
)

# Static library
add_library(${PROJECT_NAME} STATIC
    ${IPC_SERVER_SOURCES}
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
        gRPC::grpc++
        protobuf::libprotobuf
        hnvue-ipc-proto
    PRIVATE
        spdlog::spdlog
)

# Alias target
add_library(HnVue::ipc ALIAS ${PROJECT_NAME})

# Compiler warnings
if(MSVC)
    target_compile_options(${PROJECT_NAME} PRIVATE /W4)
else()
    target_compile_options(${PROJECT_NAME} PRIVATE -Wall -Wextra -Wpedantic)
endif()

# Platform-specific libraries
if(UNIX AND NOT APPLE)
    target_link_libraries(${PROJECT_NAME} PUBLIC pthread)
endif()

# Standalone server executable (for integration testing)
add_executable(hnvue-ipc-server
    src/main.cpp
)

target_link_libraries(hnvue-ipc-server PRIVATE
    HnVue::ipc
    gRPC::grpc++
    protobuf::libprotobuf
    spdlog::spdlog
)

# Platform-specific libraries for executable
if(UNIX AND NOT APPLE)
    target_link_libraries(hnvue-ipc-server PRIVATE pthread)
endif()

# Install rules (optional, for future packaging)
# install(TARGETS ${PROJECT_NAME} EXPORT HnVueTargets)
# install(FILES ${IPC_SERVER_HEADERS} DESTINATION include/hnvue/ipc)
# install(TARGETS hnvue-ipc-server DESTINATION bin)
