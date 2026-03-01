cmake_minimum_required(VERSION 3.10)
project(Txt2Bas VERSION 1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED True)
set(CMAKE_CXX_EXTENSIONS OFF)

add_executable(txt2bas txt2bas.cpp txt2bas.h)
target_compile_definitions(txt2bas PRIVATE TOOL_VERSION="${PROJECT_VERSION}")

if(MSVC)
    target_compile_options(txt2bas PRIVATE /W4)
else()
    target_compile_options(txt2bas PRIVATE -Wall -Wextra -Wpedantic)
endif()

if(MINGW)
    target_link_options(txt2bas PRIVATE "-static")
endif()

# Installation logic
# This ensures that 'cmake --install .' will place the executable in the correct prefix
install(TARGETS txt2bas DESTINATION bin)