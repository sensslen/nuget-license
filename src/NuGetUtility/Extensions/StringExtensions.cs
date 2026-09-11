// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NuGetUtility.Extensions
{
    public static class StringExtensions
    {
        // Wraps string.IsNullOrEmpty with the null-state annotation the framework method lacks on net472,
        // so callers get null-narrowing after the check instead of needing a null-forgiving operator.
        public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
        {
            return string.IsNullOrEmpty(value);
        }

        /// <summary>Matches case-insensitively, with "*" for any sequence of characters and "?" for any single character.</summary>
        public static bool Like(this string str, string pattern)
        {
            return new Regex(
                $"^{Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".")}$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline,
                TimeSpan.FromMilliseconds(1000)
            ).IsMatch(str);
        }

        /// <summary>Matches the pattern against the full path and against the file name alone, so a bare "*.dll" also matches a nested path.</summary>
        public static bool PathLike(this string path, string pattern)
        {
            if (path.Like(pattern))
            {
                return true;
            }

            // Path.GetFileName only honours the running platform's separator, so a Windows path would not split on Linux.
            int lastSeparatorIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            string fileName = lastSeparatorIndex >= 0 ? path[(lastSeparatorIndex + 1)..] : path;
            return fileName.Like(pattern);
        }
    }
}
