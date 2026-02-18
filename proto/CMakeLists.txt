cmake_minimum_required(VERSION 3.25)

# Protobuf Definitions
# SPEC-IPC-001: Shared protobuf and gRPC code generation for C++/C# IPC

# Find Protobuf and gRPC packages
find_package(Protobuf REQUIRED)
find_package(gRPC REQUIRED)

# Get all .proto files (exclude hnvue_ipc.proto which is a legacy stub)
file(GLOB PROTO_FILES
  "${CMAKE_CURRENT_SOURCE_DIR}/hnvue_common.proto"
  "${CMAKE_CURRENT_SOURCE_DIR}/hnvue_command.proto"
  "${CMAKE_CURRENT_SOURCE_DIR}/hnvue_image.proto"
  "${CMAKE_CURRENT_SOURCE_DIR}/hnvue_health.proto"
  "${CMAKE_CURRENT_SOURCE_DIR}/hnvue_config.proto"
)

# Generate C++ and gRPC stubs for each proto file
foreach(PROTO_FILE ${PROTO_FILES})
  get_filename_component(PROTO_NAME ${PROTO_FILE} NAME_WE)

  # Generate protobuf stubs
  protobuf_generate_cpp(PROTO_SRCS PROTO_HDRS ${PROTO_FILE})

  # Generate gRPC service stubs
  set(GRPC_SRCS "${CMAKE_CURRENT_BINARY_DIR}/${PROTO_NAME}.grpc.pb.cc")
  set(GRPC_HDRS "${CMAKE_CURRENT_BINARY_DIR}/${PROTO_NAME}.grpc.pb.h")

  add_custom_command(
    OUTPUT "${GRPC_SRCS}" "${GRPC_HDRS}"
    COMMAND protoc
    ARGS --grpc_out="${CMAKE_CURRENT_BINARY_DIR}"
         --cpp_out="${CMAKE_CURRENT_BINARY_DIR}"
         -I"${CMAKE_CURRENT_SOURCE_DIR}"
         --plugin=protoc-gen-grpc=$<TARGET_FILE:gRPC::grpc_cpp_plugin>
         "${PROTO_FILE}"
    DEPENDS "${PROTO_FILE}" gRPC::grpc_cpp_plugin
    COMMENT "Generating gRPC stub for ${PROTO_NAME}"
  )

  # Create interface library for generated proto files
  add_library(${PROTO_NAME}_proto INTERFACE)
  target_sources(${PROTO_NAME}_proto INTERFACE
    ${PROTO_SRCS}
    ${PROTO_HDRS}
    ${GRPC_SRCS}
    ${GRPC_HDRS}
  )
  target_include_directories(${PROTO_NAME}_proto INTERFACE
    ${CMAKE_CURRENT_BINARY_DIR}
    ${Protobuf_INCLUDE_DIRS}
  )
  target_link_libraries(${PROTO_NAME}_proto INTERFACE
    protobuf::libprotobuf
    gRPC::grpc++
  )

  message(STATUS "Generated protobuf and gRPC stubs for: ${PROTO_NAME}")

  # Add to global list for consumers
  list(APPEND HNVUE_PROTO_TARGETS ${PROTO_NAME}_proto)
endforeach()

# Create aggregate interface library for all proto targets
add_library(hnvue-ipc-proto INTERFACE)
target_link_libraries(hnvue-ipc-proto INTERFACE ${HNVUE_PROTO_TARGETS})
