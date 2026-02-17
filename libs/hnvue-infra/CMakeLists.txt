cmake_minimum_required(VERSION 3.25)

# hnvue-infra - Infrastructure utilities
# SPEC-INFRA-001: Foundation utilities library (logging, config, etc.)

project(hnvue-infra
    VERSION 0.1.0
    DESCRIPTION "HnVue infrastructure utilities"
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
add_library(HnVue::infra ALIAS ${PROJECT_NAME})

# TODO: Add dependencies as needed
# target_link_libraries(${PROJECT_NAME}
#     PUBLIC
#         HnVue::<dependency>
# )

# TODO: Add install rules
# install(TARGETS ${PROJECT_NAME} EXPORT HnVueTargets)
