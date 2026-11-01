cmake_minimum_required(VERSION 3.20)
project(VanTools.Bridge C)

# Use the C23 standard
set(CMAKE_C_STANDARD 23)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_C_EXTENSIONS OFF)

# Require the AOT output directory to be passed as a variable
if(NOT DEFINED AOT_DIR)
    message(FATAL_ERROR "AOT_DIR is not set. Please provide the path to the published C# AOT library (e.g., -DAOT_DIR=../../out/aot).")
endif()

add_library(VanTools.Bridge SHARED
    src/bridge.c
)

target_include_directories(VanTools.Bridge PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
)

# Link against the C# Native AOT library
# The AOT output has no "lib" prefix, so link the explicit file path instead of
# relying on -l lookup; passing the directory also records the rpath entry.
target_link_directories(VanTools.Bridge PRIVATE ${AOT_DIR})

if(WIN32)
    set(AOT_LIBRARY "${AOT_DIR}/VanTools.Bridge.Native.lib")
elseif(APPLE)
    set(AOT_LIBRARY "${AOT_DIR}/VanTools.Bridge.Native.dylib")
else()
    set(AOT_LIBRARY "${AOT_DIR}/VanTools.Bridge.Native.so")
endif()

target_link_libraries(VanTools.Bridge PRIVATE "${AOT_LIBRARY}")
