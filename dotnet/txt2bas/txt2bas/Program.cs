// File   Program_txt2bas.cs
// Brief  Implementation file for the process that converts plain text into a +3DOS file.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Txt2Bas
{
    internal static class Plus3Dos
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

            if (autoStartLine is >= 0 and < 32768)
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

    internal abstract class SinclairNumber
    {
        public static byte[] Pack(double number)
        {
            if (number % 1 == 0 && number is >= -65535.0 and <= 65535.0)
            {
                int val = (int)number;
                byte sign = val < 0 ? (byte)0xFF : (byte)0x00;
                if (val < 0) val = -val;

                return [0x00, sign, (byte)(val & 0xFF), (byte)((val >> 8) & 0xFF), 0x00];
            }
            return "\0\0\0\0\0"u8.ToArray();
        }
    }

    internal class TokenMap
    {
        public Dictionary<string, byte> Map { get; private set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            // ZX Spectrum Next Extensions
            ["PEEK$"] = 0x87,
            ["REG"] = 0x88,
            ["DPOKE"] = 0x89,
            ["DPEEK"] = 0x8A,
            ["MOD"] = 0x8B,
            ["<<"] = 0x8C,
            [">>"] = 0x8D,
            ["UNTIL"] = 0x8E,
            ["ERROR"] = 0x8F,
            ["ON"] = 0x90,
            ["DEFPROC"] = 0x91,
            ["ENDPROC"] = 0x92,
            ["PROC"] = 0x93,
            ["LOCAL"] = 0x94,
            ["DRIVER"] = 0x95,
            ["WHILE"] = 0x96,
            ["REPEAT"] = 0x97,
            ["ELSE"] = 0x98,
            ["REMOUNT"] = 0x99,
            ["BANK"] = 0x9A,
            ["TILE"] = 0x9B,
            ["LAYER"] = 0x9C,
            ["PALETTE"] = 0x9D,
            ["SPRITE"] = 0x9E,
            ["PWD"] = 0x9F,
            ["CD"] = 0xA0,
            ["MKDIR"] = 0xA1,
            ["RMDIR"] = 0xA2,
            // Standard Sinclair BASIC
            ["SPECTRUM"] = 0xA3,
            ["PLAY"] = 0xA4,
            ["RND"] = 0xA5,
            ["INKEY$"] = 0xA6,
            ["PI"] = 0xA7,
            ["FN"] = 0xA8,
            ["POINT"] = 0xA9,
            ["SCREEN$"] = 0xAA,
            ["ATTR"] = 0xAB,
            ["AT"] = 0xAC,
            ["TAB"] = 0xAD,
            ["VAL$"] = 0xAE,
            ["CODE"] = 0xAF,
            ["VAL"] = 0xB0,
            ["LEN"] = 0xB1,
            ["SIN"] = 0xB2,
            ["COS"] = 0xB3,
            ["TAN"] = 0xB4,
            ["ASN"] = 0xB5,
            ["ACS"] = 0xB6,
            ["ATN"] = 0xB7,
            ["LN"] = 0xB8,
            ["EXP"] = 0xB9,
            ["INT"] = 0xBA,
            ["SQR"] = 0xBB,
            ["SGN"] = 0xBC,
            ["ABS"] = 0xBD,
            ["PEEK"] = 0xBE,
            ["IN"] = 0xBF,
            ["USR"] = 0xC0,
            ["STR$"] = 0xC1,
            ["CHR$"] = 0xC2,
            ["NOT"] = 0xC3,
            ["BIN"] = 0xC4,
            ["OR"] = 0xC5,
            ["AND"] = 0xC6,
            ["<="] = 0xC7,
            [">="] = 0xC8,
            ["<>"] = 0xC9,
            ["LINE"] = 0xCA,
            ["THEN"] = 0xCB,
            ["TO"] = 0xCC,
            ["STEP"] = 0xCD,
            ["DEF FN"] = 0xCE,
            ["CAT"] = 0xCF,
            ["FORMAT"] = 0xD0,
            ["MOVE"] = 0xD1,
            ["ERASE"] = 0xD2,
            ["OPEN #"] = 0xD3,
            ["CLOSE #"] = 0xD4,
            ["MERGE"] = 0xD5,
            ["VERIFY"] = 0xD6,
            ["BEEP"] = 0xD7,
            ["CIRCLE"] = 0xD8,
            ["INK"] = 0xD9,
            ["PAPER"] = 0xDA,
            ["FLASH"] = 0xDB,
            ["BRIGHT"] = 0xDC,
            ["INVERSE"] = 0xDD,
            ["OVER"] = 0xDE,
            ["OUT"] = 0xDF,
            ["LPRINT"] = 0xE0,
            ["LLIST"] = 0xE1,
            ["STOP"] = 0xE2,
            ["READ"] = 0xE3,
            ["DATA"] = 0xE4,
            ["RESTORE"] = 0xE5,
            ["NEW"] = 0xE6,
            ["BORDER"] = 0xE7,
            ["CONTINUE"] = 0xE8,
            ["DIM"] = 0xE9,
            ["REM"] = 0xEA,
            ["FOR"] = 0xEB,
            ["GO TO"] = 0xEC,
            ["GOTO"] = 0xEC,
            ["GO SUB"] = 0xED,
            ["GOSUB"] = 0xED,
            ["INPUT"] = 0xEE,
            ["LOAD"] = 0xEF,
            ["LIST"] = 0xF0,
            ["LET"] = 0xF1,
            ["PAUSE"] = 0xF2,
            ["NEXT"] = 0xF3,
            ["POKE"] = 0xF4,
            ["PRINT"] = 0xF5,
            ["PLOT"] = 0xF6,
            ["RUN"] = 0xF7,
            ["SAVE"] = 0xF8,
            ["RANDOMIZE"] = 0xF9,
            ["IF"] = 0xFA,
            ["CLS"] = 0xFB,
            ["DRAW"] = 0xFC,
            ["CLEAR"] = 0xFD,
            ["RETURN"] = 0xFE,
            ["COPY"] = 0xFF
        };

        // ZX Spectrum Next Extensions
        // Standard Sinclair BASIC
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
                        string[] parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
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

            List<byte> result =
            [
                (byte)((lineNum >> 8) & 0xFF),
                (byte)(lineNum & 0xFF)
            ];

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
