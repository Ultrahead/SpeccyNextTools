// File  Program.cs
// Brief Implementation file for the process that converts a +3DOS file into plain text.

using System.Text;

namespace Bas2Txt
{
    /// <summary>
    /// Main entry point for the application.
    /// Converts a binary BASIC file (.bas) to a UTF-8 Text file.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// Reads the binary BASIC file +3DOS format, parses the structure and tokens, and writes the decoded text to the output file.
        /// </summary>
        /// <param name="args">
        /// A string array containing command-line arguments:
        /// <list type="bullet">
        /// <item><description><b>args[0]</b>: The path to the input .bas file.</description></item>
        /// <item><description><b>args[1]</b>: The path to the output text file.</description></item>
        /// </list>
        /// </param>
        private static void Main(string[] args)
        {
            // 1. Validate the command line arguments count.
            // 2. Verify that the input file exists.
            // 3. Read the binary file content into a byte array.
            // 4. Instantiate the parser to decode binary data into text.
            // 5. Write the resulting string to the output file with UTF-8 encoding.
            // 6. Output success message.
            
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: bas2txt <input.bas> <output.txt>");
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
                byte[] fileBytes = File.ReadAllBytes(inputFile);
                
                BasParser parser = new BasParser();
                string decodedText = parser.Parse(fileBytes);

                File.WriteAllText(outputFile, decodedText, Encoding.UTF8);

                Console.WriteLine($"Success! Decoded {inputFile} to {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Parses binary Spectrum BASIC data into a human-readable string.
    /// Handles +3DOS headers, Token decoding, and hidden numeric formats.
    /// </summary>
    public class BasParser
    {
        private readonly Dictionary<byte, string> _reverseTokenMap = new ReverseTokenMap().Map;

        /// <summary>
        /// Parses a byte array containing a complete BASIC file.
        /// </summary>
        /// <param name="data">The raw file bytes.</param>
        /// <returns>A string representing the decoded BASIC code.</returns>
        public string Parse(byte[] data)
        {
            // 1. Check for the presence of a +3DOS header (128 bytes, starts with PLUS3DOS).
            // 2. If header exists:
            //    a. Read the Auto-start line (bytes 18-19).
            //    b. If Auto-start is active, append "#autostart" directive.
            //    c. Advance the offset to 128 (skip header).
            // 3. Loop through the file data while offset < length:
            //    a. Read Line Number (Big Endian).
            //    b. Read Line Length (Little Endian).
            //    c. Decode the line data payload.
            //    d. Format as "LineNum Code".
            // 4. Return the full text.
            
            StringBuilder sb = new StringBuilder();
            int offset = 0;

            // 1. Detect and Process +3DOS Header (128 bytes)
            if (data.Length >= 128)
            {
                string signature = Encoding.ASCII.GetString(data, 0, 8);
                
                // Standard header usually starts with PLUS3DOS or ZXPLUS3
                if (signature == "PLUS3DOS" || signature.StartsWith("ZXPLUS3"))
                {
                    // Check for Auto-start line (Bytes 18-19, Little Endian)
                    int autoStart = data[18] | (data[19] << 8);

                    // 32768 (0x8000) means "No Auto-start". Anything else is a line number.
                    if (autoStart != 32768)
                    {
                        sb.AppendLine($"#autostart {autoStart}");
                    }

                    // Move past header to start of BASIC data
                    offset = 128;
                }
            }

            // 2. Parse Lines
            // Line Format: [LineNum (2B Big Endian)] [Length (2B Little Endian)] [Data...] [0x0D]
            while (offset < data.Length)
            {
                // Need at least 4 bytes for line header
                if (offset + 4 > data.Length) break;

                // Line Number (Big Endian)
                int lineNum = (data[offset] << 8) | data[offset + 1];
                offset += 2;

                // Line Length (Little Endian)
                int lineLen = data[offset] | (data[offset + 1] << 8);
                offset += 2;

                // Safety check for corrupt files
                if (offset + lineLen - 1 > data.Length) break;

                // Process Line Data (Exclude the final 0x0D which is included in lineLen)
                string lineContent = DecodeLineData(data, offset, lineLen - 1);
                
                sb.AppendLine($"{lineNum} {lineContent}");

                offset += lineLen;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Decodes the raw data payload of a single BASIC line.
        /// </summary>
        private string DecodeLineData(byte[] data, int start, int length)
        {
            // 1. Iterate through the byte range for this line.
            // 2. Check for Hidden Number Markers (0x0E):
            //    a. Skip the next 5 bytes (binary format) as we already have the ASCII representation.
            // 3. Check for Tokens (Keywords):
            //    a. Map byte to keyword string.
            //    b. Apply Smart Spacing: If next byte is alphanumeric or quote, insert a space.
            // 4. Check for Printable ASCII (32-126): Append char.
            // 5. Check for Copyright symbol (0x7F).
            
            var sb = new StringBuilder();
            int end = start + length;

            for (int i = start; i < end; i++)
            {
                byte b = data[i];

                // Case 1: Hidden Number Marker (0x0E)
                // Spectrum BASIC stores numbers as ASCII followed by 0x0E and 5 binary bytes.
                // We skip the binary bytes as we only need the ASCII representation for text output.
                if (b == 0x0E)
                {
                    i += 5; 
                    continue;
                }

                // Case 2: Token (Keywords)
                if (_reverseTokenMap.TryGetValue(b, out var value))
                {
                    sb.Append(value);

                    // SMART SPACING LOGIC:
                    // If the NEXT byte is a printable character (letter, digit, quote),
                    // insert a space to restore readability (e.g., CD"path" -> CD "path").
                    if (i + 1 < end)
                    {
                        byte next = data[i + 1];
                        // If next is not a token (byte < 128) and not a hidden number marker
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
                else switch (b)
                {
                    // Case 3: Printable ASCII
                    case >= 32 and <= 126:
                        sb.Append((char)b);
                        break;
                    // Case 4: Copyright Symbol
                    case 0x7F:
                        sb.Append('©');
                        break;
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Dictionary mapping Byte Tokens to String Keywords.
    /// </summary>
    public class ReverseTokenMap
    {
        // 1. Populate dictionary with NextBASIC extensions.
        // 2. Populate dictionary with Standard 48K BASIC tokens.
        
        /// <summary>
        /// A dictionary containing the mapping from Byte token to Keyword string.
        /// </summary>
        public readonly Dictionary<byte, string> Map = new Dictionary<byte, string>();

        public ReverseTokenMap()
        {
            // === ZX SPECTRUM NEXT EXTENSIONS (0x80 - 0xA2) ===
            Map[0x87] = "PEEK$"; Map[0x88] = "REG"; Map[0x89] = "DPOKE"; Map[0x8A] = "DPEEK";
            Map[0x8B] = "MOD"; Map[0x8C] = "<<"; Map[0x8D] = ">>"; Map[0x8E] = "UNTIL";
            Map[0x8F] = "ERROR"; Map[0x90] = "ON"; Map[0x91] = "DEFPROC"; Map[0x92] = "ENDPROC";
            Map[0x93] = "PROC"; Map[0x94] = "LOCAL"; Map[0x95] = "DRIVER"; Map[0x96] = "WHILE";
            Map[0x97] = "REPEAT"; Map[0x98] = "ELSE"; Map[0x99] = "REMOUNT"; Map[0x9A] = "BANK";
            Map[0x9B] = "TILE"; Map[0x9C] = "LAYER"; Map[0x9D] = "PALETTE"; Map[0x9E] = "SPRITE";
            Map[0x9F] = "PWD"; Map[0xA0] = "CD"; Map[0xA1] = "MKDIR"; Map[0xA2] = "RMDIR";

            // === STANDARD ZX SPECTRUM 48K TOKENS (0xA3 - 0xFF) ===
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
}