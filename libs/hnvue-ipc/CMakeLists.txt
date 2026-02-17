cmake_minimum_required(VERSION 3.25)

# hnvue-ipc - IPC server (gRPC)
# SPEC-INFRA-001: gRPC-based IPC server for C++ core

project(hnvue-ipc
    VERSION 0.1.0
    DESCRIPTION "HnVue IPC server (gRPC)"
    LANGUAGES CXX
)

# C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Static library
add_library(${PROJECT_NAME} STATIC
    # TODO: Add source files as they are created
)

# Public include directory
target_include_directories(${PROJECT_NAME}
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:include>
)

# Alias target
add_library(HnVue::ipc ALIAS ${PROJECT_NAME})

# TODO: Add dependencies (gRPC, protobuf)
# find_package(Protobuf REQUIRED)
# find_package(gRPC REQUIRED)
# target_link_libraries(${PROJECT_NAME}
#     PUBLIC
#         gRPC::grpc++
#         protobuf::libprotobuf
# )

# TODO: Add install rules
