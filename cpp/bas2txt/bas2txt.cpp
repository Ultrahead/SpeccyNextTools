#include "bas2txt.h"
#include <fstream>
#include <iostream>
#include <sstream>
#include <cctype>
#include <algorithm>

// TOOL_VERSION is provided by CMake. Fallback set to 1.0.
#ifndef TOOL_VERSION
#define TOOL_VERSION "1.0"
#endif

namespace bas2txt {

    ReverseTokenMap::ReverseTokenMap() {
        // ZX Spectrum Next Extensions
        Map[0x81] = "TIME"; Map[0x82] = "PRIVATE"; Map[0x84] = "ENDIF"; Map[0x85] = "EXIT";
        Map[0x86] = "REF";
        Map[0x87] = "PEEK$"; Map[0x88] = "REG"; Map[0x89] = "DPOKE"; Map[0x8A] = "DPEEK";
        Map[0x8B] = "MOD"; Map[0x8C] = "<<"; Map[0x8D] = ">>"; Map[0x8E] = "UNTIL";
        Map[0x8F] = "ERROR"; Map[0x90] = "ON"; Map[0x91] = "DEFPROC"; Map[0x92] = "ENDPROC";
        Map[0x93] = "PROC"; Map[0x94] = "LOCAL"; Map[0x95] = "DRIVER"; Map[0x96] = "WHILE";
        Map[0x97] = "REPEAT"; Map[0x98] = "ELSE"; Map[0x99] = "REMOUNT"; Map[0x9A] = "BANK";
        Map[0x9B] = "TILE"; Map[0x9C] = "LAYER"; Map[0x9D] = "PALETTE"; Map[0x9E] = "SPRITE";
        Map[0x9F] = "PWD"; Map[0xA0] = "CD"; Map[0xA1] = "MKDIR"; Map[0xA2] = "RMDIR";

        // Aliases missing in the original logic but present in op-table
        Map[0x83] = "ELSE IF"; Map[0xE8] = "CONT"; Map[0xF9] = "RAND";

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

    // Static helper scoped only to this compilation unit to avoid header dependencies
    static std::string GetUnicodeChar(uint8_t code) {
        if (code == 0x60) return "£";
        if (code == 0x7F) return "©";
        return std::string(1, static_cast<char>(code));
    }

    std::string BasParser::Parse(const std::vector<uint8_t>& data) {
        std::stringstream sb;
        size_t offset = 0;
        size_t limit = data.size(); // Type match strictly to resolve C++ warning signed/unsigned

        // Handle +3DOS Header
        if (data.size() >= 128) {
            std::string sig(data.begin(), data.begin() + 8);
            if (sig == "PLUS3DOS" || sig.substr(0, 7) == "ZXPLUS3") {
                uint8_t hType = data[15];
                size_t hFileLength = data[16] | (data[17] << 8);

                // Fix: JS bugs cancel out to write standard Little-Endian bytes, so we parse it as standard Little-Endian
                int autoStart = data[18] | (data[19] << 8);

                size_t hOffset = data[20] | (data[21] << 8);

                // Replicate logic `const length = header.hType === 0 ? header.hOffset : header.hFileLength;`
                size_t payloadLength = (hType == 0) ? hOffset : hFileLength;
                limit = 128 + payloadLength;

                if (limit > data.size()) {
                    limit = data.size();
                }

                if (autoStart != 0 && autoStart != 32768 && autoStart <= 9999) {
                    sb << "#autostart " << autoStart << "\n";
                }
                offset = 128;
            }
        }

        // Handle banked logic
        bool banked = false;
        if (offset + 1 < limit && data[offset] == 0x42 && data[offset + 1] == 0x43) {
            offset += 2;
            banked = true;
        }

        // Iterate through BASIC lines
        while (offset < limit) {
            if (offset + 4 > limit) break;

            // In bas2txt: unpack '<n$line S$length' means BigEndian Line, LittleEndian Length
            int lineNum = (data[offset] << 8) | data[offset + 1];
            size_t lineLen = data[offset + 2] | (data[offset + 3] << 8); // Size_t for bounds comparisons
            offset += 4;

            if (lineLen == 0) break;

            if (lineNum > 9999) {
                if (lineLen == 0x8080 && lineNum == 0x8080 && banked) {
                    break;
                }
                throw std::runtime_error(std::to_string(lineNum) + " is beyond 9999 range: " + std::to_string(lineLen));
            }

            if (offset + lineLen > limit) break;

            // Pass exactly the line content into decoder
            std::string decoded = DecodeLineData(data, static_cast<int>(offset), static_cast<int>(lineLen));
            std::string fullLine = std::to_string(lineNum) + " " + decoded;

            // Trim trailing spaces mimicking JS `lines.push(string.trim());`
            auto last = std::find_if_not(fullLine.rbegin(), fullLine.rend(), [](unsigned char ch) { return std::isspace(ch); }).base();
            fullLine.erase(last, fullLine.end());

            sb << fullLine << "\n";
            offset += lineLen;
        }

        // Remove trailing \n to match JS `.join('\n')`
        std::string result = sb.str();
        if (!result.empty() && result.back() == '\n') {
            result.pop_back();
        }

        return result;
    }

    std::string BasParser::DecodeLineData(const std::vector<uint8_t>& data, int start, int length) {
        std::stringstream sb;
        int end = start + length;

        bool inString = false;
        bool inComment = false;
        int lastNonWhitespace = -1;
        int lastToken = -1;

        for (int i = start; i < end; i++) {
            uint8_t c = data[i];

            if (c == 0x0D) {
                break;
            }

            uint8_t peek = (i + 1 < end) ? data[i + 1] : 0;
            char chr = static_cast<char>(c);

            if (inString || inComment) {
                if (c == 0x60 || c == 0x7F) { // BASIC_CHRS maps
                    sb << GetUnicodeChar(c);
                } else {
                    sb << chr;
                }
            } else {
                if (chr == ';') {
                    // check if we're starting a comment
                    if (lastNonWhitespace == -1 || lastNonWhitespace == ':') {
                        inComment = true;
                    }
                    if (_reverseTokenMap.Map.count(peek)) {
                        sb << chr << ' ';
                    } else {
                        sb << chr;
                    }
                } else if (chr == ':') {
                    if (peek == ';') {
                        sb << chr << ' ';
                    } else {
                        sb << chr;
                    }
                } else if (_reverseTokenMap.Map.count(c)) {
                    std::string keyword = _reverseTokenMap.Map[c];
                    if (keyword == "REM") {
                        inComment = true;
                    }

                    if (lastToken != -1 && _reverseTokenMap.Map.count(lastToken) && _reverseTokenMap.Map[lastToken] == ":") {
                        sb << ' ' << keyword << ' ';
                    } else if (lastToken != -1 && !_reverseTokenMap.Map.count(lastToken) && lastToken != ' ') {
                        sb << ' ' << keyword << ' ';
                    } else {
                        sb << keyword << ' ';
                    }
                } else if (c == 0x0E) {
                    // jump over numeric 5-byte payload.
                    // Let the loop naturally close so `last` correctly registers this mathematical block format
                    i += 5;
                } else {
                    sb << chr;
                }
            }

            if (c == 0x22) { // '"'
                inString = !inString;
            }

            if (chr != ' ') {
                lastNonWhitespace = chr;
            }

            lastToken = c;
        }

        return sb.str();
    }
} // namespace bas2txt

void PrintHelp() {
    std::cout << "ZX Spectrum BASIC-to-Text Converter v" << TOOL_VERSION << "\n"
              << "Usage: bas2txt [options] <input.bas> <output.txt>\n\n"
              << "Options:\n"
              << "  -h, --help     Show this help message\n"
              << "  -v, --version  Show version information\n";
}

int main(int argc, char* argv[]) {
    if (argc >= 2) {
        std::string arg = argv[1];
        if (arg == "-h" || arg == "--help") { PrintHelp(); return 0; }
        if (arg == "-v" || arg == "--version") {
            std::cout << "bas2txt version " << TOOL_VERSION << "\n";
            return 0;
        }
    }

    if (argc < 3) {
        std::cout << "Usage: bas2txt <input.bas> <output.txt>\n"
                  << "Try 'bas2txt --help' for details.\n";
        return 0;
    }

    std::ifstream file(argv[1], std::ios::binary | std::ios::ate);
    if (!file.is_open()) {
        std::cerr << "Error: Could not open input file " << argv[1] << "\n";
        return 1;
    }

    std::streamsize size = file.tellg();
    file.seekg(0, std::ios::beg);
    std::vector<uint8_t> buffer(size);
    if (!file.read((char*)buffer.data(), size)) {
        std::cerr << "Error: Could not read file contents.\n";
        return 1;
    }
    file.close();

    try {
        bas2txt::BasParser parser;
        std::string output = parser.Parse(buffer);

        std::ofstream outFile(argv[2]);
        if (!outFile.is_open()) {
            std::cerr << "Error: Could not open output file " << argv[2] << "\n";
            return 1;
        }
        outFile << output;
        outFile.close();

        std::cout << "Successfully decoded " << argv[1] << " (v" << TOOL_VERSION << ")\n";
    } catch (const std::exception& e) {
        std::cerr << "Error: " << e.what() << "\n";
        return 1;
    }

    return 0;
}
