cmake_minimum_required(VERSION 3.16)
project(YugiRENativeDualCards VERSION 0.6 LANGUAGES CXX)
if(NOT WIN32 OR NOT CMAKE_SIZEOF_VOID_P EQUAL 4)
  message(FATAL_ERROR "This must be built for Windows x86 (Visual Studio -A Win32).")
endif()
add_library(yugire_rules SHARED
  src/engine.cpp
  src/duel_state.cpp
  src/effect_context.cpp
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

# Small native test for the reconstructed CardRequest offsets. This test does
# not load or call the Joey engine.
enable_testing()
add_executable(test_card_request tests/test_card_request.cpp)
target_include_directories(test_card_request PRIVATE include)
target_compile_features(test_card_request PRIVATE cxx_std_17)
add_test(NAME card_request_offsets COMMAND test_card_request)

# Pure C++ regression test with an engine mock: no live Joey process needed.
add_executable(test_effect_context tests/test_effect_context.cpp src/effect_context.cpp)
target_include_directories(test_effect_context PRIVATE include)
target_compile_features(test_effect_context PRIVATE cxx_std_17)
add_test(NAME effect_context COMMAND test_effect_context)

add_executable(test_duel_state tests/test_duel_state.cpp src/duel_state.cpp)
target_include_directories(test_duel_state PRIVATE include)
target_compile_features(test_duel_state PRIVATE cxx_std_17)
add_test(NAME duel_state COMMAND test_duel_state)
