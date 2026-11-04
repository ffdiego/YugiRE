cmake_minimum_required(VERSION 3.16)
project(YugiRENativeSparks LANGUAGES CXX)

if(NOT WIN32 OR NOT CMAKE_SIZEOF_VOID_P EQUAL 4)
  message(FATAL_ERROR "Build requires 32-bit Windows: Visual Studio -A Win32 or MinGW i686.")
endif()

add_library(joey_rules SHARED joey_rules.cpp joey_rules.def)
target_compile_features(joey_rules PRIVATE cxx_std_17)
set_target_properties(joey_rules PROPERTIES OUTPUT_NAME "joey_rules" PREFIX "")

if(MSVC)
  target_compile_options(joey_rules PRIVATE /W4)
  # Small test DLL: avoid depending on a VC++ runtime installed on the game PC.
  set_property(TARGET joey_rules PROPERTY MSVC_RUNTIME_LIBRARY
    "MultiThreaded$<$<CONFIG:Debug>:Debug>")
else()
  target_compile_options(joey_rules PRIVATE -Wall -Wextra)
endif()
