# SPECCY NEXT TOOLS
The idea of this repository is to provide a set of tools that can be used for development for the ZX Spectrum Next computer.

## Text to Basic ... and Back ...

This is a port of the commandline tool ```txt2bas``` created by [Remy Sharp](https://zx.remysharp.com/) -which can be found in [this repo](https://github.com/remy/txt2bas/) as a [Node.JS-based](https://nodejs.org/en/) tool, which in turn is inspired by the ```.txt2bas``` dot command available on the ZX Spectrum Next.

The goal is not to replace such a great tool, but to provide a .NET alternative for basic ```+3DOS``` functionality that you can directly use and or integrate into your own projects, if needed. In this case, the provided functionality  converts plain text into tokenized (+3DOS basic) text that can be recognized by the ```Speccy Next```. So why is the tool so limited? Because I only need, right now, to get an autoexec.bas file like this ...

![](https://github.com/Ultrahead/SpeccyNextTools/blob/main/blob/autoexec.png)

In case you need the whole pack of features that the Node.JS app provides, please either follow the instructions in Remy's repo to install the tool and or use the web-based versions available [here](https://zx.remysharp.com/bas/).

Additionally, here you will find a .NET console app to convert a +3DOS basic tokenized file into plain text, known as ```bas2txt```.

## Building Steps

Just browse to the tool that you want to use, download the the provided VS Solution of said app and compile it with Visual Studio 2022, Visual Studio Code, Rider, or any other IDE of your preference. Yes, it's that simple! 

Eventually, I will be submitting compiled versions for the latest .NET version. Promise. But not now ...

## Usage

Open the commandline, move to the folder where the txt2bas.exe is located, and use the following syntax:  

```sh
txt2bas path/to/file.txt path/to/output.bas
```

If everything results ok, the app will produce the tokenized .bas file in the path you indicated with the choosen name.

Conversely, if you need to translate a .bas file into plain text, you can use bas2txt.exe as follows:

```sh
bas2txt path/to/file.bas path/to/output.txt
```

Again, if everything results ok, you will find the text file in the path you provided.

<ins>IMPORTANT</ins>: beforehand, you will have to properly install the version of the .NET Framework needed in order to run any of the apps.

## Final Words ...

Thanks to:
- Remy Sharp (for developing the Node.JS-based tool)
- The developers of the NextZXOS (for providing the original tool in the OS and make the assembly code public)
- Juan Segura Duran, aka "Duefectu" (for maintaining ZX Basic Studio, and his patience to all my questions)
- José Rodriguez, aka "Boriel" (for providing such a beatiful and useful language original baptized as Boriel Basic, and his infinite patience to my crazy requests for the language)

This is very important to keep in mind: <ins>the code comes as is with no support</ins>. So, in case you want to modify it to suit your needs, go ahead, grab the code and have fun experimenting with it!

Enjoy .. 🍻

