// File  Program_bas2txt.cs
// Brief Implementation file for the process that converts a +3DOS file into plain text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Bas2Txt
{
    public class ReverseTokenMap
    {
        public Dictionary<byte, string> Map { get; private set; }

        public ReverseTokenMap()
        {
            Map = new Dictionary<byte, string>();

            // ZX Spectrum Next Extensions
            Map[0x87] = "PEEK$"; Map[0x88] = "REG"; Map[0x89] = "DPOKE"; Map[0x8A] = "DPEEK";
            Map[0x8B] = "MOD"; Map[0x8C] = "<<"; Map[0x8D] = ">>"; Map[0x8E] = "UNTIL";
            Map[0x8F] = "ERROR"; Map[0x90] = "ON"; Map[0x91] = "DEFPROC"; Map[0x92] = "ENDPROC";
            Map[0x93] = "PROC"; Map[0x94] = "LOCAL"; Map[0x95] = "DRIVER"; Map[0x96] = "WHILE";
            Map[0x97] = "REPEAT"; Map[0x98] = "ELSE"; Map[0x99] = "REMOUNT"; Map[0x9A] = "BANK";
            Map[0x9B] = "TILE"; Map[0x9C] = "LAYER"; Map[0x9D] = "PALETTE"; Map[0x9E] = "SPRITE";
            Map[0x9F] = "PWD"; Map[0xA0] = "CD"; Map[0xA1] = "MKDIR"; Map[0xA2] = "RMDIR";

            // Standard Sinclair BASIC
            Map[0xA3] = "SPECTRUM"; Map[0xA4] = "PLAY"; Map[0xA5] = "RND"; Map[0xA6] = "INKEY$";
            Map[0xA7] = "PI"; Map[0xA8] = "FN"; Map[0xA9] = "POINT"; Map[0xAA] = "SCREEN$";
            Map[0xAB] = "ATTR"; Map[0xAC] = "AT"; Map[0xAD] = "TAB"; Map[0xAE] = "VAL$";
            Map[0xAF] = "CODE"; Map[0xB0] = "VAL"; Map[0xB1] = "LEN"; Map[0xB2] = "SIN";
            Map[0xB3] = "COS"; Map[0xB4] = "TAN"; Map[0xB5] = "ASN"; Map[0xB6] = "ACS";
            Map[0xB7] = "ATN"; Map[0xB8] = "LN"; Map[0xB9] = "EXP"; Map[0xBA] = "INT";
            Map[0xBB] = "SQR"; Map[0xBC] = "SGN"; Map[0xBD] = "ABS"; Map[0xBE] = "PEEK";
            Map[0xBF] = "IN"; Map[0xC0] = "USR"; Map[0xC1] = "STR$"; Map[0xC2] = "CHR$";
            Map[0xC3] = "NOT"; Map[0xC4] = "BIN"; Map[0xC5] = "OR"; Map[0xC6] = "AND";
            Map[0xC7] = "<="; Map[0xC8] = ">="; Map[0xC9] = "<>"; Map[0xCA] = "LINE";
            Map[0xCB] = "THEN"; Map[0xCC] = "TO"; Map[0xCD] = "STEP"; Map[0xCE] = "DEF FN";
            Map[0xCF] = "CAT"; Map[0xD0] = "FORMAT"; Map[0xD1] = "MOVE"; Map[0xD2] = "ERASE";
            Map[0xD3] = "OPEN #"; Map[0xD4] = "CLOSE #"; Map[0xD5] = "MERGE"; Map[0xD6] = "VERIFY";
            Map[0xD7] = "BEEP"; Map[0xD8] = "CIRCLE"; Map[0xD9] = "INK"; Map[0xDA] = "PAPER";
            Map[0xDB] = "FLASH"; Map[0xDC] = "BRIGHT"; Map[0xDD] = "INVERSE"; Map[0xDE] = "OVER";
            Map[0xDF] = "OUT"; Map[0xE0] = "LPRINT"; Map[0xE1] = "LLIST"; Map[0xE2] = "STOP";
            Map[0xE3] = "READ"; Map[0xE4] = "DATA"; Map[0xE5] = "RESTORE"; Map[0xE6] = "NEW";
            Map[0xE7] = "BORDER"; Map[0xE8] = "CONTINUE"; Map[0xE9] = "DIM"; Map[0xEA] = "REM";
            Map[0xEB] = "FOR"; Map[0xEC] = "GO TO"; Map[0xED] = "GO SUB"; Map[0xEE] = "INPUT";
            Map[0xEF] = "LOAD"; Map[0xF0] = "LIST"; Map[0xF1] = "LET"; Map[0xF2] = "PAUSE";
            Map[0xF3] = "NEXT"; Map[0xF4] = "POKE"; Map[0xF5] = "PRINT"; Map[0xF6] = "PLOT";
            Map[0xF7] = "RUN"; Map[0xF8] = "SAVE"; Map[0xF9] = "RANDOMIZE"; Map[0xFA] = "IF";
            Map[0xFB] = "CLS"; Map[0xFC] = "DRAW"; Map[0xFD] = "CLEAR"; Map[0xFE] = "RETURN";
            Map[0xFF] = "COPY";
        }
    }

    public class BasParser
    {
        private readonly ReverseTokenMap _reverseTokenMap;

        public BasParser()
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

                if (_reverseTokenMap.Map.TryGetValue(b, out string tokenStr))
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
                else if (b >= 32 && b <= 126)
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
