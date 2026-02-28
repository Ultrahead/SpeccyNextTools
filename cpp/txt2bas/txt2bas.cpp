#include "txt2bas.h"
#include <fstream>
#include <iostream>
#include <regex>
#include <algorithm>
#include <cctype>
#include <cmath>
#include <sstream>

namespace txt2bas {
    // --- Plus3Dos Implementation ---
    std::vector<uint8_t> Plus3Dos::CreateHeader(int basicLength, int autoStartLine) {
        std::vector<uint8_t> header(128, 0);

        std::string sig = "PLUS3DOS";
        std::copy(sig.begin(), sig.end(), header.begin());
        header[8] = 0x1A; // Soft EOF

        header[9] = 0x01; // Issue
        header[10] = 0x00; // Version

        int totalFileSize = basicLength + 128;
        header[11] = static_cast<uint8_t>(totalFileSize & 0xFF);
        header[12] = static_cast<uint8_t>((totalFileSize >> 8) & 0xFF);
        header[13] = static_cast<uint8_t>((totalFileSize >> 16) & 0xFF);
        header[14] = static_cast<uint8_t>((totalFileSize >> 24) & 0xFF);

        header[15] = 0x00; // Type: Program

        header[16] = static_cast<uint8_t>(basicLength & 0xFF);
        header[17] = static_cast<uint8_t>((basicLength >> 8) & 0xFF);

        if (autoStartLine >= 0 && autoStartLine < 32768) {
            header[18] = static_cast<uint8_t>(autoStartLine & 0xFF);
            header[19] = static_cast<uint8_t>((autoStartLine >> 8) & 0xFF);
        } else {
            header[18] = static_cast<uint8_t>(32768 & 0xFF);
            header[19] = static_cast<uint8_t>((32768 >> 8) & 0xFF);
        }

        header[20] = static_cast<uint8_t>(basicLength & 0xFF);
        header[21] = static_cast<uint8_t>((basicLength >> 8) & 0xFF);

        int sum = 0;
        for (int i = 0; i < 127; i++) {
            sum += header[i];
        }
        header[127] = static_cast<uint8_t>(sum % 256);

        return header;
    }


    // --- SinclairNumber Implementation ---
    std::vector<uint8_t> SinclairNumber::Pack(double number) {
        double intPart;
        // Check if number is an integer and within bounds
        if (std::modf(number, &intPart) == 0.0 && number >= -65535.0 && number <= 65535.0) {
            int val = static_cast<int>(number);
            uint8_t sign = 0;

            if (val < 0) {
                sign = 0xFF;
                val = -val;
            }

            return {0x00, sign, static_cast<uint8_t>(val & 0xFF), static_cast<uint8_t>((val >> 8) & 0xFF), 0x00};
        }

        return {0, 0, 0, 0, 0};
    }


