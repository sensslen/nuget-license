// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Linq;

namespace SPDXLicenseMatcher.JavaPort
{
    public static class LicenseCompareHelper
    {
        public static string? GetFirstLicenseToken(string? text) => text?.Split(' ').FirstOrDefault(s => !string.IsNullOrEmpty(s));

        public static string LocateOriginalText(string originalText, int startToken, int endToken, Dictionary<int, LineColumn> tokenToLocation, string[] matchTokens)
        {
            if (startToken > endToken || !tokenToLocation.TryGetValue(startToken, out LineColumn? start) || !tokenToLocation.TryGetValue(endToken, out LineColumn? end))
            {
                return string.Empty;
            }

            // This assumes tokens are on the same line for simplicity.
            // A more robust implementation would handle multi-line text reconstruction.
            int startCharIndex = start.Column;
            int endCharIndex = end.Column + end.Length;

            // Find the line text
            string[] lines = originalText.Split('\n');
            if (start.Line - 1 < lines.Length)
            {
                string line = lines[start.Line - 1];
                if (endCharIndex <= line.Length)
                {
                    return line.Substring(startCharIndex, endCharIndex - startCharIndex);
                }
            }
            return string.Empty; // Could not reconstruct
        }

        public static bool IsSingleTokenString(string? text)
        {
            if (text == null) return false;
            // A single token string should not contain spaces after trimming.
            return !text.Trim().Contains(" ");
        }
    }
}
