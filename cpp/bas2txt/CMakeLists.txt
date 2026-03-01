cmake_minimum_required(VERSION 3.10)

# Updated project version to 1.0
project(Bas2Txt
        VERSION 1.0
        DESCRIPTION "ZX Spectrum BASIC-to-Text Converter"
        LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED True)
set(CMAKE_CXX_EXTENSIONS OFF)

add_executable(bas2txt bas2txt.cpp bas2txt.h)

# Inject the version into the source code
target_compile_definitions(bas2txt PRIVATE TOOL_VERSION="${PROJECT_VERSION}")

if(MSVC)
    target_compile_options(bas2txt PRIVATE /W4)
else()
    target_compile_options(bas2txt PRIVATE -Wall -Wextra -Wpedantic)
endif()

if(MINGW)
    target_link_options(bas2txt PRIVATE "-static")
endif()

# Installation logic
install(TARGETS bas2txt DESTINATION bin)
