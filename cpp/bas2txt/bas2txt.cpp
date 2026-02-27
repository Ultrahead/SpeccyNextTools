#include "bas2txt.h"
#include <fstream>
#include <iostream>
#include <sstream>
#include <cctype>

namespace bas2txt {
    // --- ReverseTokenMap Implementation ---
    ReverseTokenMap::ReverseTokenMap() {
        // === ZX SPECTRUM NEXT EXTENSIONS (0x80 - 0xA2) ===
        Map[0x87] = "PEEK$";
        Map[0x88] = "REG";
        Map[0x89] = "DPOKE";
        Map[0x8A] = "DPEEK";
        Map[0x8B] = "MOD";
        Map[0x8C] = "<<";
        Map[0x8D] = ">>";
        Map[0x8E] = "UNTIL";
        Map[0x8F] = "ERROR";
        Map[0x90] = "ON";
        Map[0x91] = "DEFPROC";
        Map[0x92] = "ENDPROC";
        Map[0x93] = "PROC";
        Map[0x94] = "LOCAL";
        Map[0x95] = "DRIVER";
        Map[0x96] = "WHILE";
        Map[0x97] = "REPEAT";
        Map[0x98] = "ELSE";
        Map[0x99] = "REMOUNT";
        Map[0x9A] = "BANK";
        Map[0x9B] = "TILE";
        Map[0x9C] = "LAYER";
        Map[0x9D] = "PALETTE";
        Map[0x9E] = "SPRITE";
        Map[0x9F] = "PWD";
        Map[0xA0] = "CD";
        Map[0xA1] = "MKDIR";
        Map[0xA2] = "RMDIR";

        // === STANDARD ZX SPECTRUM 48K TOKENS (0xA3 - 0xFF) ===
        Map[0xA3] = "SPECTRUM";
        Map[0xA4] = "PLAY";
        Map[0xA5] = "RND";
        Map[0xA6] = "INKEY$";
        Map[0xA7] = "PI";
        Map[0xA8] = "FN";
        Map[0xA9] = "POINT";
        Map[0xAA] = "SCREEN$";
        Map[0xAB] = "ATTR";
        Map[0xAC] = "AT";
        Map[0xAD] = "TAB";
        Map[0xAE] = "VAL$";
        Map[0xAF] = "CODE";
        Map[0xB0] = "VAL";
        Map[0xB1] = "LEN";
        Map[0xB2] = "SIN";
        Map[0xB3] = "COS";
        Map[0xB4] = "TAN";
        Map[0xB5] = "ASN";
        Map[0xB6] = "ACS";
        Map[0xB7] = "ATN";
        Map[0xB8] = "LN";
        Map[0xB9] = "EXP";
        Map[0xBA] = "INT";
        Map[0xBB] = "SQR";
        Map[0xBC] = "SGN";
        Map[0xBD] = "ABS";
        Map[0xBE] = "PEEK";
        Map[0xBF] = "IN";
        Map[0xC0] = "USR";
        Map[0xC1] = "STR$";
        Map[0xC2] = "CHR$";
        Map[0xC3] = "NOT";
        Map[0xC4] = "BIN";
        Map[0xC5] = "OR";
        Map[0xC6] = "AND";
        Map[0xC7] = "<=";
        Map[0xC8] = ">=";
        Map[0xC9] = "<>";
        Map[0xCA] = "LINE";
        Map[0xCB] = "THEN";
        Map[0xCC] = "TO";
        Map[0xCD] = "STEP";
        Map[0xCE] = "DEF FN";
        Map[0xCF] = "CAT";
        Map[0xD0] = "FORMAT";
        Map[0xD1] = "MOVE";
        Map[0xD2] = "ERASE";
        Map[0xD3] = "OPEN #";
        Map[0xD4] = "CLOSE #";
        Map[0xD5] = "MERGE";
        Map[0xD6] = "VERIFY";
        Map[0xD7] = "BEEP";
        Map[0xD8] = "CIRCLE";
        Map[0xD9] = "INK";
        Map[0xDA] = "PAPER";
        Map[0xDB] = "FLASH";
        Map[0xDC] = "BRIGHT";
        Map[0xDD] = "INVERSE";
        Map[0xDE] = "OVER";
        Map[0xDF] = "OUT";
        Map[0xE0] = "LPRINT";
        Map[0xE1] = "LLIST";
        Map[0xE2] = "STOP";
        Map[0xE3] = "READ";
        Map[0xE4] = "DATA";
        Map[0xE5] = "RESTORE";
        Map[0xE6] = "NEW";
        Map[0xE7] = "BORDER";
        Map[0xE8] = "CONTINUE";
        Map[0xE9] = "DIM";
        Map[0xEA] = "REM";
        Map[0xEB] = "FOR";
        Map[0xEC] = "GO TO";
        Map[0xED] = "GO SUB";
        Map[0xEE] = "INPUT";
        Map[0xEF] = "LOAD";
        Map[0xF0] = "LIST";
        Map[0xF1] = "LET";
        Map[0xF2] = "PAUSE";
        Map[0xF3] = "NEXT";
        Map[0xF4] = "POKE";
        Map[0xF5] = "PRINT";
        Map[0xF6] = "PLOT";
        Map[0xF7] = "RUN";
        Map[0xF8] = "SAVE";
        Map[0xF9] = "RANDOMIZE";
        Map[0xFA] = "IF";
        Map[0xFB] = "CLS";
        Map[0xFC] = "DRAW";
        Map[0xFD] = "CLEAR";
        Map[0xFE] = "RETURN";
        Map[0xFF] = "COPY";
    }

