// File   Program.cs
// Brief  Implementation file for the process that converts plain text into a +3DOS file.

using System.Text;
using System.Text.RegularExpressions;

namespace Txt2Bas
{
    /// <summary>
    /// Main entry point for the application.
    /// Handles command line arguments and orchestrates the conversion process.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// Reads the input text file, converts it to +3DOS tokenized BASIC, generates the header, and writes the output file.
        /// </summary>
        /// <param name="args">
        /// A string array containing command-line arguments:
        /// <list type="bullet">
        /// <item><description><b>args[0]</b>: The path to the input text file.</description></item>
        /// <item><description><b>args[1]</b>: The path to the output .bas file.</description></item>
        /// </list>
        /// </param>
        static void Main(string[] args)
        {
            // 1. Validate the command line arguments count.
            // 2. Check if the input file exists on disk.
            // 3. Instantiate the converter and process the text file into binary BASIC data.
            // 4. Create the +3DOS file header using the calculated data size and explicit auto-start line.
            // 5. Write the Header followed immediately by the BASIC data to the output file.
            // 6. Output success metrics to the console.

            if (args.Length < 2)
            {
                Console.WriteLine("Usage: txt2bas <input.txt> <output.bas>");
                return;
            }

            string inputFile = args[0];
            string outputFile = args[1];

            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Error: Input file '{inputFile}' not found.");
                return;
            }

