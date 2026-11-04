cmake_minimum_required(VERSION 3.16)
project(YugiRENativeDualCards VERSION 0.2 LANGUAGES CXX)
if(NOT WIN32 OR NOT CMAKE_SIZEOF_VOID_P EQUAL 4)
  message(FATAL_ERROR "This must be built for Windows x86 (Visual Studio -A Win32).")
endif()
add_library(yugire_rules SHARED
  src/engine.cpp
  src/cards/sparks.cpp
  src/cards/raimei.cpp
  src/exports.cpp
  yugire_rules.def
)
target_include_directories(yugire_rules PRIVATE include)
target_compile_features(yugire_rules PRIVATE cxx_std_17)
set_target_properties(yugire_rules PROPERTIES PREFIX "" OUTPUT_NAME "yugire_rules")
if(MSVC)
  target_compile_options(yugire_rules PRIVATE /W4)
  set_property(TARGET yugire_rules PROPERTY MSVC_RUNTIME_LIBRARY
    "MultiThreaded$<$<CONFIG:Debug>:Debug>")
else()
  target_compile_options(yugire_rules PRIVATE -Wall -Wextra)
endif()
