#include "txt2bas.h"
#include <fstream>
#include <iostream>
#include <regex>
#include <algorithm>
#include <cctype>
#include <cmath>
#include <sstream>

// TOOL_VERSION is injected via CMake target_compile_definitions.
// We provide "1.0" as a fallback for manual compilation.
#ifndef TOOL_VERSION
#define TOOL_VERSION "1.0"
#endif

namespace txt2bas {

    // --- Plus3Dos Implementation ---
    // Creates the 128-byte +3DOS header required for Sinclair BASIC files.
    std::vector<uint8_t> Plus3Dos::CreateHeader(int basicLength, int autoStartLine) {
        std::vector<uint8_t> header(128, 0);

        // Bytes 0-7: Signature
        std::string sig = "PLUS3DOS";
        std::copy(sig.begin(), sig.end(), header.begin());
        
        header[8] = 0x1A; // Byte 8: Soft EOF
        header[9] = 0x01; // Byte 9: Issue number
        header[10] = 0x00; // Byte 10: Version number

        // Bytes 11-14: Total file length (including header)
        int totalFileSize = basicLength + 128;
        header[11] = static_cast<uint8_t>(totalFileSize & 0xFF);
        header[12] = static_cast<uint8_t>((totalFileSize >> 8) & 0xFF);
        header[13] = static_cast<uint8_t>((totalFileSize >> 16) & 0xFF);
        header[14] = static_cast<uint8_t>((totalFileSize >> 24) & 0xFF);

        header[15] = 0x00; // Byte 15: File type (0 = BASIC Program)
        
        // Bytes 16-17: BASIC data length
        header[16] = static_cast<uint8_t>(basicLength & 0xFF);
        header[17] = static_cast<uint8_t>((basicLength >> 8) & 0xFF);

        // Bytes 18-19: Auto-start line (32768 = None)
        if (autoStartLine >= 0 && autoStartLine < 32768) {
            header[18] = static_cast<uint8_t>(autoStartLine & 0xFF);
            header[19] = static_cast<uint8_t>((autoStartLine >> 8) & 0xFF);
        } else {
            header[18] = 0x00;
            header[19] = 0x80; // 32768 in little-endian
        }

        // Bytes 20-21: Program offset (usually same as basicLength)
        header[20] = static_cast<uint8_t>(basicLength & 0xFF);
        header[21] = static_cast<uint8_t>((basicLength >> 8) & 0xFF);

        // Byte 127: Checksum (sum of bytes 0-126 MOD 256)
        int sum = 0;
        for (int i = 0; i < 127; i++) {
            sum += header[i];
        }
        header[127] = static_cast<uint8_t>(sum % 256);

        return header;
    }

    // --- SinclairNumber Implementation ---
    // Packs floating point numbers into the 5-byte ZX Spectrum internal format.
    std::vector<uint8_t> SinclairNumber::Pack(double number) {
        double intPart;
        // Optimization: Use specialized 5-byte integer format for small whole numbers
        if (std::modf(number, &intPart) == 0.0 && number >= -65535.0 && number <= 65535.0) {
            int val = static_cast<int>(number);
            uint8_t sign = (val < 0) ? 0xFF : 0x00;
            if (val < 0) val = -val;

            return {0x00, sign, static_cast<uint8_t>(val & 0xFF), static_cast<uint8_t>((val >> 8) & 0xFF), 0x00};
        }
        // General floating point packing is omitted for this CLI utility
        return {0, 0, 0, 0, 0};
    }