            try
            {
                BasConverter converter = new BasConverter();
                byte[] basData = converter.ConvertFile(inputFile);

                // Use the explicitly parsed AutoStartLine (defaults to 32768 if no #autostart found)
                byte[] header = Plus3Dos.CreateHeader(basData.Length, converter.AutoStartLine);

                using (var fs = new FileStream(outputFile, FileMode.Create))
                {
                    fs.Write(header, 0, header.Length);
                    fs.Write(basData, 0, basData.Length);
                }

                Console.WriteLine($"Success! Created {outputFile}");

                Console.WriteLine(converter.AutoStartLine < 32768
                    ? $" - Auto-start Line: {converter.AutoStartLine}"
                    : " - Auto-start Line: None");

                Console.WriteLine($" - BASIC Size: {basData.Length} bytes");
                Console.WriteLine($" - Total File Size: {header.Length + basData.Length} bytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Generates the 128-byte +3DOS Header required for Spectrum Next/Plus3 files.
    /// </summary>
    public static class Plus3Dos
    {
        /// <summary>
        /// Creates a valid +3DOS header.
        /// </summary>
        /// <param name="basicLength">The length of the BASIC program data (excluding header).</param>
        /// <param name="autoStartLine">The line number to auto-run (or 32768 for none).</param>
        /// <returns>A 128-byte byte array containing the header.</returns>
        public static byte[] CreateHeader(int basicLength, int autoStartLine)
        {
            // 1. Allocate a zero-filled 128-byte buffer.
            // 2. Write the "PLUS3DOS" signature and Soft EOF (0x1A) to the start.
            // 3. Set the Issue (1) and Version (0) bytes.
            // 4. Calculate and write Total File Size (Header + BASIC Data) at offset 11.
            // 5. Write BASIC-specific metadata (Type, Length, Vars Offset).
            // 6. Set the Auto-start line (or 32768 if disabled).
            // 7. Calculate the Checksum (Sum of bytes 0-126 modulo 256) and write it at byte 127.

            byte[] header = new byte[128];
            Array.Clear(header, 0, 128);

            // Signature
            byte[] sig = Encoding.ASCII.GetBytes("PLUS3DOS");
            Array.Copy(sig, header, sig.Length);
            header[8] = 0x1A;

            // Version info
            header[9] = 0x01;
            header[10] = 0x00;

            // Total File Size (Header + Data)
            int totalFileSize = basicLength + 128; 
            BitConverter.GetBytes(totalFileSize).CopyTo(header, 11);

            // BASIC Header Info
            header[15] = 0x00; // Type: Program
            BitConverter.GetBytes((ushort)basicLength).CopyTo(header, 16); // Length

            // Auto-start
            if (autoStartLine is >= 0 and < 32768)
            {
                BitConverter.GetBytes((ushort)autoStartLine).CopyTo(header, 18);
            }
            else
            {
                BitConverter.GetBytes((ushort)32768).CopyTo(header, 18);
            }

            // Vars Offset
            BitConverter.GetBytes((ushort)basicLength).CopyTo(header, 20);

            // Checksum
            int sum = 0;
            for (int i = 0; i < 127; i++) sum += header[i];
            header[127] = (byte)(sum % 256);

            return header;
        }
    }

    /// <summary>
    /// Converts plain text into tokenized Spectrum BASIC binary format.
    /// </summary>
    public class BasConverter
    {
        private readonly TokenMap _tokenMap;
        private readonly List<string> _sortedKeys;
        
        /// <summary>
        /// The Auto-start line number. 
        /// Defaults to 32768 (No Auto-start). 
        /// Set only via the #autostart directive in the source file.
        /// </summary>
        public int AutoStartLine { get; private set; } = 32768; 

        /// <summary>
        /// Initializes member variable fields of the <see cref="BasConverter"/> class.
        /// </summary>
        public BasConverter()
        {
            // 1. Initialize the TokenMap dictionary.
            // 2. Create a list of keys sorted by Length Descending.
            //    (This ensures greedy matching: e.g., "DEFPROC" is matched before "DEF").
            
            _tokenMap = new TokenMap();
            _sortedKeys = _tokenMap.Map.Keys
                .OrderByDescending(k => k.Length)
                .ToList();
        }

        /// <summary>
        /// Reads a text file and converts it to a byte array of tokenized BASIC.
        /// </summary>
        /// <param name="path">Path to the input text file.</param>
        /// <returns>Byte array representing the BASIC program.</returns>
        public byte[] ConvertFile(string path)
        {
            // 1. Read all lines from the source text file.
            // 2. Initialize state variables (auto-line counter, output buffer).
            // 3. Iterate through each line:
            //    a. Skip whitespace-only lines (source code formatting).
            //    b. Process directive (#autostart) then skip the line.
            //    c. Skip all other lines starting with # (source code comments).
            //    d. Parse explicit or implicit line numbers and tokenize content.
            // 4. Return the aggregated binary data.

            string[] lines = File.ReadAllLines(path);
            var output = new List<byte>();

            int currentLineNum = 10;

            foreach (string line in lines)
            {
                string text = line.Trim();
                
                // 1. Skip Empty Lines completely (do not generate a BASIC line)
                if (string.IsNullOrWhiteSpace(text)) continue;

                // 2. Handle Lines starting with #
                if (text.StartsWith("#"))
                {
                    // Check for directives
                    if (text.StartsWith("#autostart", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = text.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1 && int.TryParse(parts[1], out int autoStartVal))
                        {
                            AutoStartLine = autoStartVal;
                        }
                    }
                    
                    // Whether it was a directive or a comment, skip it in the output.
                    continue;
                }

                // 3. Handle Standard Lines
                int lineNum = currentLineNum;
                string restOfLine = text;

                Match match = Regex.Match(text, @"^(\d+)\s+(.*)");
            
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

                byte[] lineBytes = ParseLine(lineNum, restOfLine);
                output.AddRange(lineBytes);
            }

            return output.ToArray();
        }

        /// <summary>
        /// Parses a single line of text into binary line format.
        /// Structure: [LineNum(BE)] [Length(LE)] [Data...] [0x0D]
        /// </summary>
        /// <param name="lineNum">The line number.</param>
        /// <param name="text">The text content of the line.</param>
        /// <returns>A byte array representing the binary line.</returns>
        private byte[] ParseLine(int lineNum, string text)
        {
            // 1. Iterate through the text character by character.
            // 2. Detect and process String Literals (preserve exactly).
            // 3. Detect and process Numbers (convert to ASCII + 5-byte hidden Sinclair format).
            // 4. Detect and process SPECIAL COMMENT (';' after colon or at start).
            // 5. Detect and process Keywords (Greedy Match against TokenMap):
            //    a. If REM found, consume the rest of the line as a comment.
            //    b. If other keyword found, strip immediately following whitespace.
            // 6. Fallback: Add character as literal ASCII.
            // 7. Append End-of-Line marker (0x0D).
            // 8. Prepend the Line Header (Line Number + Length) and return the byte array.

            List<byte> lineData = new List<byte>();
            
            for (int i = 0; i < text.Length; i++)
            {
                // String Literals
                if (text[i] == '"')
                {
                    int endQuote = text.IndexOf('"', i + 1);
                    if (endQuote == -1) endQuote = text.Length; 
                    
                    string literal = text.Substring(i, endQuote - i + 1);
                    lineData.AddRange(Encoding.ASCII.GetBytes(literal));
                    i = endQuote; 
                    continue;
                }

                // Numbers
                if (char.IsDigit(text[i]) || (text[i] == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
                {
                    string numStr = "";
                    int j = i;
                    while (j < text.Length && (char.IsDigit(text[j]) || text[j] == '.'))
                    {
                        numStr += text[j];
                        j++;
                    }

                    if (double.TryParse(numStr, out double val))
                    {
                        lineData.AddRange(Encoding.ASCII.GetBytes(numStr));
                        lineData.Add(0x0E); // Hidden Marker
                        lineData.AddRange(SinclairNumber.Pack(val));
                        i = j - 1; 
                        continue;
                    }
                }

                // COMMENT HANDLING: Strict check for ';comment' idiom
                // Trigger: Semicolon at start of line OR Semicolon immediately preceded by Colon
                if (text[i] == ';')
                {
                    bool isComment = false;
                    
                    // Look backwards skipping whitespace to find the context
                    int back = i - 1;
                    while (back >= 0 && text[back] == ' ') back--;
                    
                    if (back < 0) isComment = true; // Start of line
                    else if (text[back] == ':') isComment = true; // Preceded by colon

                    if (isComment)
                    {
                        // Consume the rest of the line as literal text (do not tokenize)
                        string comment = text.Substring(i);
                        lineData.AddRange(Encoding.ASCII.GetBytes(comment));
                        i = text.Length; 
                        continue;
                    }
                }

                // Keywords
                bool matched = false;
                foreach (string k in _sortedKeys)
                {
                    if (i + k.Length > text.Length) continue;

                    if (string.Compare(text.Substring(i, k.Length), k, StringComparison.OrdinalIgnoreCase) != 0) 
                        continue;
                    
                    bool isAlphaToken = char.IsLetter(k[0]);
                    bool prevCharValid = (i == 0) || !char.IsLetter(text[i - 1]);
                    bool nextCharValid = (i + k.Length >= text.Length) || !char.IsLetterOrDigit(text[i + k.Length]);

                    if (isAlphaToken && (!prevCharValid || !nextCharValid)) continue;
                    
                    byte token = _tokenMap.Map[k];
                    lineData.Add(token);
                    i += k.Length; 
                    matched = true;

                    // REM handling
                    if (token == 0xEA)
                    {
                        if (i < text.Length)
                        {
                            string comment = text.Substring(i);
                            lineData.AddRange(Encoding.ASCII.GetBytes(comment));
                            i = text.Length; 
                        }
                    }
                    else
                    {
                        // Strip trailing space
                        while (i < text.Length && text[i] == ' ') i++;
                    }

                    i--; 
                    break;
                }

                if (matched) continue;

                // Literal
                lineData.Add((byte)text[i]);
            }

            lineData.Add(0x0D);

            // Construct Line Header
            List<byte> finalLine =
            [
                (byte)((lineNum >> 8) & 0xFF),
                (byte)(lineNum & 0xFF)
            ];

            int length = lineData.Count;
            finalLine.Add((byte)(length & 0xFF));
            finalLine.Add((byte)((length >> 8) & 0xFF));

            finalLine.AddRange(lineData);

            return finalLine.ToArray();
        }
    }

    /// <summary>
    /// Handles the 5-byte floating point format used by Sinclair BASIC.
    /// </summary>
    public static class SinclairNumber
    {
        /// <summary>
        /// Packs a double into the 5-byte internal format.
        /// Currently, supports integer format optimization (00 Sign LSB MSB 00).
        /// </summary>
        /// <param name="number">The number to pack.</param>
        /// <returns>A 5-byte array representing the Sinclair number format.</returns>
        public static byte[] Pack(double number)
        {
            // 1. Check if the number is a small integer (within +/- 65535).
            // 2. If yes, create the integer format: 0x00, SignByte, LSB, MSB, 0x00.
            // 3. If no, return empty zero bytes (full FP implementation omitted for brevity).
            
            if (number % 1 == 0 && number is >= -65535 and <= 65535)
            {
                int val = (int)number;
                byte sign = 0;

                if (val < 0)
                {
                    sign = 0xFF; 
                    val = -val;
                }
                
                return [0x00, sign, (byte)(val & 0xFF), (byte)((val >> 8) & 0xFF), 0x00];
            }
            
            return "\0\0\0\0\0"u8.ToArray();
        }
    }

    /// <summary>
    /// Dictionary mapping String Keywords to Byte Tokens.
    /// Includes Standard Spectrum and NextBASIC extensions.
    /// </summary>
    public class TokenMap
    {
        /// <summary>
        /// A dictionary containing the mapping from Keyword string to Byte token.
        /// </summary>
        public readonly Dictionary<string, byte> Map = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes the TokenMap with standard and extended Spectrum keywords.
        /// </summary>
        public TokenMap()
        {
            // 1. Populate the dictionary with NextBASIC extension tokens (0x80 - 0xA2).
            // 2. Populate the dictionary with Standard 48K BASIC tokens (0xA3 - 0xFF).

            // === ZX SPECTRUM NEXT EXTENSIONS (0x80 - 0xA2) ===
            Map["PEEK$"] = 0x87; Map["REG"] = 0x88; Map["DPOKE"] = 0x89; Map["DPEEK"] = 0x8A;
            Map["MOD"] = 0x8B; Map["<<"] = 0x8C; Map[">>"] = 0x8D; Map["UNTIL"] = 0x8E;
            Map["ERROR"] = 0x8F; Map["ON"] = 0x90; Map["DEFPROC"] = 0x91; Map["ENDPROC"] = 0x92;
            Map["PROC"] = 0x93; Map["LOCAL"] = 0x94; Map["DRIVER"] = 0x95; Map["WHILE"] = 0x96;
            Map["REPEAT"] = 0x97; Map["ELSE"] = 0x98; Map["REMOUNT"] = 0x99; Map["BANK"] = 0x9A;
            Map["TILE"] = 0x9B; Map["LAYER"] = 0x9C; Map["PALETTE"] = 0x9D; Map["SPRITE"] = 0x9E;
            Map["PWD"] = 0x9F; Map["CD"] = 0xA0; Map["MKDIR"] = 0xA1; Map["RMDIR"] = 0xA2;

            // === STANDARD ZX SPECTRUM 48K TOKENS (0xA3 - 0xFF) ===
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
}
