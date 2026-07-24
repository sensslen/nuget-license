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

        /// <summary>
        /// Compares the string against a given pattern.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="pattern">The pattern to match, where "*" means any sequence of characters, and "?" means any single character.</param>
        /// <returns><c>true</c> if the string matches the given pattern; otherwise <c>false</c>.</returns>
        public static bool Like(this string str, string pattern)
        {
            return new Regex(
                $"^{Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".")}$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline,
                TimeSpan.FromMilliseconds(1000)
            ).IsMatch(str);
        }

        /// <summary>
        /// Compares a path string against a given pattern, matching against both the full path and just the filename.
        /// </summary>
        /// <param name="path">The path string to match.</param>
        /// <param name="pattern">The pattern to match, where "*" means any sequence of characters, and "?" means any single character.</param>
        /// <returns><c>true</c> if either the full path or the filename matches the given pattern; otherwise <c>false</c>.</returns>
        public static bool PathLike(this string path, string pattern)
        {
            if (path.Like(pattern))
            {
                return true;
            }

            // Extract the filename in a cross-platform way by finding the last path separator
            int lastSeparatorIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            string fileName = lastSeparatorIndex >= 0 ? path[(lastSeparatorIndex + 1)..] : path;
            return fileName.Like(pattern);
        }
    }
}
