#include "txt2bas.h"
#include <fstream>
#include <iostream>
#include <regex>
#include <algorithm>
#include <cctype>
#include <cmath>
#include <sstream>

#ifndef TOOL_VERSION
#define TOOL_VERSION "1.0"
#endif

namespace txt2bas {

    std::vector<uint8_t> Plus3Dos::CreateHeader(int basicLength, int autoStartLine) {
        std::vector<uint8_t> header(128, 0);

        std::string sig = "PLUS3DOS";
        std::copy(sig.begin(), sig.end(), header.begin());
        header[8] = 0x1A;
        header[9] = 0x01;
        header[10] = 0x00;

        // Exactly mirrors headers.mjs initialization output logic which doesn't subtract 128 natively from length
        int lengthField = basicLength + 128;
        header[11] = static_cast<uint8_t>(lengthField & 0xFF);
        header[12] = static_cast<uint8_t>((lengthField >> 8) & 0xFF);
        header[13] = static_cast<uint8_t>((lengthField >> 16) & 0xFF);
        header[14] = static_cast<uint8_t>((lengthField >> 24) & 0xFF);

        header[15] = 0x00; // hType = 0 (BASIC)

        int hFileLengthField = basicLength;
        header[16] = static_cast<uint8_t>(hFileLengthField & 0xFF);
        header[17] = static_cast<uint8_t>((hFileLengthField >> 8) & 0xFF);

        if (autoStartLine >= 0 && autoStartLine < 32768) {
            header[18] = static_cast<uint8_t>(autoStartLine & 0xFF);
            header[19] = static_cast<uint8_t>((autoStartLine >> 8) & 0xFF);
        } else {
            // Default 32768 (0x8000) stored as little-endian
            header[18] = 0x00;
            header[19] = 0x80;
        }

        int hOffsetField = basicLength;
        header[20] = static_cast<uint8_t>(hOffsetField & 0xFF);
        header[21] = static_cast<uint8_t>((hOffsetField >> 8) & 0xFF);

        int sum = 0;
        for (int i = 0; i < 127; i++) sum += header[i];
        header[127] = static_cast<uint8_t>(sum % 256);

        return header;
    }

    std::vector<uint8_t> SinclairNumber::Pack(double number) {
        // First check if it can be a compact integer
        double intPart;
        if (std::modf(number, &intPart) == 0.0 && number >= -65535.0 && number <= 65535.0) {
            int val = static_cast<int>(number);
            uint8_t sign = (val < 0) ? 0xFF : 0x00;
            // JS version directly casts signed integer into setUint16 -> two's complement applies natively
            uint16_t uval = static_cast<uint16_t>(val);
            return {0x00, sign, static_cast<uint8_t>(uval & 0xFF), static_cast<uint8_t>((uval >> 8) & 0xFF), 0x00};
        }

        // Float to ZX format conversion
        bool sign = (number < 0.0);
        if (sign) number = -number;

        std::vector<uint8_t> out(5, 0);

        if (number == 0.0) return out;

        out[0] = 0x80;
        while (number < 0.5) {
            number *= 2.0;
            out[0]--;
        }

        while (number >= 1.0) {
            number *= 0.5;
            out[0]++;
        }

        number *= 4294967296.0; // 0x100000000
        number += 0.5; // rounding step

        uint32_t mantissa = static_cast<uint32_t>(number);

        out[1] = static_cast<uint8_t>((mantissa >> 24) & 0xFF);
        out[2] = static_cast<uint8_t>((mantissa >> 16) & 0xFF);
        out[3] = static_cast<uint8_t>((mantissa >> 8) & 0xFF);
        out[4] = static_cast<uint8_t>(mantissa & 0xFF);

        if (!sign) out[1] &= 0x7F;

        return out;
    }

