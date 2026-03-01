#ifndef TXT2BAS_H
#define TXT2BAS_H

#include <cstdint>
#include <string>
#include <vector>
#include <unordered_map>

namespace txt2bas {

    class Plus3Dos {
    public:
        static std::vector<uint8_t> CreateHeader(int basicLength, int autoStartLine);
    };

    class SinclairNumber {
    public:
        static std::vector<uint8_t> Pack(double number);
    };

    class TokenMap {
    public:
        std::unordered_map<std::string, uint8_t> Map;
        TokenMap();
    };

    class BasConverter {
    private:
        TokenMap _tokenMap;
        std::vector<std::string> _sortedKeys;

        std::vector<uint8_t> ParseLine(int lineNum, const std::string& text);

    public:
        int AutoStartLine = 32768;

        BasConverter();
        std::vector<uint8_t> ConvertFile(const std::string& path);
    };

} // namespace txt2bas

#endif // TXT2BAS_H