    // --- TokenMap Implementation ---
    // Maps BASIC keywords to their corresponding 1-byte tokens.
    TokenMap::TokenMap() {
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

    // --- BasConverter Implementation ---
    BasConverter::BasConverter() {
        for (const auto& pair : _tokenMap.Map) {
            _sortedKeys.push_back(pair.first);
        }
        // Longest-match sorting: "GO TO" must match before "GO"
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
        if (!file.is_open()) throw std::runtime_error("Could not open source file: " + path);

        std::string line;
        int currentLineNum = 10;
        std::regex lineRegex(R"(^(\d+)\s+(.*))");

        while (std::getline(file, line)) {
            line.erase(0, line.find_first_not_of(" \t\r\n"));
            line.erase(line.find_last_not_of(" \t\r\n") + 1);

            if (line.empty()) continue;

            // Directives: #autostart [line]
            if (line[0] == '#') {
                std::string lowerLine = line;
                std::transform(lowerLine.begin(), lowerLine.end(), lowerLine.begin(), 
                    [](unsigned char c){ return std::tolower(c); });
                
                if (lowerLine.find("#autostart") == 0) {
                    std::istringstream iss(line);
                    std::string t; iss >> t;
                    int val; if (iss >> val) AutoStartLine = val;
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

        for (size_t i = 0; i < text.length(); i++) {
            
            // Maintain command state through spaces
            if (text[i] == ' ') {
                lineData.push_back(' ');
                continue;
            }

            // Dot command detection (.run, .cd, etc.)
            if (expectCommand && text[i] == '.') {
                if (i + 1 < text.length() && std::isdigit(static_cast<unsigned char>(text[i+1]))) {
                    // Number starting with '.', let it fall through to numeric logic
                } else {
                    // Dot command found: treat the rest of the line as literal text
                    std::string dotCmd = text.substr(i);
                    lineData.insert(lineData.end(), dotCmd.begin(), dotCmd.end());
                    break;
                }
            }

            // Strings
            if (text[i] == '"') {
                expectCommand = false;
                size_t end = text.find('"', i + 1);
                if (end == std::string::npos) {
                    std::string literal = text.substr(i);
                    lineData.insert(lineData.end(), literal.begin(), literal.end());
                    i = text.length();
                } else {
                    std::string literal = text.substr(i, end - i + 1);
                    lineData.insert(lineData.end(), literal.begin(), literal.end());
                    i = end;
                }
                continue;
            }

            // Numeric logic
            if (std::isdigit(static_cast<unsigned char>(text[i])) || (text[i] == '.' && i + 1 < text.length() && std::isdigit(static_cast<unsigned char>(text[i+1])))) {
                expectCommand = false;
                std::string numStr = "";
                size_t j = i;
                while (j < text.length() && (std::isdigit(static_cast<unsigned char>(text[j])) || text[j] == '.')) {
                    numStr += text[j++];
                }
                try {
                    double val = std::stod(numStr);
                    lineData.insert(lineData.end(), numStr.begin(), numStr.end());
                    lineData.push_back(0x0E); // Marker
                    std::vector<uint8_t> packed = SinclairNumber::Pack(val);
                    lineData.insert(lineData.end(), packed.begin(), packed.end());
                    i = j - 1;
                    continue;
                } catch (...) {}
            }

            // Keyword processing
            bool matched = false;
            for (const std::string& k : _sortedKeys) {
                if (i + k.length() > text.length()) continue;
                if (!CaseInsensitiveEquals(text.substr(i, k.length()), k)) continue;

                bool isAlpha = std::isalpha(static_cast<unsigned char>(k[0]));
                bool pOk = (i == 0) || !std::isalpha(static_cast<unsigned char>(text[i - 1]));
                bool nOk = (i + k.length() >= text.length()) || !std::isalnum(static_cast<unsigned char>(text[i + k.length()]));
                
                if (isAlpha && (!pOk || !nOk)) continue;

                uint8_t token = _tokenMap.Map[k];
                lineData.push_back(token);
                i += k.length();
                matched = true;

                if (token == 0xEA) { // REM - copy literal
                    expectCommand = false;
                    if (i < text.length()) {
                        std::string rem = text.substr(i);
                        lineData.insert(lineData.end(), rem.begin(), rem.end());
                        i = text.length();
                    }
                } else {
                    if (token == 0xCB || token == 0x98) {
                        expectCommand = true; // THEN or ELSE allows a new command
                    } else {
                        expectCommand = false;
                    }
                    while (i < text.length() && text[i] == ' ') i++;
                }
                i--; break;
            }
            if (!matched) {
                lineData.push_back(static_cast<uint8_t>(text[i]));
                if (text[i] == ':') {
                    expectCommand = true;
                } else {
                    expectCommand = false;
                }
            }
        }

        lineData.push_back(0x0D); // Spectrum EOL
        
        // Final Line structure
        std::vector<uint8_t> result;
        result.push_back(static_cast<uint8_t>((lineNum >> 8) & 0xFF));
        result.push_back(static_cast<uint8_t>(lineNum & 0xFF));
        
        size_t len = lineData.size();
        result.push_back(static_cast<uint8_t>(len & 0xFF));
        result.push_back(static_cast<uint8_t>((len >> 8) & 0xFF));
        
        result.insert(result.end(), lineData.begin(), lineData.end());
        return result;
    }

} // namespace txt2bas

void PrintUsage() {
    std::cout << "ZX Spectrum Text-to-BASIC Converter v" << TOOL_VERSION << "\n"
              << "Usage: txt2bas [options] <input.txt> <output.bas>\n\n"
              << "Options:\n"
              << "  -h, --help     Show help\n"
              << "  -v, --version  Show version\n";
}

int main(int argc, char* argv[]) {
    if (argc >= 2) {
        std::string arg = argv[1];
        if (arg == "-h" || arg == "--help") { PrintUsage(); return 0; }
        if (arg == "-v" || arg == "--version") { std::cout << "txt2bas version " << TOOL_VERSION << "\n"; return 0; }
    }

    if (argc < 3) {
        std::cout << "Usage: txt2bas <input.txt> <output.bas>\nSee 'txt2bas --help' for details.\n";
        return 0;
    }

    try {
        txt2bas::BasConverter conv;
        std::vector<uint8_t> bytes = conv.ConvertFile(argv[1]);
        std::vector<uint8_t> head = txt2bas::Plus3Dos::CreateHeader(static_cast<int>(bytes.size()), conv.AutoStartLine);

        std::ofstream out(argv[2], std::ios::binary);
        if (!out.is_open()) throw std::runtime_error("Cannot write to output file.");

        out.write(reinterpret_cast<const char*>(head.data()), head.size());
        out.write(reinterpret_cast<const char*>(bytes.data()), bytes.size());
        out.close();

        std::cout << "Successfully created " << argv[2] << " (v" << TOOL_VERSION << ")\n";
    } catch (const std::exception& e) {
        std::cerr << "Error: " << e.what() << "\n";
        return 1;
    }

    return 0;
}
