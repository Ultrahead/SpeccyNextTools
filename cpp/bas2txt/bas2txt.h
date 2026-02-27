#ifndef BAS2TXT_H
#define BAS2TXT_H

#include <cstdint>
#include <string>
#include <vector>
#include <unordered_map>

namespace bas2txt {
    class ReverseTokenMap {
    public:
        std::unordered_map<uint8_t, std::string> Map;

        ReverseTokenMap();
    };

    class BasParser {
    private:
        ReverseTokenMap _reverseTokenMap;

        std::string DecodeLineData(const std::vector<uint8_t> &data, int start, int length);

    public:
        std::string Parse(const std::vector<uint8_t> &data);
    };
} // namespace bas2txt

#endif // BAS2TXT_H
