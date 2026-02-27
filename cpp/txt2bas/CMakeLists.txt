# Require a reasonably modern version of CMake
cmake_minimum_required(VERSION 3.10)

# Name your project and optionally set a version
project(txt2bas
        VERSION 1.0
        DESCRIPTION "ZX Spectrum Text-to-BASIC Converter"
        LANGUAGES CXX)

# Enforce C++17 standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED True)
set(CMAKE_CXX_EXTENSIONS OFF) # Ensures standard C++ without compiler-specific extensions

# Create the executable from your source files
# I fixed the spelling of 'txt2bas.cpp' here! Make sure it matches your actual file name exactly.
add_executable(txt2bas txt2bas.cpp
        txt2bas.h)

# Optional: Add some helpful compiler warnings to keep the code squeaky clean
if(MSVC)
    target_compile_options(txt2bas PRIVATE /W4)
    # Note: MSVC handles static linking differently, usually via /MT
else()
    target_compile_options(txt2bas PRIVATE -Wall -Wextra -Wpedantic)
endif()

# Force static linking for MinGW so you don't need external DLLs!
if(MINGW)
    target_link_options(txt2bas PRIVATE "-static")
endif()
