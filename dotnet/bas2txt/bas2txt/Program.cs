// File  Program_bas2txt.cs
// Brief Implementation file for the process that converts a +3DOS file into plain text.

using System.Text;

namespace Bas2Txt
{
    internal class ReverseTokenMap
    {
        public Dictionary<byte, string> Map { get; private set; }

        internal ReverseTokenMap()
        {
            Map = new Dictionary<byte, string>
            {
                // ZX Spectrum Next Extensions
                [0x87] = "PEEK$",
                [0x88] = "REG",
                [0x89] = "DPOKE",
                [0x8A] = "DPEEK",
                [0x8B] = "MOD",
                [0x8C] = "<<",
                [0x8D] = ">>",
                [0x8E] = "UNTIL",
                [0x8F] = "ERROR",
                [0x90] = "ON",
                [0x91] = "DEFPROC",
                [0x92] = "ENDPROC",
                [0x93] = "PROC",
                [0x94] = "LOCAL",
                [0x95] = "DRIVER",
                [0x96] = "WHILE",
                [0x97] = "REPEAT",
                [0x98] = "ELSE",
                [0x99] = "REMOUNT",
                [0x9A] = "BANK",
                [0x9B] = "TILE",
                [0x9C] = "LAYER",
                [0x9D] = "PALETTE",
                [0x9E] = "SPRITE",
                [0x9F] = "PWD",
                [0xA0] = "CD",
                [0xA1] = "MKDIR",
                [0xA2] = "RMDIR",
                // Standard Sinclair BASIC
                [0xA3] = "SPECTRUM",
                [0xA4] = "PLAY",
                [0xA5] = "RND",
                [0xA6] = "INKEY$",
                [0xA7] = "PI",
                [0xA8] = "FN",
                [0xA9] = "POINT",
                [0xAA] = "SCREEN$",
                [0xAB] = "ATTR",
                [0xAC] = "AT",
                [0xAD] = "TAB",
                [0xAE] = "VAL$",
                [0xAF] = "CODE",
                [0xB0] = "VAL",
                [0xB1] = "LEN",
                [0xB2] = "SIN",
                [0xB3] = "COS",
                [0xB4] = "TAN",
                [0xB5] = "ASN",
                [0xB6] = "ACS",
                [0xB7] = "ATN",
                [0xB8] = "LN",
                [0xB9] = "EXP",
                [0xBA] = "INT",
                [0xBB] = "SQR",
                [0xBC] = "SGN",
                [0xBD] = "ABS",
                [0xBE] = "PEEK",
                [0xBF] = "IN",
                [0xC0] = "USR",
                [0xC1] = "STR$",
                [0xC2] = "CHR$",
                [0xC3] = "NOT",
                [0xC4] = "BIN",
                [0xC5] = "OR",
                [0xC6] = "AND",
                [0xC7] = "<=",
                [0xC8] = ">=",
                [0xC9] = "<>",
                [0xCA] = "LINE",
                [0xCB] = "THEN",
                [0xCC] = "TO",
                [0xCD] = "STEP",
                [0xCE] = "DEF FN",
                [0xCF] = "CAT",
                [0xD0] = "FORMAT",
                [0xD1] = "MOVE",
                [0xD2] = "ERASE",
                [0xD3] = "OPEN #",
                [0xD4] = "CLOSE #",
                [0xD5] = "MERGE",
                [0xD6] = "VERIFY",
                [0xD7] = "BEEP",
                [0xD8] = "CIRCLE",
                [0xD9] = "INK",
                [0xDA] = "PAPER",
                [0xDB] = "FLASH",
                [0xDC] = "BRIGHT",
                [0xDD] = "INVERSE",
                [0xDE] = "OVER",
                [0xDF] = "OUT",
                [0xE0] = "LPRINT",
                [0xE1] = "LLIST",
                [0xE2] = "STOP",
                [0xE3] = "READ",
                [0xE4] = "DATA",
                [0xE5] = "RESTORE",
                [0xE6] = "NEW",
                [0xE7] = "BORDER",
                [0xE8] = "CONTINUE",
                [0xE9] = "DIM",
                [0xEA] = "REM",
                [0xEB] = "FOR",
                [0xEC] = "GO TO",
                [0xED] = "GO SUB",
                [0xEE] = "INPUT",
                [0xEF] = "LOAD",
                [0xF0] = "LIST",
                [0xF1] = "LET",
                [0xF2] = "PAUSE",
                [0xF3] = "NEXT",
                [0xF4] = "POKE",
                [0xF5] = "PRINT",
                [0xF6] = "PLOT",
                [0xF7] = "RUN",
                [0xF8] = "SAVE",
                [0xF9] = "RANDOMIZE",
                [0xFA] = "IF",
                [0xFB] = "CLS",
                [0xFC] = "DRAW",
                [0xFD] = "CLEAR",
                [0xFE] = "RETURN",
                [0xFF] = "COPY"
            };
        }
    }