    // --- BasParser Implementation ---
    std::string BasParser::Parse(const std::vector<uint8_t> &data) {
        std::stringstream sb;
        size_t offset = 0;

        // 1. Detect and Process +3DOS Header (128 bytes)
        if (data.size() >= 128) {
            std::string signature(data.begin(), data.begin() + 8);

            if (signature == "PLUS3DOS" || signature.substr(0, 7) == "ZXPLUS3") {
                // Check for Auto-start line (Bytes 18-19, Little Endian)
                int autoStart = data[18] | (data[19] << 8);

                if (autoStart != 32768) {
                    sb << "#autostart " << autoStart << "\n";
                }
                offset = 128;
            }
        }

        // 2. Parse Lines
        while (offset < data.size()) {
            if (offset + 4 > data.size()) break;

            // Line Number (Big Endian)
            int lineNum = (data[offset] << 8) | data[offset + 1];
            offset += 2;

            // Line Length (Little Endian)
            int lineLen = data[offset] | (data[offset + 1] << 8);
            offset += 2;

            if (offset + lineLen - 1 > data.size()) break;

            // Process Line Data
            std::string lineContent = DecodeLineData(data, offset, lineLen - 1);
            sb << lineNum << " " << lineContent << "\n";

            offset += lineLen;
        }

        return sb.str();
    }

    std::string BasParser::DecodeLineData(const std::vector<uint8_t> &data, int start, int length) {
        std::stringstream sb;
        int end = start + length;

        for (int i = start; i < end; i++) {
            uint8_t b = data[i];

            // Case 1: Hidden Number Marker (0x0E)
            if (b == 0x0E) {
                i += 5;
                continue;
            }

            // Case 2: Token (Keywords)
            if (_reverseTokenMap.Map.find(b) != _reverseTokenMap.Map.end()) {
                sb << _reverseTokenMap.Map[b];

                // SMART SPACING LOGIC
                if (i + 1 < end) {
                    uint8_t next = data[i + 1];
                    if (next < 128 && next != 0x0E) {
                        char c = static_cast<char>(next);
                        if (std::isalnum(static_cast<unsigned char>(c)) || c == '"' || c == '.') {
                            sb << ' ';
                        }
                    }
                }
            }
            // Case 3 & 4: Printable ASCII and Copyright Symbol
            else {
                if (b >= 32 && b <= 126) {
                    sb << static_cast<char>(b);
                } else if (b == 0x7F) {
                    // Outputting UTF-8 Copyright symbol (©)
                    sb << "\xC2\xA9";
                }
            }
        }

        return sb.str();
    }
} // namespace Bas2Txt


// --- Main Entry Point ---
int main(int argc, char *argv[]) {
    if (argc < 3) {
        std::cout << "Usage: bas2txt <input.bas> <output.txt>\n";
        return 0;
    }

    std::string inputFile = argv[1];
    std::string outputFile = argv[2];

    // Read all bytes
    std::ifstream file(inputFile, std::ios::binary | std::ios::ate);
    if (!file.is_open()) {
        std::cout << "Error: Input file '" << inputFile << "' not found.\n";
        return 0;
    }

    std::streamsize size = file.tellg();
    file.seekg(0, std::ios::beg);
    std::vector<uint8_t> fileBytes(size);
    if (!file.read(reinterpret_cast<char *>(fileBytes.data()), size)) {
        std::cout << "Error: Could not read file contents.\n";
        return 0;
    }
    file.close();

    // Parse and write text
    try {
        bas2txt::BasParser parser;
        std::string decodedText = parser.Parse(fileBytes);

        std::ofstream out(outputFile);
        if (!out.is_open()) {
            std::cout << "Error: Could not open output file.\n";
            return 0;
        }

        out << decodedText;
        out.close();

        std::cout << "Success! Decoded " << inputFile << " to " << outputFile << "\n";
    } catch (const std::exception &ex) {
        std::cout << "Error: " << ex.what() << "\n";
    }

    return 0;
}