    TokenMap::TokenMap() {
        // ZX Spectrum Next Extensions
        Map["TIME"] = 0x81; Map["PRIVATE"] = 0x82; Map["ENDIF"] = 0x84; Map["EXIT"] = 0x85;
        Map["REF"] = 0x86;
        Map["PEEK$"] = 0x87; Map["REG"] = 0x88; Map["DPOKE"] = 0x89; Map["DPEEK"] = 0x8A;
        Map["MOD"] = 0x8B; Map["<<"] = 0x8C; Map[">>"] = 0x8D; Map["UNTIL"] = 0x8E;
        Map["ERROR"] = 0x8F; Map["ON"] = 0x90; Map["DEFPROC"] = 0x91; Map["ENDPROC"] = 0x92;
        Map["PROC"] = 0x93; Map["LOCAL"] = 0x94; Map["DRIVER"] = 0x95; Map["WHILE"] = 0x96;
        Map["REPEAT"] = 0x97; Map["ELSE"] = 0x98; Map["REMOUNT"] = 0x99; Map["BANK"] = 0x9A;
        Map["TILE"] = 0x9B; Map["LAYER"] = 0x9C; Map["PALETTE"] = 0x9D; Map["SPRITE"] = 0x9E;
        Map["PWD"] = 0x9F; Map["CD"] = 0xA0; Map["MKDIR"] = 0xA1; Map["RMDIR"] = 0xA2;

        // Aliases missing in the original logic but present in op-table
        Map["ELSE IF"] = 0x83; Map["CONT"] = 0xE8; Map["RAND"] = 0xF9;

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

    BasConverter::BasConverter() {
        for (const auto& pair : _tokenMap.Map) {
            _sortedKeys.push_back(pair.first);
        }
        std::sort(_sortedKeys.begin(), _sortedKeys.end(), [](const std::string& a, const std::string& b) {
            return a.length() > b.length();
        });
    }

    bool CaseInsensitiveEquals(const std::string& a, const std::string& b) {
        if (a.length() != b.length()) return false;
        return std::equal(a.begin(), a.end(), b.begin(), [](char c1, char c2) {
            return std::toupper(static_cast<unsigned char>(c1)) == std::toupper(static_cast<unsigned char>(c2));
        });
    }

    std::vector<uint8_t> BasConverter::ConvertFile(const std::string& path) {
        std::vector<uint8_t> output;
        std::ifstream file(path);

        if (!file.is_open()) throw std::runtime_error("Could not open file: " + path);

        std::string line;
        int currentLineNum = 10;
        // Strict match to mimic `^\s*(\d{1,4})\s?(.*)$` logic from parser-version.mjs
        std::regex lineRegex(R"(^\s*(\d{1,4})\s?(.*))");

        while (std::getline(file, line)) {
            line.erase(line.find_last_not_of(" \t\r\n") + 1);

            if (line.empty()) continue;

            if (line[0] == '#') {
                std::string lowerLine = line;
                std::transform(lowerLine.begin(), lowerLine.end(), lowerLine.begin(), [](unsigned char c){ return std::tolower(c); });
                if (lowerLine.find("#autostart") == 0) {
                    std::istringstream iss(line);
                    std::string token; iss >> token;
                    int autoStartVal; if (iss >> autoStartVal) AutoStartLine = autoStartVal;
                }
                continue;
            }

            int lineNum = currentLineNum;
            std::string restOfLine = line;
            std::smatch match;

            if (std::regex_search(line, match, lineRegex)) {
                lineNum = std::stoi(match[1].str());
                restOfLine = match[2].str();
                currentLineNum = lineNum + 10;
            } else {
                currentLineNum += 10;
            }

            std::vector<uint8_t> lineBytes = ParseLine(lineNum, restOfLine);
            output.insert(output.end(), lineBytes.begin(), lineBytes.end());
        }
        return output;
    }

    std::vector<uint8_t> BasConverter::ParseLine(int lineNum, const std::string& text) {
        std::vector<uint8_t> lineData;
        bool expectCommand = true;

        // Exact state machine tracker for duplicating JS tokenization flow
        std::vector<std::string> in_stack;

        // The original JS bug relies entirely on this function blindly wiping values OUT and destroying the array if a key is missed.
        auto popTo = [&](const std::string& type) {
            while (!in_stack.empty()) {
                std::string last = in_stack.back();
                in_stack.pop_back();
                if (last == type) break;
            }
        };
        auto isIn = [&](const std::string& type) {
            return std::find(in_stack.begin(), in_stack.end(), type) != in_stack.end();
        };

        // NextBASIC Integer Expression logic trackers
        bool inIntExpression = false;
        bool inIf = false;
        bool inUntil = false;
        int intParensDepth = 0;

        for (size_t i = 0; i < text.length(); i++) {

            // NextBASIC integer expression prefix '%'
            if (text[i] == '%') {
                inIntExpression = true;
                lineData.push_back('%');
                expectCommand = false;
                if (i + 1 < text.length() && text[i+1] == ' ') i++; // JS slurps exactly ONE space after symbols
                continue;
            }

            // 2. DOT COMMAND (.run, etc.)
            if (expectCommand && text[i] == '.') {
                if (i + 1 < text.length() && std::isdigit(static_cast<unsigned char>(text[i+1]))) {
                    // It's a float, fall through
                } else {
                    size_t pos = i;
                    while (pos < text.length()) {
                        char c = text[pos];
                        if (c == '"') {
                            size_t endQuote = text.find('"', pos + 1);
                            if (endQuote != std::string::npos) pos = endQuote + 1;
                            else pos = text.length();
                        } else if (c == ':' || c == '\n') {
                            break;
                        } else {
                            pos++;
                        }
                    }
                    std::string dotCmd = text.substr(i, pos - i);
                    lineData.insert(lineData.end(), dotCmd.begin(), dotCmd.end());
                    i = pos - 1;
                    expectCommand = false;
                    if (i + 1 < text.length() && text[i+1] == ' ') i++;
                    continue;
                }
            }

            // 3. STRINGS
            if (text[i] == '"') {
                expectCommand = false;
                size_t endQuote = text.find('"', i + 1);
                if (endQuote == std::string::npos) {
                    std::string literal = text.substr(i);
                    lineData.insert(lineData.end(), literal.begin(), literal.end());
                    i = text.length();
                    popTo("STRING_EXPRESSION"); // REPLICATE JS BUG - destroys whole stack if string expression not found
                    in_stack.push_back("STRING_EXPRESSION");
                    break;
                } else {
                    std::string literal = text.substr(i, endQuote - i + 1);
                    lineData.insert(lineData.end(), literal.begin(), literal.end());
                    i = endQuote;
                    popTo("STRING_EXPRESSION"); // REPLICATE JS BUG - destroys whole stack if string expression not found
                    in_stack.push_back("STRING_EXPRESSION");
                    if (i + 1 < text.length() && text[i+1] == ' ') i++; // JS eats space after quotes too
                }
                continue;
            }

            // 4. INLINE COMMENTS (;)
            if (text[i] == ';') {
                bool isComment = true;
                // If it is evaluating after spaces, check the last valid command separator byte
                // Extends to evaluate against THEN and ELSE command contexts safely.
                for (int idx = (int)lineData.size() - 1; idx >= 0; idx--) {
                    uint8_t b = lineData[idx];
                    if (b == ' ' || b == '\t') continue;
                    if (b == ':' || b == 0x8F /* ERROR */ || b == 0xCB /* THEN */ || b == 0x98 /* ELSE */) {
                        isComment = true;
                        break;
                    }
                    isComment = false;
                    break;
                }

                if (isComment) {
                    std::string comment = text.substr(i);
                    lineData.insert(lineData.end(), comment.begin(), comment.end());
                    break;
                }
            }

            // 5. KEYWORDS
            bool matched = false;
            for (const std::string& k : _sortedKeys) {
                if (i + k.length() > text.length()) continue;

                std::string sub = text.substr(i, k.length());
                if (!CaseInsensitiveEquals(sub, k)) continue;

                bool isAlphaStart = std::isalpha(static_cast<unsigned char>(k[0]));
                bool isAlphaEnd = std::isalpha(static_cast<unsigned char>(k.back()));

                bool validBoundary = true;

                if (isAlphaStart) {
                    int back = static_cast<int>(i) - 1;
                    if (back >= 0) {
                        char p = text[back];
                        if (std::isalnum(static_cast<unsigned char>(p)) || p == '_') {
                            validBoundary = false;
                        }
                    }
                }

                if (validBoundary && isAlphaEnd) {
                    size_t next = i + k.length();
                    if (next < text.length()) {
                        char n = text[next];
                        if (std::isalnum(static_cast<unsigned char>(n)) || n == '_') {
                            validBoundary = false;
                        }
                    }
                }

                if (!validBoundary) continue;

                uint8_t token = _tokenMap.Map[k];

                if (token == 0xCE) { // Set DEF FN state
                    in_stack.push_back("DEFFN");
                    in_stack.push_back("DEFFN_SIG");
                }

                if (token == 0xFA || token == 0x83) inIf = true; // IF or ELSE IF
                if (token == 0xCB) {
                    inIf = false; // THEN
                    inIntExpression = false; // Evaluates int Expression Reset unconditionally
                }
                if (token == 0x8E) inUntil = true; // UNTIL
                if (token == 0x84) {
                    inIf = false; // ENDIF
                    inIntExpression = false;
                }

                // Operator check for inIntExpression.
                bool isOperator = false;
                if (k == "AND" || k == "OR" || k == "NOT" || k == "MOD" || k == "-" || k == "+" ||
                    k == "*" || k == "/" || k == "<" || k == ">" || k == "<=" || k == ">=" || k == "<>" ||
                    k == "<<" || k == ">>" || k == "&" || k == "|" || k == "^" || k == "!") {
                    isOperator = true;
                }
                bool isIntFunc = false;
                if (k == "IN" || k == "REG" || k == "PEEK" || k == "DPEEK" || k == "USR" ||
                    k == "BIN" || k == "RND" || k == "BANK" || k == "SPRITE" || k == "INT" ||
                    k == "ABS" || k == "SGN" || k == "CODE") {
                    isIntFunc = true;
                }

                if (!isOperator && !isIntFunc) {
                    if (intParensDepth == 0) {
                        inIntExpression = false;
                    }
                }

                if (token == 0xFA) { // IF
                    bool hasThen = false;
                    std::regex thenRegex(R"(\bTHEN\b)", std::regex_constants::icase);
                    if (std::regex_search(text.begin() + i, text.end(), thenRegex)) {
                        hasThen = true;
                    }
                    if (!hasThen) token = 0x83; // Block IF
                }

                if (token == 0xEA) { // REM
                    lineData.push_back(token);
                    size_t r = i + k.length();
                    if (r < text.length() && text[r] == ' ') r++; // Skip exactly 1 space
                    if (r < text.length()) {
                        std::string remText = text.substr(r);
                        lineData.insert(lineData.end(), remText.begin(), remText.end());
                    }
                    i = text.length(); // Break outer loop
                    matched = true;
                    break;
                } else if (token == 0x82) { // PRIVATE
                    lineData.push_back(token);
                    size_t j = i + k.length();
                    while (j < text.length() && (text[j] == ' ' || text[j] == '\t')) j++;
                    bool hasClear = false;
                    if (j + 5 <= text.length() && CaseInsensitiveEquals(text.substr(j, 5), "CLEAR")) {
                        hasClear = true;
                    }
                    if (!hasClear) {
                        lineData.push_back(0x0E); // Padding marker
                        for (int z = 0; z < 5; z++) lineData.push_back(0x00);
                    }
                    i += k.length() - 1;
                    if (i + 1 < text.length() && text[i+1] == ' ') i++; // JS Space slurp
                    matched = true;
                    break;
                } else if (token == 0xC4) { // BIN
                    lineData.push_back(token);
                    size_t j = i + k.length();
                    while (j < text.length() && (text[j] == ' ' || text[j] == '\t')) j++;
                    std::string binStr = "";
                    while (j < text.length() && (text[j] == '0' || text[j] == '1')) {
                        binStr += text[j++];
                    }
                    if (!binStr.empty()) {
                        lineData.insert(lineData.end(), binStr.begin(), binStr.end());
                        // Only add pack marker if we're not inside a tight integer expression
                        if (!inIntExpression) {
                            lineData.push_back(0x0E);
                            try {
                                unsigned long binVal = std::stoul(binStr, nullptr, 2);
                                std::vector<uint8_t> packed = SinclairNumber::Pack(static_cast<double>(binVal));
                                lineData.insert(lineData.end(), packed.begin(), packed.end());
                            } catch (...) {
                                std::vector<uint8_t> packed(5, 0);
                                lineData.insert(lineData.end(), packed.begin(), packed.end());
                            }
                        }
                        i = j - 1;
                    } else {
                        i += k.length() - 1;
                    }
                    expectCommand = false;
                    if (i + 1 < text.length() && text[i+1] == ' ') i++;
                    matched = true;
                    break;
                } else {
                    lineData.push_back(token);
                    if (token == 0xCB || token == 0x98) expectCommand = true;
                    else expectCommand = false;
                    i += k.length() - 1;
                    if (i + 1 < text.length() && text[i+1] == ' ') i++;
                    matched = true;
                    break;
                }
            }

            if (matched) continue;

            // 6. IDENTIFIERS & VARIABLES
            if (std::isalpha(static_cast<unsigned char>(text[i]))) {
                size_t j = i;
                while (j < text.length() && (std::isalnum(static_cast<unsigned char>(text[j])) || text[j] == '_' || text[j] == '$')) {
                    j++;
                }
                std::string ident = text.substr(i, j - i);
                lineData.insert(lineData.end(), ident.begin(), ident.end());

                if (isIn("STRING_EXPRESSION")) {
                    popTo("STRING_EXPRESSION"); // REPLICATE JS BUG - wipes DEFFN scope context accidentally alongside string expression
                }
                if (!ident.empty() && ident.back() == '$') {
                    in_stack.push_back("STRING_EXPRESSION");
                }
                if (isIn("DEFFN_ARGS")) {
                    // String variables do not get numerical space allocation in Sinclair BASIC
                    if (ident.empty() || ident.back() != '$') {
                        lineData.push_back(0x0E); // Padding marker for DEF FN arguments
                        for (int z = 0; z < 5; z++) lineData.push_back(0x00);
                    }
                }

                i = j - 1;
                expectCommand = false;
                if (i + 1 < text.length() && text[i+1] == ' ') i++;
                continue;
            }

            // 7. HEX AND BINARY NEXTBASIC SYMBOLS (e.g. $, @)
            if (text[i] == '$') {
                size_t j = i + 1;
                std::string hexStr;
                while (j < text.length() && (std::isxdigit(static_cast<unsigned char>(text[j])) || text[j] == '.')) {
                    hexStr += text[j++];
                }
                if (!hexStr.empty()) {
                    lineData.insert(lineData.end(), text.begin() + i, text.begin() + j);
                    if (!inIntExpression) {
                        lineData.push_back(0x0E);
                        try {
                            double val = 0;
                            size_t dotPos = hexStr.find('.');
                            if (dotPos != std::string::npos) {
                                std::string whole = hexStr.substr(0, dotPos);
                                std::string frac = hexStr.substr(dotPos + 1);
                                val = std::stoul(whole, nullptr, 16);
                                if (!frac.empty()) {
                                    val += static_cast<double>(std::stoul(frac, nullptr, 16)) / std::pow(16.0, frac.length());
                                }
                            } else {
                                val = static_cast<double>(std::stoul(hexStr, nullptr, 16));
                            }
                            std::vector<uint8_t> packed = SinclairNumber::Pack(val);
                            lineData.insert(lineData.end(), packed.begin(), packed.end());
                        } catch (...) {
                            std::vector<uint8_t> packed(5, 0);
                            lineData.insert(lineData.end(), packed.begin(), packed.end());
                        }
                    }
                    i = j - 1;
                    expectCommand = false;
                    if (i + 1 < text.length() && text[i+1] == ' ') i++;
                    continue;
                }
            }
            if (text[i] == '@') {
                size_t j = i + 1;
                std::string binStr;
                while (j < text.length() && (text[j] == '0' || text[j] == '1' || text[j] == '.')) {
                    binStr += text[j++];
                }
                if (!binStr.empty()) {
                    lineData.insert(lineData.end(), text.begin() + i, text.begin() + j);
                    if (!inIntExpression) {
                        lineData.push_back(0x0E);
                        try {
                            double val = 0;
                            size_t dotPos = binStr.find('.');
                            if (dotPos != std::string::npos) {
                                std::string whole = binStr.substr(0, dotPos);
                                std::string frac = binStr.substr(dotPos + 1);
                                val = std::stoul(whole, nullptr, 2);
                                if (!frac.empty()) {
                                    val += static_cast<double>(std::stoul(frac, nullptr, 2)) / std::pow(2.0, frac.length());
                                }
                            } else {
                                val = static_cast<double>(std::stoul(binStr, nullptr, 2));
                            }
                            std::vector<uint8_t> packed = SinclairNumber::Pack(val);
                            lineData.insert(lineData.end(), packed.begin(), packed.end());
                        } catch (...) {
                            std::vector<uint8_t> packed(5, 0);
                            lineData.insert(lineData.end(), packed.begin(), packed.end());
                        }
                    }
                    i = j - 1;
                    expectCommand = false;
                    if (i + 1 < text.length() && text[i+1] == ' ') i++;
                    continue;
                }
            }

            // 8. NUMBERS
            if (std::isdigit(static_cast<unsigned char>(text[i])) ||
               (text[i] == '.' && i + 1 < text.length() && std::isdigit(static_cast<unsigned char>(text[i+1])))) {

                expectCommand = false;

                // Numbers inside integer expressions don't get a 6-byte marker. Strictly mirror JS skip marker behavior.
                bool skipMarker = inIntExpression;

                std::string numStr = "";
                size_t j = i;
                while (j < text.length() && (std::isdigit(static_cast<unsigned char>(text[j])) || text[j] == '.' ||
                        (text[j] == 'E' || text[j] == 'e'))) {
                    if (text[j] == 'E' || text[j] == 'e') {
                        numStr += text[j++];
                        if (j < text.length() && (text[j] == '+' || text[j] == '-')) numStr += text[j++];
                    } else {
                        numStr += text[j++];
                    }
                }

                if (!skipMarker) {
                    double val = 0;
                    try { val = std::stod(numStr); } catch(...) {}
                    lineData.insert(lineData.end(), numStr.begin(), numStr.end());
                    lineData.push_back(0x0E); // Explicitly required 6-byte payload start
                    std::vector<uint8_t> packed = SinclairNumber::Pack(val);
                    lineData.insert(lineData.end(), packed.begin(), packed.end());
                } else {
                    lineData.insert(lineData.end(), numStr.begin(), numStr.end());
                }

                i = j - 1;
                if (i + 1 < text.length() && text[i+1] == ' ') i++;
                continue;
            }

            // 9. EXTRA SPACES
            if (text[i] == ' ' || text[i] == '\t') {
                lineData.push_back(static_cast<uint8_t>(text[i]));
                continue;
            }

            // 10. LITERAL
            uint8_t c = static_cast<uint8_t>(text[i]);
            lineData.push_back(c);

            if (c == ':') {
                expectCommand = true;
                in_stack.clear();
                inIf = false;
                inUntil = false;
                intParensDepth = 0;
                inIntExpression = false;
            } else {
                expectCommand = false;
            }

            // Explicitly mirrors JS opTable.ELSEIF missing key bug resulting in blocks tracking '=' as a valid expression reset token.
            if (c == '=' || c == ',' || c == ';') {
                if (intParensDepth == 0 && !inIf && !inUntil) {
                    inIntExpression = false;
                }
            }

            if (c == '(') {
                if (inIntExpression) intParensDepth++;
                in_stack.push_back("OPEN_PARENS");
                if (isIn("DEFFN_SIG")) {
                    in_stack.push_back("DEFFN_ARGS");
                }
            } else if (c == ')') {
                if (intParensDepth > 0) intParensDepth--;
                popTo("OPEN_PARENS");
            } else if (c == '=') {
                if (!in_stack.empty() && in_stack.back() == "DEFFN_SIG") {
                    popTo("DEFFN_SIG");
                }
            }

            if (i + 1 < text.length() && text[i+1] == ' ') i++;
        }

        lineData.push_back(0x0D);

        std::vector<uint8_t> finalLine;
        finalLine.push_back(static_cast<uint8_t>((lineNum >> 8) & 0xFF));
        finalLine.push_back(static_cast<uint8_t>(lineNum & 0xFF));

        size_t length = lineData.size();
        finalLine.push_back(static_cast<uint8_t>(length & 0xFF));
        finalLine.push_back(static_cast<uint8_t>((length >> 8) & 0xFF));

        finalLine.insert(finalLine.end(), lineData.begin(), lineData.end());

        return finalLine;
    }
}

void PrintHelp() {
    std::cout << "  _______     _   ___  ___          \n"
              << " |__   __|   | | |__ \\|  _ \\         \n"
              << "    | |___  _| |_   ) | |_) | __ _ ___\n"
              << "    | / \\ \\/ / __| / /|  _ < / _` / __|\n"
              << "    | |  >  <| |_ / /_| |_) | (_| \\__ \\\n"
              << "    |_| /_/\\_\\\\__|____|____/ \\__,_|___/\n\n"
              << "ZX Spectrum Text-to-BASIC Converter v" << TOOL_VERSION << "\n\n"
              << "Usage: txt2bas [options] <input.txt> <output.bas>\n\n"
              << "Options:\n"
              << "  -h, --help     Show this help message and exit\n"
              << "  -v, --version  Show version information and exit\n";
}

int main(int argc, char* argv[]) {
    if (argc >= 2) {
        std::string arg1 = argv[1];
        if (arg1 == "-h" || arg1 == "--help") { PrintHelp(); return 0; }
        if (arg1 == "-v" || arg1 == "--version") { std::cout << "txt2bas version " << TOOL_VERSION << "\n"; return 0; }
    }
    if (argc < 3) {
        std::cout << "Usage: txt2bas <input.txt> <output.bas>\n";
        return 0;
    }

    try {
        txt2bas::BasConverter converter;
        std::vector<uint8_t> basData = converter.ConvertFile(argv[1]);
        std::vector<uint8_t> header = txt2bas::Plus3Dos::CreateHeader(basData.size(), converter.AutoStartLine);

        std::ofstream out(argv[2], std::ios::binary);
        if (!out.is_open()) throw std::runtime_error("Could not open output file.");

        out.write(reinterpret_cast<const char*>(header.data()), header.size());
        out.write(reinterpret_cast<const char*>(basData.data()), basData.size());
        out.close();

        std::cout << "Success! Created " << argv[2] << " (" << basData.size() << " bytes)\n";
    } catch (const std::exception& ex) {
        std::cout << "Error: " << ex.what() << "\n";
    }
    return 0;
}
