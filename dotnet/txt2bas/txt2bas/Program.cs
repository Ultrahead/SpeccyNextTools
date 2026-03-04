// File   Program_txt2bas.cs
// Brief  Implementation file for the process that converts plain text into a +3DOS file.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Txt2Bas
{
    public class Plus3Dos
    {
        public static byte[] CreateHeader(int basicLength, int autoStartLine)
        {
            byte[] header = new byte[128];
            byte[] sig = Encoding.ASCII.GetBytes("PLUS3DOS");
            Array.Copy(sig, header, sig.Length);
            
            header[8] = 0x1A; // Soft EOF
            header[9] = 0x01; // Issue number
            header[10] = 0x00; // Version number

            int totalFileSize = basicLength + 128;
            header[11] = (byte)(totalFileSize & 0xFF);
            header[12] = (byte)((totalFileSize >> 8) & 0xFF);
            header[13] = (byte)((totalFileSize >> 16) & 0xFF);
            header[14] = (byte)((totalFileSize >> 24) & 0xFF);

            header[15] = 0x00; // Type: Program
            
            header[16] = (byte)(basicLength & 0xFF);
            header[17] = (byte)((basicLength >> 8) & 0xFF);

            if (autoStartLine >= 0 && autoStartLine < 32768)
            {
                header[18] = (byte)(autoStartLine & 0xFF);
                header[19] = (byte)((autoStartLine >> 8) & 0xFF);
            }
            else
            {
                header[18] = 0x00;
                header[19] = 0x80; // 32768
            }

            header[20] = (byte)(basicLength & 0xFF);
            header[21] = (byte)((basicLength >> 8) & 0xFF);

            int sum = 0;
            for (int i = 0; i < 127; i++)
            {
                sum += header[i];
            }
            header[127] = (byte)(sum % 256);

            return header;
        }
    }

    public class SinclairNumber
    {
        public static byte[] Pack(double number)
        {
            if (number % 1 == 0 && number >= -65535.0 && number <= 65535.0)
            {
                int val = (int)number;
                byte sign = val < 0 ? (byte)0xFF : (byte)0x00;
                if (val < 0) val = -val;

                return new byte[] { 0x00, sign, (byte)(val & 0xFF), (byte)((val >> 8) & 0xFF), 0x00 };
            }
            return new byte[] { 0, 0, 0, 0, 0 };
        }
    }

    public class TokenMap
    {
        public Dictionary<string, byte> Map { get; private set; }

        public TokenMap()
        {
            Map = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

            // ZX Spectrum Next Extensions
            Map["PEEK$"] = 0x87; Map["REG"] = 0x88; Map["DPOKE"] = 0x89; Map["DPEEK"] = 0x8A;
            Map["MOD"] = 0x8B; Map["<<"] = 0x8C; Map[">>"] = 0x8D; Map["UNTIL"] = 0x8E;
            Map["ERROR"] = 0x8F; Map["ON"] = 0x90; Map["DEFPROC"] = 0x91; Map["ENDPROC"] = 0x92;
            Map["PROC"] = 0x93; Map["LOCAL"] = 0x94; Map["DRIVER"] = 0x95; Map["WHILE"] = 0x96;
            Map["REPEAT"] = 0x97; Map["ELSE"] = 0x98; Map["REMOUNT"] = 0x99; Map["BANK"] = 0x9A;
            Map["TILE"] = 0x9B; Map["LAYER"] = 0x9C; Map["PALETTE"] = 0x9D; Map["SPRITE"] = 0x9E;
            Map["PWD"] = 0x9F; Map["CD"] = 0xA0; Map["MKDIR"] = 0xA1; Map["RMDIR"] = 0xA2;

            // Standard Sinclair BASIC
            Map["SPECTRUM"] = 0xA3; Map["PLAY"] = 0xA4; Map["RND"] = 0xA5; Map["INKEY$"] = 0xA6;
            Map["PI"] = 0xA7; Map["FN"] = 0xA8; Map["POINT"] = 0xA9; Map["SCREEN$"] = 0xAA;
            Map["ATTR"] = 0xAB; Map["AT"] = 0xAC; Map["TAB"] = 0xAD; Map["VAL$"] = 0xAE;
            Map["CODE"] = 0xAF; Map["VAL"] = 0xB0; Map["LEN"] = 0xB1; Map["SIN"] = 0xB2;
            Map["COS"] = 0xB3; Map["TAN"] = 0xB4; Map["ASN"] = 0xB5; Map["ACS"] = 0xB6;
            Map["ATN"] = 0xB7; Map["LN"] = 0xB8; Map["EXP"] = 0xB9; Map["INT"] = 0xBA;
            Map["SQR"] = 0xBB; Map["SGN"] = 0xBC; Map["ABS"] = 0xBD; Map["PEEK"] = 0xBE;
            Map["IN"] = 0xBF; Map["USR"] = 0xC0; Map["STR$"] = 0xC1; Map["CHR$"] = 0xC2;
            Map["NOT"] = 0xC3; Map["BIN"] = 0xC4; Map["OR"] = 0xC5; Map["AND"] = 0xC6;
            Map["<="] = 0xC7; Map[">="] = 0xC8; Map["<>"] = 0xC9; Map["LINE"] = 0xCA;
            Map["THEN"] = 0xCB; Map["TO"] = 0xCC; Map["STEP"] = 0xCD; Map["DEF FN"] = 0xCE;
            Map["CAT"] = 0xCF; Map["FORMAT"] = 0xD0; Map["MOVE"] = 0xD1; Map["ERASE"] = 0xD2;
            Map["OPEN #"] = 0xD3; Map["CLOSE #"] = 0xD4; Map["MERGE"] = 0xD5; Map["VERIFY"] = 0xD6;
            Map["BEEP"] = 0xD7; Map["CIRCLE"] = 0xD8; Map["INK"] = 0xD9; Map["PAPER"] = 0xDA;
            Map["FLASH"] = 0xDB; Map["BRIGHT"] = 0xDC; Map["INVERSE"] = 0xDD; Map["OVER"] = 0xDE;
            Map["OUT"] = 0xDF; Map["LPRINT"] = 0xE0; Map["LLIST"] = 0xE1; Map["STOP"] = 0xE2;
            Map["READ"] = 0xE3; Map["DATA"] = 0xE4; Map["RESTORE"] = 0xE5; Map["NEW"] = 0xE6;
            Map["BORDER"] = 0xE7; Map["CONTINUE"] = 0xE8; Map["DIM"] = 0xE9; Map["REM"] = 0xEA;
            Map["FOR"] = 0xEB; Map["GO TO"] = 0xEC; Map["GOTO"] = 0xEC; Map["GO SUB"] = 0xED;
            Map["GOSUB"] = 0xED; Map["INPUT"] = 0xEE; Map["LOAD"] = 0xEF; Map["LIST"] = 0xF0;
            Map["LET"] = 0xF1; Map["PAUSE"] = 0xF2; Map["NEXT"] = 0xF3; Map["POKE"] = 0xF4;
            Map["PRINT"] = 0xF5; Map["PLOT"] = 0xF6; Map["RUN"] = 0xF7; Map["SAVE"] = 0xF8;
            Map["RANDOMIZE"] = 0xF9; Map["IF"] = 0xFA; Map["CLS"] = 0xFB; Map["DRAW"] = 0xFC;
            Map["CLEAR"] = 0xFD; Map["RETURN"] = 0xFE; Map["COPY"] = 0xFF;
        }
    }

    public class BasConverter
    {
        private readonly TokenMap _tokenMap;
        private readonly List<string> _sortedKeys;
        public int AutoStartLine { get; private set; } = 32768;

        public BasConverter()
        {
            _tokenMap = new TokenMap();
            _sortedKeys = _tokenMap.Map.Keys.ToList();
            _sortedKeys.Sort((a, b) => b.Length.CompareTo(a.Length));
        }

        public byte[] ConvertFile(string path)
        {
            List<byte> output = new List<byte>();
            string[] lines = File.ReadAllLines(path);
            int currentLineNum = 10;
            Regex lineRegex = new Regex(@"^(\d+)\s+(.*)");

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("#"))
                {
                    string lowerLine = line.ToLowerInvariant();
                    if (lowerLine.StartsWith("#autostart"))
                    {
                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1 && int.TryParse(parts[1], out int val))
                        {
                            AutoStartLine = val;
                        }
                    }
                    continue;
                }

                int lineNum = currentLineNum;
                string restOfLine = line;
                Match match = lineRegex.Match(line);

                if (match.Success)
                {
                    lineNum = int.Parse(match.Groups[1].Value);
                    restOfLine = match.Groups[2].Value;
                    currentLineNum = lineNum + 10;
                }
                else
                {
                    currentLineNum += 10;
                }

                output.AddRange(ParseLine(lineNum, restOfLine));
            }

            return output.ToArray();
        }

        private byte[] ParseLine(int lineNum, string text)
        {
            List<byte> lineData = new List<byte>();
            bool expectCommand = true;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ')
                {
                    lineData.Add((byte)' ');
                    continue;
                }

                if (expectCommand && text[i] == '.')
                {
                    if (i + 1 < text.Length && char.IsDigit(text[i + 1]))
                    {
                        // Number starting with '.', let it fall through
                    }
                    else
                    {
                        string dotCmd = text.Substring(i);
                        lineData.AddRange(Encoding.ASCII.GetBytes(dotCmd));
                        break;
                    }
                }

                if (text[i] == '"')
                {
                    expectCommand = false;
                    int end = text.IndexOf('"', i + 1);
                    if (end == -1)
                    {
                        lineData.AddRange(Encoding.ASCII.GetBytes(text.Substring(i)));
                        i = text.Length;
                    }
                    else
                    {
                        lineData.AddRange(Encoding.ASCII.GetBytes(text.Substring(i, end - i + 1)));
                        i = end;
                    }
                    continue;
                }

                if (char.IsDigit(text[i]) || (text[i] == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
                {
                    expectCommand = false;
                    string numStr = "";
                    int j = i;
                    while (j < text.Length && (char.IsDigit(text[j]) || text[j] == '.'))
                    {
                        numStr += text[j++];
                    }

                    if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                    {
                        lineData.AddRange(Encoding.ASCII.GetBytes(numStr));
                        lineData.Add(0x0E);
                        lineData.AddRange(SinclairNumber.Pack(val));
                        i = j - 1;
                        continue;
                    }
                }

                bool matched = false;
                foreach (string k in _sortedKeys)
                {
                    if (i + k.Length > text.Length) continue;
                    if (!text.Substring(i, k.Length).Equals(k, StringComparison.OrdinalIgnoreCase)) continue;

                    bool isAlpha = char.IsLetter(k[0]);
                    bool pOk = (i == 0) || !char.IsLetter(text[i - 1]);
                    bool nOk = (i + k.Length >= text.Length) || !char.IsLetterOrDigit(text[i + k.Length]);

                    if (isAlpha && (!pOk || !nOk)) continue;

                    byte token = _tokenMap.Map[k];
                    lineData.Add(token);
                    i += k.Length;
                    matched = true;

                    if (token == 0xEA) // REM
                    {
                        expectCommand = false;
                        if (i < text.Length)
                        {
                            lineData.AddRange(Encoding.ASCII.GetBytes(text.Substring(i)));
                            i = text.Length;
                        }
                    }
                    else
                    {
                        if (token == 0xCB || token == 0x98) // THEN or ELSE
                            expectCommand = true;
                        else
                            expectCommand = false;

                        while (i < text.Length && text[i] == ' ') i++;
                    }
                    i--;
                    break;
                }

                if (!matched)
                {
                    lineData.Add((byte)text[i]);
                    expectCommand = (text[i] == ':');
                }
            }

            lineData.Add(0x0D);

            List<byte> result = new List<byte>();
            result.Add((byte)((lineNum >> 8) & 0xFF));
            result.Add((byte)(lineNum & 0xFF));

            int len = lineData.Count;
            result.Add((byte)(len & 0xFF));
            result.Add((byte)((len >> 8) & 0xFF));

            result.AddRange(lineData);
            return result.ToArray();
        }
    }

    /// <summary>
    /// Main entry point for the application.
    /// Handles command line arguments and orchestrates the conversion process.
    /// </summary>
    internal static class Program
    {
        private const string ToolVersion = "1.0";

        static void PrintHelp()
        {
            Console.WriteLine($"ZX Spectrum Text-to-BASIC Converter v{ToolVersion}");
            Console.WriteLine("Usage: txt2bas [options] <input.txt> <output.bas>\n");
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
                if (arg == "-v" || arg == "--version") { Console.WriteLine($"txt2bas version {ToolVersion}"); return 0; }
            }

            if (args.Length < 2)
            {
                Console.WriteLine("Usage: txt2bas <input.txt> <output.bas>");
                Console.WriteLine("See 'txt2bas --help' for details.");
                return 0;
            }

            try
            {
                BasConverter conv = new BasConverter();
                byte[] bytes = conv.ConvertFile(args[0]);
                byte[] head = Plus3Dos.CreateHeader(bytes.Length, conv.AutoStartLine);

                using (FileStream fs = new FileStream(args[1], FileMode.Create, FileAccess.Write))
                {
                    fs.Write(head, 0, head.Length);
                    fs.Write(bytes, 0, bytes.Length);
                }

                Console.WriteLine($"Successfully created {args[1]} (v{ToolVersion})");
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
