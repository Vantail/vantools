cmake_minimum_required(VERSION 3.20)
project(VanTools.Bridge C)

set(CMAKE_C_STANDARD 11)

add_library(VanTools.Bridge SHARED
    src/bridge.c
)

target_include_directories(VanTools.Bridge PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
)
