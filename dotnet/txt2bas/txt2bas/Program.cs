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
            
            bool isNegative = number < 0.0;
            if (isNegative) number = -number;

            if (number == 0.0) return "\0\0\0\0\0"u8.ToArray();

            byte exponent = 0x80;
            while (number < 0.5)
            {
                number *= 2.0;
                exponent--;
            }

            while (number >= 1.0)
            {
                number *= 0.5;
                exponent++;
            }

            number *= 4294967296.0; // 0x100000000
            number += 0.5; // rounding step

            uint mantissa = (uint)number;

            byte m1 = (byte)((mantissa >> 24) & 0xFF);
            byte m2 = (byte)((mantissa >> 16) & 0xFF);
            byte m3 = (byte)((mantissa >> 8) & 0xFF);
            byte m4 = (byte)(mantissa & 0xFF);

            if (!isNegative) m1 &= 0x7F;

            return [exponent, m1, m2, m3, m4];
        }
    }

    internal class TokenMap
    {
        public Dictionary<string, byte> Map { get; private set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            // ZX Spectrum Next Extensions
            ["TIME"] = 0x81, ["PRIVATE"] = 0x82, ["ENDIF"] = 0x84, ["EXIT"] = 0x85,
            ["REF"] = 0x86, 
            ["PEEK$"] = 0x87, ["REG"] = 0x88, ["DPOKE"] = 0x89, ["DPEEK"] = 0x8A,
            ["MOD"] = 0x8B, ["<<"] = 0x8C, [">>"] = 0x8D, ["UNTIL"] = 0x8E,
            ["ERROR"] = 0x8F, ["ON"] = 0x90, ["DEFPROC"] = 0x91, ["ENDPROC"] = 0x92,
            ["PROC"] = 0x93, ["LOCAL"] = 0x94, ["DRIVER"] = 0x95, ["WHILE"] = 0x96,
            ["REPEAT"] = 0x97, ["ELSE"] = 0x98, ["REMOUNT"] = 0x99, ["BANK"] = 0x9A,
            ["TILE"] = 0x9B, ["LAYER"] = 0x9C, ["PALETTE"] = 0x9D, ["SPRITE"] = 0x9E,
            ["PWD"] = 0x9F, ["CD"] = 0xA0, ["MKDIR"] = 0xA1, ["RMDIR"] = 0xA2,

            // Aliases missing in the original logic but present in op-table
            ["ELSE IF"] = 0x83, ["CONT"] = 0xE8, ["RAND"] = 0xF9, 

            // Standard Sinclair BASIC
            ["SPECTRUM"] = 0xA3, ["PLAY"] = 0xA4, ["RND"] = 0xA5, ["INKEY$"] = 0xA6,
            ["PI"] = 0xA7, ["FN"] = 0xA8, ["POINT"] = 0xA9, ["SCREEN$"] = 0xAA,
            ["ATTR"] = 0xAB, ["AT"] = 0xAC, ["TAB"] = 0xAD, ["VAL$"] = 0xAE,
            ["CODE"] = 0xAF, ["VAL"] = 0xB0, ["LEN"] = 0xB1, ["SIN"] = 0xB2,
            ["COS"] = 0xB3, ["TAN"] = 0xB4, ["ASN"] = 0xB5, ["ACS"] = 0xB6,
            ["ATN"] = 0xB7, ["LN"] = 0xB8, ["EXP"] = 0xB9, ["INT"] = 0xBA,
            ["SQR"] = 0xBB, ["SGN"] = 0xBC, ["ABS"] = 0xBD, ["PEEK"] = 0xBE,
            ["IN"] = 0xBF, ["USR"] = 0xC0, ["STR$"] = 0xC1, ["CHR$"] = 0xC2,
            ["NOT"] = 0xC3, ["BIN"] = 0xC4, ["OR"] = 0xC5, ["AND"] = 0xC6,
            ["<="] = 0xC7, [">="] = 0xC8, ["<>"] = 0xC9, ["LINE"] = 0xCA,
            ["THEN"] = 0xCB, ["TO"] = 0xCC, ["STEP"] = 0xCD, ["DEF FN"] = 0xCE,
            ["CAT"] = 0xCF, ["FORMAT"] = 0xD0, ["MOVE"] = 0xD1, ["ERASE"] = 0xD2,
            ["OPEN #"] = 0xD3, ["CLOSE #"] = 0xD4, ["MERGE"] = 0xD5, ["VERIFY"] = 0xD6,
            ["BEEP"] = 0xD7, ["CIRCLE"] = 0xD8, ["INK"] = 0xD9, ["PAPER"] = 0xDA,
            ["FLASH"] = 0xDB, ["BRIGHT"] = 0xDC, ["INVERSE"] = 0xDD, ["OVER"] = 0xDE,
            ["OUT"] = 0xDF, ["LPRINT"] = 0xE0, ["LLIST"] = 0xE1, ["STOP"] = 0xE2,
            ["READ"] = 0xE3, ["DATA"] = 0xE4, ["RESTORE"] = 0xE5, ["NEW"] = 0xE6,
            ["BORDER"] = 0xE7, ["CONTINUE"] = 0xE8, ["DIM"] = 0xE9, ["REM"] = 0xEA,
            ["FOR"] = 0xEB, ["GO TO"] = 0xEC, ["GOTO"] = 0xEC, ["GO SUB"] = 0xED,
            ["GOSUB"] = 0xED, ["INPUT"] = 0xEE, ["LOAD"] = 0xEF, ["LIST"] = 0xF0,
            ["LET"] = 0xF1, ["PAUSE"] = 0xF2, ["NEXT"] = 0xF3, ["POKE"] = 0xF4,
            ["PRINT"] = 0xF5, ["PLOT"] = 0xF6, ["RUN"] = 0xF7, ["SAVE"] = 0xF8,
            ["RANDOMIZE"] = 0xF9, ["IF"] = 0xFA, ["CLS"] = 0xFB, ["DRAW"] = 0xFC,
            ["CLEAR"] = 0xFD, ["RETURN"] = 0xFE, ["COPY"] = 0xFF
        };
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

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || 
                   (c >= 'A' && c <= 'F') || 
                   (c >= 'a' && c <= 'f');
        }

        public byte[] ConvertFile(string path)
        {
            List<byte> output = new List<byte>();
            string text = File.ReadAllText(path);
            
            // Replicates the C++ logic to handle classic Mac \r files and standard Unix/Windows properly
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int currentLineNum = 10;
            Regex lineRegex = new Regex(@"^\s*(\d{1,4})\s?(.*)");

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("#"))
                {
                    string lowerLine = line.ToLowerInvariant();
                    if (lowerLine.StartsWith("#autostart"))
                    {
                        string[] parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
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
            
            List<string> inStack = new List<string>();
            
            void PopTo(string type)
            {
                while (inStack.Count > 0)
                {
                    string last = inStack[inStack.Count - 1];
                    inStack.RemoveAt(inStack.Count - 1);
                    if (last == type) break;
                }
            }

            bool IsIn(string type)
            {
                return inStack.Contains(type);
            }

            bool inIntExpression = false;
            bool inIf = false;
            bool inUntil = false;
            int intParensDepth = 0;
            bool intSubStatement = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '=' || text[i] == ',' || text[i] == ';' || text[i] == ':')
                {
                    if (intParensDepth == 0 && !inIf && !inUntil)
                    {
                        if (!intSubStatement) inIntExpression = false;
                    }
                }
                
                if (text[i] == ':')
                {
                    inIf = false;
                    inUntil = false;
                    intParensDepth = 0;
                    inIntExpression = false;
                    intSubStatement = false;
                }

                if (text[i] == '%')
                {
                    inIntExpression = true;
                    bool startOfIntStatement = false;
                    
                    if (lineData.Count == 0)
                    {
                        startOfIntStatement = true;
                    }
                    else
                    {
                        for (int idx = lineData.Count - 1; idx >= 0; idx--)
                        {
                            byte b = lineData[idx];
                            if (b == ' ' || b == '\t') continue;
                            if (b == ':' || b == 0x8F || b == '=' ||
                                b == 0xFA || b == 0x83 || b == 0x98 || b == 0x8E ||
                                b == 0xCE)
                            {
                                startOfIntStatement = true;
                            }
                            break;
                        }
                    }
                    
                    if (startOfIntStatement) intSubStatement = true;
                    
                    lineData.Add((byte)'%');
                    expectCommand = false;
                    if (i + 1 < text.Length && text[i + 1] == ' ') i++;
                    continue;
                }

                if (expectCommand && text[i] == '.')
                {
                    if (i + 1 < text.Length && char.IsDigit(text[i + 1]))
                    {
                        // Fall through
                    }
                    else
                    {
                        int pos = i;
                        while (pos < text.Length)
                        {
                            char ch = text[pos];
                            if (ch == '"')
                            {
                                int endQuote = text.IndexOf('"', pos + 1);
                                if (endQuote != -1) pos = endQuote + 1;
                                else pos = text.Length;
                            }
                            else if (ch == ':' || ch == '\n')
                            {
                                break;
                            }
                            else
                            {
                                pos++;
                            }
                        }
                        string dotCmd = text.Substring(i, pos - i);
                        lineData.AddRange(Encoding.ASCII.GetBytes(dotCmd));
                        i = pos - 1;
                        expectCommand = false;
                        if (i + 1 < text.Length && text[i + 1] == ' ') i++;
                        continue;
                    }
                }

                if (text[i] == '"')
                {
                    expectCommand = false;
                    int endQuote = text.IndexOf('"', i + 1);
                    if (endQuote == -1)
                    {
                        lineData.AddRange(Encoding.ASCII.GetBytes(text.Substring(i)));
                        i = text.Length;
                        PopTo("STRING_EXPRESSION");
                        inStack.Add("STRING_EXPRESSION");
                        break;
                    }
                    else
                    {
                        lineData.AddRange(Encoding.ASCII.GetBytes(text.Substring(i, endQuote - i + 1)));
                        i = endQuote;
                        PopTo("STRING_EXPRESSION");
                        inStack.Add("STRING_EXPRESSION");
                        if (i + 1 < text.Length && text[i + 1] == ' ') i++;
                    }
                    continue;
                }

                if (text[i] == ';')
                {
                    bool isComment = true;
                    for (int idx = lineData.Count - 1; idx >= 0; idx--)
                    {
                        byte b = lineData[idx];
                        if (b == ' ' || b == '\t') continue;
                        if (b == ':' || b == 0x8F || b == 0xCB || b == 0x98)
                        {
                            isComment = true;
                            break;
                        }
                        isComment = false;
                        break;
                    }

                    if (isComment || lineData.Count == 0)
                    {
                        lineData.AddRange(Encoding.ASCII.GetBytes(text.Substring(i)));
                        break;
                    }
                }

                bool matched = false;
                foreach (string k in _sortedKeys)
                {
                    if (i + k.Length > text.Length) continue;

                    string sub = text.Substring(i, k.Length);
                    if (!sub.Equals(k, StringComparison.OrdinalIgnoreCase)) continue;

                    bool isAlphaStart = char.IsLetter(k[0]);
                    bool isAlphaEnd = char.IsLetter(k[k.Length - 1]);

                    bool validBoundary = true;
                    if (isAlphaStart)
                    {
                        int back = i - 1;
                        if (back >= 0)
                        {
                            char p = text[back];
                            if (char.IsLetterOrDigit(p) || p == '_') validBoundary = false;
                        }
                    }
                    if (validBoundary && isAlphaEnd)
                    {
                        int next = i + k.Length;
                        if (next < text.Length)
                        {
                            char n = text[next];
                            if (char.IsLetterOrDigit(n) || n == '_') validBoundary = false;
                        }
                    }

                    if (!validBoundary) continue;

                    byte token = _tokenMap.Map[k];

                    if (token == 0xCE)
                    {
                        inStack.Add("DEFFN");
                        inStack.Add("DEFFN_SIG");
                    }

                    if (token == 0xFA)
                    {
                        bool hasThen = Regex.IsMatch(text.Substring(i), @"\bTHEN\b", RegexOptions.IgnoreCase);
                        if (!hasThen) token = 0x83;
                    }

                    if (token == 0xFA || token == 0x83) inIf = true;

                    if (token == 0xCB)
                    {
                        inIf = false;
                        inIntExpression = false;
                        intSubStatement = false;
                    }
                    if (token == 0x8E) inUntil = true;
                    if (token == 0x84)
                    {
                        inIf = false;
                        inIntExpression = false;
                        intSubStatement = false;
                    }

                    bool isOperator = false;
                    if (k == "AND" || k == "OR" || k == "NOT" || k == "MOD" || k == "-" || k == "+" ||
                        k == "*" || k == "/" || k == "<" || k == ">" || k == "<=" || k == ">=" || k == "<>" ||
                        k == "<<" || k == ">>" || k == "&" || k == "|" || k == "^" || k == "!")
                    {
                        isOperator = true;
                    }
                    bool isIntFunc = false;
                    if (k == "IN" || k == "REG" || k == "PEEK" || k == "DPEEK" || k == "USR" ||
                        k == "BIN" || k == "RND" || k == "BANK" || k == "SPRITE" || k == "INT" ||
                        k == "ABS" || k == "SGN" || k == "CODE")
                    {
                        isIntFunc = true;
                    }

                    if (inIntExpression && intParensDepth > 0)
                    {
                    }
                    else if (intSubStatement)
                    {
                    }
                    else if (!isOperator && !isIntFunc)
                    {
                        inIntExpression = false;
                        intSubStatement = false;
                    }

                    if (token == 0xEA) // REM
                    {
                        lineData.Add(token);
                        int r = i + k.Length;
                        if (r < text.Length && text[r] == ' ') r++;
                        if (r < text.Length)
                        {
                            lineData.AddRange(Encoding.ASCII.GetBytes(text.Substring(r)));
                        }
                        i = text.Length;
                        matched = true;
                        break;
                    }
                    else if (token == 0x82) // PRIVATE
                    {
                        lineData.Add(token);
                        int j = i + k.Length;
                        while (j < text.Length && (text[j] == ' ' || text[j] == '\t')) j++;
                        bool hasClear = false;
                        if (j + 5 <= text.Length && text.Substring(j, 5).Equals("CLEAR", StringComparison.OrdinalIgnoreCase))
                        {
                            hasClear = true;
                        }
                        if (!hasClear)
                        {
                            lineData.Add(0x0E);
                            for (int z = 0; z < 5; z++) lineData.Add(0x00);
                        }
                        i += k.Length - 1;
                        if (i + 1 < text.Length && text[i + 1] == ' ') i++;
                        matched = true;
                        break;
                    }
                    else if (token == 0xC4) // BIN
                    {
                        lineData.Add(token);
                        int j = i + k.Length;
                        while (j < text.Length && (text[j] == ' ' || text[j] == '\t')) j++;
                        string binStr = "";
                        while (j < text.Length && (text[j] == '0' || text[j] == '1'))
                        {
                            binStr += text[j++];
                        }
                        if (binStr.Length > 0)
                        {
                            lineData.AddRange(Encoding.ASCII.GetBytes(binStr));
                            if (!inIntExpression)
                            {
                                lineData.Add(0x0E);
                                try
                                {
                                    uint binVal = Convert.ToUInt32(binStr, 2);
                                    lineData.AddRange(SinclairNumber.Pack(binVal));
                                }
                                catch
                                {
                                    lineData.AddRange(new byte[] { 0, 0, 0, 0, 0 });
                                }
                            }
                            i = j - 1;
                        }
                        else
                        {
                            i += k.Length - 1;
                        }
                        expectCommand = false;
                        if (i + 1 < text.Length && text[i + 1] == ' ') i++;
                        matched = true;
                        break;
                    }
                    else
                    {
                        lineData.Add(token);
                        if (token == 0xCB || token == 0x98) expectCommand = true;
                        else expectCommand = false;
                        i += k.Length - 1;
                        if (i + 1 < text.Length && text[i + 1] == ' ') i++;
                        matched = true;
                        break;
                    }
                }

                if (matched) continue;

                if (char.IsLetter(text[i]))
                {
                    int j = i;
                    while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] == '_' || text[j] == '$'))
                    {
                        j++;
                    }
                    string ident = text.Substring(i, j - i);
                    lineData.AddRange(Encoding.ASCII.GetBytes(ident));

                    if (IsIn("STRING_EXPRESSION"))
                    {
                        PopTo("STRING_EXPRESSION");
                    }
                    if (ident.Length > 0 && ident.EndsWith("$"))
                    {
                        inStack.Add("STRING_EXPRESSION");
                    }
                    if (IsIn("DEFFN_ARGS"))
                    {
                        if (ident.Length == 0 || !ident.EndsWith("$"))
                        {
                            lineData.Add(0x0E);
                            for (int z = 0; z < 5; z++) lineData.Add(0x00);
                        }
                    }

                    i = j - 1;
                    expectCommand = false;
                    if (i + 1 < text.Length && text[i + 1] == ' ') i++;
                    continue;
                }

                if (text[i] == '$')
                {
                    int j = i + 1;
                    string hexStr = "";
                    while (j < text.Length && (IsHexDigit(text[j]) || text[j] == '.'))
                    {
                        hexStr += text[j++];
                    }
                    if (hexStr.Length > 0)
                    {
                        lineData.AddRange(Encoding.ASCII.GetBytes(text.Substring(i, j - i)));
                        if (!inIntExpression)
                        {
                            lineData.Add(0x0E);
                            try
                            {
                                double val = 0;
                                int dotPos = hexStr.IndexOf('.');
                                if (dotPos != -1)
                                {
                                    string whole = hexStr.Substring(0, dotPos);
                                    string frac = hexStr.Substring(dotPos + 1);
                                    val = whole.Length > 0 ? Convert.ToUInt32(whole, 16) : 0;
                                    if (frac.Length > 0)
                                    {
                                        val += Convert.ToUInt32(frac, 16) / Math.Pow(16.0, frac.Length);
                                    }
                                }
                                else
                                {
                                    val = Convert.ToUInt32(hexStr, 16);
                                }
                                lineData.AddRange(SinclairNumber.Pack(val));
                            }
                            catch
                            {
                                lineData.AddRange(new byte[] { 0, 0, 0, 0, 0 });
                            }
                        }
                        i = j - 1;
                        expectCommand = false;
                        if (i + 1 < text.Length && text[i + 1] == ' ') i++;
                        continue;
                    }
                }

                if (text[i] == '@')
                {
                    int j = i + 1;
                    string binStr = "";
                    while (j < text.Length && (text[j] == '0' || text[j] == '1' || text[j] == '.'))
                    {
                        binStr += text[j++];
                    }
                    if (binStr.Length > 0)
                    {
                        lineData.AddRange(Encoding.ASCII.GetBytes(text.Substring(i, j - i)));
                        if (!inIntExpression)
                        {
                            lineData.Add(0x0E);
                            try
                            {
                                double val = 0;
                                int dotPos = binStr.IndexOf('.');
                                if (dotPos != -1)
                                {
                                    string whole = binStr.Substring(0, dotPos);
                                    string frac = binStr.Substring(dotPos + 1);
                                    val = whole.Length > 0 ? Convert.ToUInt32(whole, 2) : 0;
                                    if (frac.Length > 0)
                                    {
                                        val += Convert.ToUInt32(frac, 2) / Math.Pow(2.0, frac.Length);
                                    }
                                }
                                else
                                {
                                    val = Convert.ToUInt32(binStr, 2);
                                }
                                lineData.AddRange(SinclairNumber.Pack(val));
                            }
                            catch
                            {
                                lineData.AddRange(new byte[] { 0, 0, 0, 0, 0 });
                            }
                        }
                        i = j - 1;
                        expectCommand = false;
                        if (i + 1 < text.Length && text[i + 1] == ' ') i++;
                        continue;
                    }
                }

                if (char.IsDigit(text[i]) || (text[i] == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
                {
                    expectCommand = false;
                    bool skipMarker = inIntExpression;
                    string numStr = "";
                    int j = i;
                    while (j < text.Length && (char.IsDigit(text[j]) || text[j] == '.' || text[j] == 'E' || text[j] == 'e'))
                    {
                        if (text[j] == 'E' || text[j] == 'e')
                        {
                            numStr += text[j++];
                            if (j < text.Length && (text[j] == '+' || text[j] == '-')) numStr += text[j++];
                        }
                        else
                        {
                            numStr += text[j++];
                        }
                    }

                    if (!skipMarker)
                    {
                        double val = 0;
                        double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out val);
                        lineData.AddRange(Encoding.ASCII.GetBytes(numStr));
                        lineData.Add(0x0E);
                        lineData.AddRange(SinclairNumber.Pack(val));
                    }
                    else
                    {
                        lineData.AddRange(Encoding.ASCII.GetBytes(numStr));
                    }
                    
                    i = j - 1;
                    if (i + 1 < text.Length && text[i + 1] == ' ') i++;
                    continue;
                }

                if (text[i] == ' ' || text[i] == '\t')
                {
                    lineData.Add((byte)text[i]);
                    continue;
                }

                byte c = (byte)text[i];
                lineData.Add(c);

                if (c == '=')
                {
                    if (!inIf && !inUntil)
                    {
                        inIntExpression = false;
                        intSubStatement = false;
                    }
                }
                else if (c == ',' || c == ';')
                {
                    if (intParensDepth == 0 && !inIf && !inUntil)
                    {
                        inIntExpression = false;
                        intSubStatement = false;
                    }
                }

                if (c == ':')
                {
                    expectCommand = true;
                    inStack.Clear();
                    inIf = false;
                    inUntil = false;
                    intParensDepth = 0;
                    inIntExpression = false;
                    intSubStatement = false;
                }
                else
                {
                    expectCommand = false;
                }

                if (c == '(')
                {
                    if (inIntExpression) intParensDepth++;
                    inStack.Add("OPEN_PARENS");
                    if (IsIn("DEFFN_SIG")) inStack.Add("DEFFN_ARGS");
                }
                else if (c == ')')
                {
                    if (intParensDepth > 0) intParensDepth--;
                    PopTo("OPEN_PARENS");
                }
                else if (c == '=')
                {
                    if (inStack.Count > 0 && inStack[inStack.Count - 1] == "DEFFN_SIG")
                    {
                        PopTo("DEFFN_SIG");
                    }
                }

                if (i + 1 < text.Length && text[i + 1] == ' ') i++;
            }

            lineData.Add(0x0D);

            List<byte> result = new List<byte>
            {
                (byte)((lineNum >> 8) & 0xFF),
                (byte)(lineNum & 0xFF)
            };

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
            Console.WriteLine("  _______     _   ___  ___         ");
            Console.WriteLine(" |__   __|   | | |__ \\|  _ \\       ");
            Console.WriteLine("    | |___  _| |_   ) | |_) | __ _ ___");
            Console.WriteLine("    | / \\ \\/ / __| / /|  _ < / _` / __|");
            Console.WriteLine("    | |  >  <| |_ / /_| |_) | (_| \\__ \\");
            Console.WriteLine("    |_| /_/\\_\\\\__|____|____/ \\__,_|___/\n");
            
            Console.WriteLine($"ZX Spectrum Text-to-BASIC Converter v{ToolVersion}\n");
            Console.WriteLine("Usage: txt2bas [options] <input.txt> <output.bas>\n");
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --help     Show this help message and exit");
            Console.WriteLine("  -v, --version  Show version information and exit\n");
            Console.WriteLine("Example:");
            Console.WriteLine("  txt2bas game.txt game.bas");
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
                Console.WriteLine("Try 'txt2bas --help' for more information.");
                return 0;
            }

            string inputFile = args[0];
            string outputFile = args[1];

            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Error: Input file '{inputFile}' not found.");
                return 0;
            }

            try
            {
                BasConverter conv = new BasConverter();
                byte[] bytes = conv.ConvertFile(inputFile);
                byte[] head = Plus3Dos.CreateHeader(bytes.Length, conv.AutoStartLine);

                using (FileStream fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(head, 0, head.Length);
                    fs.Write(bytes, 0, bytes.Length);
                }

                Console.WriteLine($"Success! Created {outputFile}");
                if (conv.AutoStartLine < 32768)
                {
                    Console.WriteLine($" - Auto-start Line: {conv.AutoStartLine}");
                }
                else
                {
                    Console.WriteLine(" - Auto-start Line: None");
                }
                Console.WriteLine($" - BASIC Size: {bytes.Length} bytes");
                Console.WriteLine($" - Total File Size: {head.Length + bytes.Length} bytes");

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
