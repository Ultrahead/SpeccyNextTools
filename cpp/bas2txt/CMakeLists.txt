# Require a reasonably modern version of CMake
cmake_minimum_required(VERSION 3.10)

# Name your project
project(bas2txt
        VERSION 1.0
        DESCRIPTION "ZX Spectrum BASIC-to-Text Converter"
        LANGUAGES CXX)

# Enforce C++17 standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED True)
set(CMAKE_CXX_EXTENSIONS OFF) # Ensures standard C++ without compiler-specific extensions

# Create the executable from your source files
# Double-check that these exact file names match what is in your folder!
add_executable(bas2txt bas2txt.cpp bas2txt.h)

# Add some helpful compiler warnings to keep the code squeaky clean
if(MSVC)
    target_compile_options(bas2txt PRIVATE /W4)
else()
    target_compile_options(bas2txt PRIVATE -Wall -Wextra -Wpedantic)
endif()

# Force static linking for MinGW (Rider on Windows)
# This guarantees your .exe won't complain about missing DLLs!
if(MINGW)
    target_link_options(bas2txt PRIVATE "-static")
endif()
