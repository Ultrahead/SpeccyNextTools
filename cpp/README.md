# **🚀 ZX Spectrum BASIC Tools (Txt2Bas & Bas2Txt)**

Welcome to the **ZX Spectrum BASIC Tools**\! This repository contains two lightning-fast, C++17 command-line utilities for working with classic ZX Spectrum \+3DOS BASIC files:

* **txt2bas**: Converts modern, readable plain-text scripts into tokenized, binary .bas files ready for the ZX Spectrum.  
* **bas2txt**: Reverses the process, taking binary .bas files and decoding them back into beautiful, editable plain text.

## **🛠️ Prerequisites**

Before you build, make sure you have the following installed on your system:

* **CMake** (Version 3.10 or higher)  
* A **C++17 compatible compiler**:  
  * *Windows*: MinGW-w64 (GCC) or MSVC (Visual Studio)  
  * *Linux*: GCC or Clang  
  * *Mac*: Apple Clang

*Note for Windows/MinGW users: The build system is configured to statically link the C++ libraries automatically. This means your compiled .exe files will be completely portable and won't require any missing DLLs\!*

## **⚙️ How to Compile (Manually with CMake)**

We use an "out-of-source" build process. This keeps your main source folder clean by putting all the generated build files into a separate build directory.

Open your terminal or command prompt in the root folder of this project (where your CMakeLists.txt is located) and run these commands step-by-step:

**1\. Create a build directory and navigate into it:**

mkdir build  
cd build

**2\. Configure the project with CMake:**

This tells CMake to find your compiler and generate the build files.

cmake ..

**3\. Compile the programs:**

This command actually builds your executables.

cmake \--build .

*(Optional: If you want to build a highly optimized release version, run cmake \--build . \--config Release instead).*

🎉 **Success\!** You will now find your executable tools sitting right there in your build directory\!

## **🚀 Usage**

Once compiled, you can run the tools directly from your terminal.

### **Convert Text to BASIC**

Takes your plain text script and creates a tokenized \+3DOS binary file.

./txt2bas input\_script.txt output\_game.bas

### **Convert BASIC to Text**

Takes a binary \+3DOS basic file and decodes it back into readable text.

./bas2txt input\_game.bas output\_script.txt

*Happy Retro Coding\! 👾*