    // --- TokenMap Implementation ---
    TokenMap::TokenMap() {
        // ZX SPECTRUM NEXT EXTENSIONS
        Map["PEEK$"] = 0x87;
        Map["REG"] = 0x88;
        Map["DPOKE"] = 0x89;
        Map["DPEEK"] = 0x8A;
        Map["MOD"] = 0x8B;
        Map["<<"] = 0x8C;
        Map[">>"] = 0x8D;
        Map["UNTIL"] = 0x8E;
        Map["ERROR"] = 0x8F;
        Map["ON"] = 0x90;
        Map["DEFPROC"] = 0x91;
        Map["ENDPROC"] = 0x92;
        Map["PROC"] = 0x93;
        Map["LOCAL"] = 0x94;
        Map["DRIVER"] = 0x95;
        Map["WHILE"] = 0x96;
        Map["REPEAT"] = 0x97;
        Map["ELSE"] = 0x98;
        Map["REMOUNT"] = 0x99;
        Map["BANK"] = 0x9A;
        Map["TILE"] = 0x9B;
        Map["LAYER"] = 0x9C;
        Map["PALETTE"] = 0x9D;
        Map["SPRITE"] = 0x9E;
        Map["PWD"] = 0x9F;
        Map["CD"] = 0xA0;
        Map["MKDIR"] = 0xA1;
        Map["RMDIR"] = 0xA2;

        // STANDARD ZX SPECTRUM 48K TOKENS
        Map["SPECTRUM"] = 0xA3;
        Map["PLAY"] = 0xA4;
        Map["RND"] = 0xA5;
        Map["INKEY$"] = 0xA6;
        Map["PI"] = 0xA7;
        Map["FN"] = 0xA8;
        Map["POINT"] = 0xA9;
        Map["SCREEN$"] = 0xAA;
        Map["ATTR"] = 0xAB;
        Map["AT"] = 0xAC;
        Map["TAB"] = 0xAD;
        Map["VAL$"] = 0xAE;
        Map["CODE"] = 0xAF;
        Map["VAL"] = 0xB0;
        Map["LEN"] = 0xB1;
        Map["SIN"] = 0xB2;
        Map["COS"] = 0xB3;
        Map["TAN"] = 0xB4;
        Map["ASN"] = 0xB5;
        Map["ACS"] = 0xB6;
        Map["ATN"] = 0xB7;
        Map["LN"] = 0xB8;
        Map["EXP"] = 0xB9;
        Map["INT"] = 0xBA;
        Map["SQR"] = 0xBB;
        Map["SGN"] = 0xBC;
        Map["ABS"] = 0xBD;
        Map["PEEK"] = 0xBE;
        Map["IN"] = 0xBF;
        Map["USR"] = 0xC0;
        Map["STR$"] = 0xC1;
        Map["CHR$"] = 0xC2;
        Map["NOT"] = 0xC3;
        Map["BIN"] = 0xC4;
        Map["OR"] = 0xC5;
        Map["AND"] = 0xC6;
        Map["<="] = 0xC7;
        Map[">="] = 0xC8;
        Map["<>"] = 0xC9;
        Map["LINE"] = 0xCA;
        Map["THEN"] = 0xCB;
        Map["TO"] = 0xCC;
        Map["STEP"] = 0xCD;
        Map["DEF FN"] = 0xCE;
        Map["CAT"] = 0xCF;
        Map["FORMAT"] = 0xD0;
        Map["MOVE"] = 0xD1;
        Map["ERASE"] = 0xD2;
        Map["OPEN #"] = 0xD3;
        Map["CLOSE #"] = 0xD4;
        Map["MERGE"] = 0xD5;
        Map["VERIFY"] = 0xD6;
        Map["BEEP"] = 0xD7;
        Map["CIRCLE"] = 0xD8;
        Map["INK"] = 0xD9;
        Map["PAPER"] = 0xDA;
        Map["FLASH"] = 0xDB;
        Map["BRIGHT"] = 0xDC;
        Map["INVERSE"] = 0xDD;
        Map["OVER"] = 0xDE;
        Map["OUT"] = 0xDF;
        Map["LPRINT"] = 0xE0;
        Map["LLIST"] = 0xE1;
        Map["STOP"] = 0xE2;
        Map["READ"] = 0xE3;
        Map["DATA"] = 0xE4;
        Map["RESTORE"] = 0xE5;
        Map["NEW"] = 0xE6;
        Map["BORDER"] = 0xE7;
        Map["CONTINUE"] = 0xE8;
        Map["DIM"] = 0xE9;
        Map["REM"] = 0xEA;
        Map["FOR"] = 0xEB;
        Map["GO TO"] = 0xEC;
        Map["GOTO"] = 0xEC;
        Map["GO SUB"] = 0xED;
        Map["GOSUB"] = 0xED;
        Map["INPUT"] = 0xEE;
        Map["LOAD"] = 0xEF;
        Map["LIST"] = 0xF0;
        Map["LET"] = 0xF1;
        Map["PAUSE"] = 0xF2;
        Map["NEXT"] = 0xF3;
        Map["POKE"] = 0xF4;
        Map["PRINT"] = 0xF5;
        Map["PLOT"] = 0xF6;
        Map["RUN"] = 0xF7;
        Map["SAVE"] = 0xF8;
        Map["RANDOMIZE"] = 0xF9;
        Map["IF"] = 0xFA;
        Map["CLS"] = 0xFB;
        Map["DRAW"] = 0xFC;
        Map["CLEAR"] = 0xFD;
        Map["RETURN"] = 0xFE;
        Map["COPY"] = 0xFF;
    }


