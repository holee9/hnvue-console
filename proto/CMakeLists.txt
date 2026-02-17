cmake_minimum_required(VERSION 3.25)

# Protobuf Definitions
# SPEC-INFRA-001: Shared protobuf code generation

# Find Protobuf package
find_package(Protobuf REQUIRED)
find_package(gRPC REQUIRED)

# Get all .proto files
file(GLOB_RECURSE PROTO_FILES "${CMAKE_CURRENT_SOURCE_DIR}/*.proto")

# Generate C++ stubs
foreach(PROTO_FILE ${PROTO_FILES})
  get_filename_component(PROTO_NAME ${PROTO_FILE} NAME_WE)

  protobuf_generate_cpp(PROTO_SRCS PROTO_HDRS ${PROTO_FILE})

  # Create interface library for generated files
  add_library(${PROTO_NAME}_proto INTERFACE)
  target_sources(${PROTO_NAME}_proto INTERFACE ${PROTO_SRCS} ${PROTO_HDRS})
  target_include_directories(${PROTO_NAME}_proto INTERFACE
    ${CMAKE_CURRENT_BINARY_DIR}
    ${Protobuf_INCLUDE_DIRS}
  )
  target_link_libraries(${PROTO_NAME}_proto INTERFACE
    protobuf::libprotobuf
  )

  message(STATUS "Generated protobuf for: ${PROTO_NAME}")
endforeach()
