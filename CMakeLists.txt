# beatlesrockband - ReXGlue Recompiled Project
#
# Stable SDK integration lives in cmake/ReXGlue.cmake.

cmake_minimum_required(VERSION 3.25)
project(beatlesrockband LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(cmake/ReXGlue.cmake)

# Sources
set(BEATLESROCKBAND_SOURCES
    src/main.cpp
    src/math_compat.cpp
    src/guitar_input.cpp
    src/input_lock.cpp
    src/network_compat.cpp
    src/file_trace.cpp
    src/song_trace.cpp
    src/parts_trace.cpp
    src/random_compat.cpp
    src/audio_timing.cpp
    src/audio_trace.cpp
    src/windows_audio.cpp
    src/precise_sleep.cpp
    src/refill_trace.cpp
    src/audio_pacing.cpp
    src/frame_trace.cpp
    src/gpu_wait.cpp
    src/physical_input.cpp
)

if(WIN32)
    add_executable(beatlesrockband WIN32 ${BEATLESROCKBAND_SOURCES})
else()
    add_executable(beatlesrockband ${BEATLESROCKBAND_SOURCES})
endif()

rexglue_setup_target(beatlesrockband GPU_PLUGINS xenos)

if(WIN32)
    target_link_options(beatlesrockband PRIVATE /DEBUG /OPT:REF /OPT:ICF)
endif()

set_target_properties(beatlesrockband PROPERTIES OUTPUT_NAME "beatlesrockband-guitar")

target_link_libraries(beatlesrockband PRIVATE bcrypt synchronization xaudio2 ole32)
add_library(beatles-input SHARED src/controller_bridge.cpp)
target_link_libraries(beatles-input PRIVATE SDL3::SDL3-static)
target_compile_definitions(beatles-input PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
target_link_libraries(beatlesrockband PRIVATE beatles-input)

add_executable(beatles-content src/content_import.cpp)
target_link_libraries(beatles-content PRIVATE rex::runtime)
target_compile_definitions(beatles-content PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)

option(BEATLES_BUILD_AUDIO_TESTS "Build native Windows audio lifecycle tests" OFF)
if(BEATLES_BUILD_AUDIO_TESTS AND WIN32)
    add_executable(windows-audio-tests tests/WindowsAudioTests.cpp)
    target_link_libraries(windows-audio-tests PRIVATE rex::runtime xaudio2 ole32)
    target_compile_definitions(windows-audio-tests PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
endif()