    // --- BasConverter Implementation ---
    BasConverter::BasConverter() {
        for (const auto &pair: _tokenMap.Map) {
            _sortedKeys.push_back(pair.first);
        }
        std::sort(_sortedKeys.begin(), _sortedKeys.end(), [](const std::string &a, const std::string &b) {
            return a.length() > b.length();
        });
    }

    // Helper for case-insensitive string matching
    bool CaseInsensitiveEquals(const std::string &a, const std::string &b) {
        if (a.length() != b.length()) return false;
        return std::equal(a.begin(), a.end(), b.begin(), [](char c1, char c2) {
            return std::toupper(static_cast<unsigned char>(c1)) == std::toupper(static_cast<unsigned char>(c2));
        });
    }

    std::vector<uint8_t> BasConverter::ConvertFile(const std::string &path) {
        std::vector<uint8_t> output;
        std::ifstream file(path);

        if (!file.is_open()) {
            throw std::runtime_error("Could not open file: " + path);
        }

        std::string line;
        int currentLineNum = 10;
        std::regex lineRegex(R"(^(\d+)\s+(.*))");

        while (std::getline(file, line)) {
            // Trim whitespace
            line.erase(0, line.find_first_not_of(" \t\r\n"));
            line.erase(line.find_last_not_of(" \t\r\n") + 1);

            if (line.empty()) continue;

            if (line[0] == '#') {
                std::string lowerLine = line;
                std::transform(lowerLine.begin(), lowerLine.end(), lowerLine.begin(),
                               [](unsigned char c) { return std::tolower(c); });

                if (lowerLine.find("#autostart") == 0) {
                    std::istringstream iss(line);
                    std::string token;
                    iss >> token;
                    int autoStartVal;
                    if (iss >> autoStartVal) {
                        AutoStartLine = autoStartVal;
                    }
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

    std::vector<uint8_t> BasConverter::ParseLine(int lineNum, const std::string &text) {
        std::vector<uint8_t> lineData;

        for (size_t i = 0; i < text.length(); i++) {
            // String Literals
            if (text[i] == '"') {
                size_t endQuote = text.find('"', i + 1);
                if (endQuote == std::string::npos) {
                    std::string literal = text.substr(i);
                    lineData.insert(lineData.end(), literal.begin(), literal.end());
                    i = text.length();
                } else {
                    std::string literal = text.substr(i, endQuote - i + 1);
                    lineData.insert(lineData.end(), literal.begin(), literal.end());
                    i = endQuote;
                }
                continue;
            }

            // Numbers
            if (std::isdigit(static_cast<unsigned char>(text[i])) ||
                (text[i] == '.' && i + 1 < text.length() && std::isdigit(static_cast<unsigned char>(text[i + 1])))) {
                std::string numStr = "";
                size_t j = i;
                while (j < text.length() && (std::isdigit(static_cast<unsigned char>(text[j])) || text[j] == '.')) {
                    numStr += text[j];
                    j++;
                }

                try {
                    double val = std::stod(numStr);
                    lineData.insert(lineData.end(), numStr.begin(), numStr.end());
                    lineData.push_back(0x0E); // Hidden Marker
                    std::vector<uint8_t> packed = SinclairNumber::Pack(val);
                    lineData.insert(lineData.end(), packed.begin(), packed.end());
                    i = j - 1;
                    continue;
                } catch (...) {
                    // Fallthrough to standard character matching if parsing fails
                }
            }

            // Comment Handling
            if (text[i] == ';') {
                bool isComment = false;
                int back = static_cast<int>(i) - 1;
                while (back >= 0 && text[back] == ' ') back--;

                if (back < 0) isComment = true;
                else if (text[back] == ':') isComment = true;

                if (isComment) {
                    std::string comment = text.substr(i);
                    lineData.insert(lineData.end(), comment.begin(), comment.end());
                    i = text.length();
                    continue;
                }
            }

            // Keywords
            bool matched = false;
            for (const std::string &k: _sortedKeys) {
                if (i + k.length() > text.length()) continue;

                std::string sub = text.substr(i, k.length());
                if (!CaseInsensitiveEquals(sub, k)) continue;

                bool isAlphaToken = std::isalpha(static_cast<unsigned char>(k[0]));
                bool prevCharValid = (i == 0) || !std::isalpha(static_cast<unsigned char>(text[i - 1]));
                bool nextCharValid = (i + k.length() >= text.length()) || !std::isalnum(
                                         static_cast<unsigned char>(text[i + k.length()]));

                if (isAlphaToken && (!prevCharValid || !nextCharValid)) continue;

                uint8_t token = _tokenMap.Map[k];
                lineData.push_back(token);
                i += k.length();
                matched = true;

                if (token == 0xEA) {
                    // REM
                    if (i < text.length()) {
                        std::string comment = text.substr(i);
                        lineData.insert(lineData.end(), comment.begin(), comment.end());
                        i = text.length();
                    }
                } else {
                    while (i < text.length() && text[i] == ' ') i++;
                }

                i--;
                break;
            }

            if (matched) continue;

            // Literal
            lineData.push_back(static_cast<uint8_t>(text[i]));
        }

        lineData.push_back(0x0D);

        // Construct Line Header
        std::vector<uint8_t> finalLine;
        finalLine.push_back(static_cast<uint8_t>((lineNum >> 8) & 0xFF));
        finalLine.push_back(static_cast<uint8_t>(lineNum & 0xFF));

        size_t length = lineData.size();
        finalLine.push_back(static_cast<uint8_t>(length & 0xFF));
        finalLine.push_back(static_cast<uint8_t>((length >> 8) & 0xFF));

        finalLine.insert(finalLine.end(), lineData.begin(), lineData.end());

        return finalLine;
    }
} // namespace txt2bas

// --- Main Entry Point ---
int main(int argc, char *argv[]) {
    if (argc < 3) {
        std::cout << "Usage: txt2bas <input.txt> <output.bas>\n";
        return 0;
    }

    std::string inputFile = argv[1];
    std::string outputFile = argv[2];

    std::ifstream infile(inputFile);
    if (!infile.good()) {
        std::cout << "Error: Input file '" << inputFile << "' not found.\n";
        return 0;
    }
    infile.close();

    try {
        txt2bas::BasConverter converter;
        std::vector<uint8_t> basData = converter.ConvertFile(inputFile);
        std::vector<uint8_t> header = txt2bas::Plus3Dos::CreateHeader(basData.size(), converter.AutoStartLine);

        std::ofstream out(outputFile, std::ios::binary);
        if (!out.is_open()) {
            std::cout << "Error: Could not open output file.\n";
            return 0;
        }

        out.write(reinterpret_cast<const char *>(header.data()), header.size());
        out.write(reinterpret_cast<const char *>(basData.data()), basData.size());
        out.close();

        std::cout << "Success! Created " << outputFile << "\n";
        if (converter.AutoStartLine < 32768) {
            std::cout << " - Auto-start Line: " << converter.AutoStartLine << "\n";
        } else {
            std::cout << " - Auto-start Line: None\n";
        }
        std::cout << " - BASIC Size: " << basData.size() << " bytes\n";
        std::cout << " - Total File Size: " << header.size() + basData.size() << " bytes\n";
    } catch (const std::exception &ex) {
        std::cout << "Error: " << ex.what() << "\n";
    }

    return 0;
}