    internal class BasParser
    {
        private readonly ReverseTokenMap _reverseTokenMap;

        internal BasParser()
        {
            _reverseTokenMap = new ReverseTokenMap();
        }

        public string Parse(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            int offset = 0;

            if (data.Length >= 128)
            {
                string sig = Encoding.ASCII.GetString(data, 0, 8);
                if (sig == "PLUS3DOS" || sig.StartsWith("ZXPLUS3"))
                {
                    int autoStart = data[18] | (data[19] << 8);
                    if (autoStart != 32768)
                    {
                        sb.AppendLine($"#autostart {autoStart}");
                    }
                    offset = 128;
                }
            }

            while (offset < data.Length)
            {
                if (offset + 4 > data.Length) break;

                int lineNum = (data[offset] << 8) | data[offset + 1];
                int lineLen = data[offset + 2] | (data[offset + 3] << 8);
                offset += 4;

                if (offset + lineLen - 1 > data.Length) break;

                sb.AppendLine($"{lineNum} {DecodeLineData(data, offset, lineLen - 1)}");
                offset += lineLen;
            }

            return sb.ToString();
        }

        private string DecodeLineData(byte[] data, int start, int length)
        {
            StringBuilder sb = new StringBuilder();
            int end = start + length;

            for (int i = start; i < end; i++)
            {
                byte b = data[i];

                if (b == 0x0E)
                {
                    i += 5;
                    continue;
                }

                if (_reverseTokenMap.Map.TryGetValue(b, out string? tokenStr))
                {
                    sb.Append(tokenStr);
                    if (i + 1 < end)
                    {
                        byte next = data[i + 1];
                        if (next < 128 && next != 0x0E)
                        {
                            char c = (char)next;
                            if (char.IsLetterOrDigit(c) || c == '"' || c == '.')
                            {
                                sb.Append(' ');
                            }
                        }
                    }
                }
                else if (b is >= 32 and <= 126)
                {
                    sb.Append((char)b);
                }
                else if (b == 0x7F)
                {
                    sb.Append("\u00A9"); // Copyright symbol
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Main entry point for the application.
    /// Converts a binary BASIC file (.bas) to a UTF-8 Text file.
    /// </summary>
    internal static class Program
    {
        private const string ToolVersion = "1.0";

        static void PrintHelp()
        {
            Console.WriteLine($"ZX Spectrum BASIC-to-Text Converter v{ToolVersion}");
            Console.WriteLine("Usage: bas2txt [options] <input.bas> <output.txt>\n");
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --help     Show help");
            Console.WriteLine("  -v, --version  Show version");
        }

        static int Main(string[] args)
        {
            if (args.Length >= 1)
            {
                string arg = args[0];
                if (arg == "-h" || arg == "--help") { PrintHelp(); return 0; }
                if (arg == "-v" || arg == "--version") { Console.WriteLine($"bas2txt version {ToolVersion}"); return 0; }
            }

            if (args.Length < 2)
            {
                Console.WriteLine("Usage: bas2txt <input.bas> <output.txt>");
                Console.WriteLine("See 'bas2txt --help' for details.");
                return 0;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(args[0]);
                BasParser parser = new BasParser();
                string output = parser.Parse(bytes);

                File.WriteAllText(args[1], output, new UTF8Encoding(false)); // Write without BOM
                Console.WriteLine($"Successfully decoded {args[0]} (v{ToolVersion})");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
    }
}
