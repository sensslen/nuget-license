// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// SPDX-FileCopyrightText: Copyright (c) 2024 Source Auditor Inc.
/// SPDX-FileType: SOURCE
/// SPDX-License-Identifier: Apache-2.0
/// </summary>
namespace SPDXLicenseMatcher.JavaPort
{
    /// <summary>
    /// Static helper class for comparing license text.
    /// </summary>
    public static class LicenseTextHelper
    {
        private const string TokenSplitRegex = "(^|[^\\s.,?'();:\"/\\[\\]<>]{1,100})((\\s|\\.|,|\\?|'|\"|\\(|\\)|;|:|/|\\[|]|<|>|$){1,100})";
        public static readonly Regex TokenSplitPattern = new Regex(TokenSplitRegex, RegexOptions.Compiled);

        private static readonly ISet<string> s_punctuation = new HashSet<string>
            { ".", ",", "?", "\"", "'", "(", ")", ";", ":", "/", "[", "]", "<", ">" };

        // Most of these are comments for common programming languages (C style, Java, Ruby, Python)
        private static readonly ISet<string> s_skippableTokens = new HashSet<string>
            { "//", "/*", "*/", "/**", "#", "##", "*", "**", "\"\"\"", "/", "=begin", "=end" };

        private const string DashesRegexStr = "[\\u2010\\u2011\\u2012\\u2013\\u2014\\u2015\\uFE58\\uFF0D\\-]{1,2}";
        private static readonly Regex s_dashesPattern = new Regex(DashesRegexStr, RegexOptions.Compiled);
        private static readonly Regex s_spacePattern = new Regex("[\\u202F\\u2007\\u2060\\u2009]", RegexOptions.Compiled);
        private static readonly Regex s_commaPattern = new Regex("[\\uFF0C\\uFE10\\uFE50]", RegexOptions.Compiled);
        private static readonly Regex s_perCentPattern = new Regex("per cent", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_copyrightHolderPattern = new Regex("copyright holder", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_copyrightHoldersPattern = new Regex("copyright holders", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_copyrightOwnersPattern = new Regex("copyright owners", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_copyrightOwnerPattern = new Regex("copyright owner", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_copyrightHoldersPatternLf = new Regex("copyright\\s{0,100}\\n{1,10}\\s{0,100}holders", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_copyrightHolderPatternLf = new Regex("copyright\\s{0,100}\\n{1,10}\\s{0,100}holder", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_copyrightOwnersPatternLf = new Regex("copyright\\s{0,100}\\n{1,10}\\s{0,100}owners", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_copyrightOwnerPatternLf = new Regex("copyright\\s{0,100}\\n{1,10}\\s{0,100}owner", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_copyrightSymbolPattern = new Regex("\\(c\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly IImmutableDictionary<string, string> NormalizeTokens = ImmutableDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("&", "and"),
                new KeyValuePair<string, string>("acknowledgment", "acknowledgement"),
                new KeyValuePair<string, string>("analogue", "analog"),
                new KeyValuePair<string, string>("analyse", "analyze"),
                new KeyValuePair<string, string>("artefact", "artifact"),
                new KeyValuePair<string, string>("authorisation", "authorization"),
                new KeyValuePair<string, string>("authorised", "authorized"),
                new KeyValuePair<string, string>("calibre", "caliber"),
                new KeyValuePair<string, string>("cancelled", "canceled"),
                new KeyValuePair<string, string>("capitalisations", "capitalizations"),
                new KeyValuePair<string, string>("catalogue", "catalog"),
                new KeyValuePair<string, string>("categorise", "categorize"),
                new KeyValuePair<string, string>("centre", "center"),
                new KeyValuePair<string, string>("emphasised", "emphasized"),
                new KeyValuePair<string, string>("favour", "favor"),
                new KeyValuePair<string, string>("favourite", "favorite"),
                new KeyValuePair<string, string>("fulfil", "fulfill"),
                new KeyValuePair<string, string>("fulfilment", "fulfillment"),
                new KeyValuePair<string, string>("initialise", "initialize"),
                new KeyValuePair<string, string>("judgment", "judgement"),
                new KeyValuePair<string, string>("labelling", "labeling"),
                new KeyValuePair<string, string>("labour", "labor"),
                new KeyValuePair<string, string>("licence", "license"),
                new KeyValuePair<string, string>("maximise", "maximize"),
                new KeyValuePair<string, string>("modelled", "modeled"),
                new KeyValuePair<string, string>("modelling", "modeling"),
                new KeyValuePair<string, string>("offence", "offense"),
                new KeyValuePair<string, string>("optimise", "optimize"),
                new KeyValuePair<string, string>("organisation", "organization"),
                new KeyValuePair<string, string>("organise", "organize"),
                new KeyValuePair<string, string>("practise", "practice"),
                new KeyValuePair<string, string>("programme", "program"),
                new KeyValuePair<string, string>("realise", "realize"),
                new KeyValuePair<string, string>("recognise", "recognize"),
                new KeyValuePair<string, string>("signalling", "signaling"),
                new KeyValuePair<string, string>("utilisation", "utilization"),
                new KeyValuePair<string, string>("whilst", "while"),
                new KeyValuePair<string, string>("wilful", "willful"),
                new KeyValuePair<string, string>("non-commercial", "noncommercial"),
                new KeyValuePair<string, string>("copyright-owner", "copyright-holder"),
                new KeyValuePair<string, string>("sublicense", "sub-license"),
                new KeyValuePair<string, string>("non-infringement", "noninfringement"),
                new KeyValuePair<string, string>("(c)", "-c-"),
                new KeyValuePair<string, string>("©", "-c-"),
                new KeyValuePair<string, string>("copyright", "-c-"),
                new KeyValuePair<string, string>("\"", "'"),
                new KeyValuePair<string, string>("merchantability", "merchantability")
            ]);

        /// <summary>
        /// Returns true if two sets of license text are considered a match per the SPDX License matching guidelines.
        /// </summary>
        /// <param name="licenseTextA">Text to compare.</param>
        /// <param name="licenseTextB">Text to compare.</param>
        /// <returns>True if the license text is equivalent.</returns>
        public static bool IsLicenseTextEquivalent(string licenseTextA, string licenseTextB)
        {
            if (licenseTextA == null) return string.IsNullOrEmpty(licenseTextB);
            if (licenseTextB == null) return string.IsNullOrEmpty(licenseTextA);
            if (licenseTextA == licenseTextB) return true;

            var tokenToLocationA = new Dictionary<int, LineColumn>();
            var tokenToLocationB = new Dictionary<int, LineColumn>();
            string[] licenseATokens = TokenizeLicenseText(licenseTextA, tokenToLocationA);
            string[] licenseBTokens = TokenizeLicenseText(licenseTextB, tokenToLocationB);

            int bTokenCounter = 0;
            int aTokenCounter = 0;
            string? nextAToken = GetTokenAt(licenseATokens, aTokenCounter++);
            string? nextBToken = GetTokenAt(licenseBTokens, bTokenCounter++);

            while (nextAToken != null)
            {
                if (nextBToken == null)
                {
                    while (CanSkip(nextAToken))
                    {
                        nextAToken = GetTokenAt(licenseATokens, aTokenCounter++);
                    }
                    if (nextAToken != null) return false;
                }
                else if (TokensEquivalent(nextAToken, nextBToken))
                {
                    nextAToken = GetTokenAt(licenseATokens, aTokenCounter++);
                    nextBToken = GetTokenAt(licenseBTokens, bTokenCounter++);
                }
                else
                {
                    while (CanSkip(nextBToken))
                    {
                        nextBToken = GetTokenAt(licenseBTokens, bTokenCounter++);
                    }
                    while (CanSkip(nextAToken))
                    {
                        nextAToken = GetTokenAt(licenseATokens, aTokenCounter++);
                    }

                    if (!TokensEquivalent(nextAToken, nextBToken)) return false;

                    nextAToken = GetTokenAt(licenseATokens, aTokenCounter++);
                    nextBToken = GetTokenAt(licenseBTokens, bTokenCounter++);
                }
            }

            while (CanSkip(nextBToken))
            {
                nextBToken = GetTokenAt(licenseBTokens, bTokenCounter++);
            }

            return nextBToken == null;
        }

        /// <summary>
        /// Tokenizes the license text, normalizes quotes, lowercases, and converts multi-words.
        /// </summary>
        /// <param name="licenseText">Text to tokenize.</param>
        /// <param name="tokenToLocation">A dictionary to store the location of each token.</param>
        /// <returns>An array of tokens from the license text.</returns>
        public static string[] TokenizeLicenseText(string licenseText, Dictionary<int, LineColumn> tokenToLocation)
        {
            string textToTokenize = NormalizeText(ReplaceMultiWord(ReplaceSpaceComma(licenseText))).ToLower();
            var tokens = new List<string>();

            try
            {
                using (var reader = new StringReader(textToTokenize))
                {
                    int currentLineNum = 1;
                    int currentTokenIndex = 0;
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = RemoveLineSeparators(line);
                        foreach (Match match in TokenSplitPattern.Matches(line))
                        {
                            string token = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(token))
                            {
                                tokens.Add(token);
                                tokenToLocation[currentTokenIndex] = new LineColumn(currentLineNum, match.Index, token.Length);
                                currentTokenIndex++;
                            }

                            string fullMatch = match.Value;
                            for (int i = match.Groups[1].Length; i < fullMatch.Length; i++)
                            {
                                string possiblePunctuation = fullMatch.Substring(i, 1);
                                if (s_punctuation.Contains(possiblePunctuation))
                                {
                                    tokens.Add(possiblePunctuation);
                                    tokenToLocation[currentTokenIndex] = new LineColumn(currentLineNum, match.Index + i, 1);
                                    currentTokenIndex++;
                                }
                            }
                        }
                        currentLineNum++;
                    }
                }
            }
            catch (IOException)
            {
                // Fallback for unexpected IO errors with StringReader
                foreach (Match m in TokenSplitPattern.Matches(textToTokenize))
                {
                    string word = m.Groups[1].Value.Trim();
                    string separator = m.Groups[2].Value.Trim();
                    tokens.Add(word);
                    if (s_punctuation.Contains(separator))
                    {
                        tokens.Add(separator);
                    }
                }
            }
            return tokens.ToArray();
        }

        /// <summary>
        /// Safely retrieves the string at an index, returning null if the index is out of range.
        /// </summary>
        /// <param name="tokens">Array of tokens.</param>
        /// <param name="tokenIndex">Index of the token to retrieve.</param>
        /// <returns>The token at the index or null if it does not exist.</returns>
        public static string? GetTokenAt(string[] tokens, int tokenIndex)
        {
            return tokenIndex >= tokens.Length ? null : tokens[tokenIndex];
        }

        /// <summary>
        /// Returns true if the token can be ignored per the matching rules.
        /// </summary>
        /// <param name="token">Token to check.</param>
        /// <returns>True if the token can be ignored.</returns>
        public static bool CanSkip(string? token)
        {
            if (IsNullOrWhiteSpace(token)) return true;
            return s_skippableTokens.Contains(token.Trim().ToLower());
        }

        /// <summary>
        /// Returns true if two tokens are equivalent per the SPDX license matching rules.
        /// </summary>
        /// <param name="tokenA">Token to compare.</param>
        /// <param name="tokenB">Token to compare.</param>
        /// <returns>True if tokens are equivalent.</returns>
        public static bool TokensEquivalent(string? tokenA, string? tokenB)
        {
            if (tokenA == null) return tokenB == null;
            if (tokenB == null) return false;

            string s1 = s_dashesPattern.Replace(tokenA.Trim().ToLower(), "-");
            string s2 = s_dashesPattern.Replace(tokenB.Trim().ToLower(), "-");

            if (s1 == s2) return true;

            string ns1 = NormalizeTokens.TryGetValue(s1, out string? val1) ? val1 : s1;
            string ns2 = NormalizeTokens.TryGetValue(s2, out string? val2) ? val2 : s2;

            return ns1 == ns2;
        }

        /// <summary>
        /// Replaces different forms of spaces and commas with normalized ASCII equivalents.
        /// </summary>
        /// <param name="s">Input string.</param>
        /// <returns>The normalized string.</returns>
        public static string ReplaceSpaceComma(string s)
        {
            string spaced = s_spacePattern.Replace(s, " ");
            return s_commaPattern.Replace(spaced, ",");
        }

        /// <summary>
        /// Replaces multi-word phrases with single, hyphenated tokens.
        /// </summary>
        /// <param name="s">Input string.</param>
        /// <returns>The modified string.</returns>
        public static string ReplaceMultiWord(string s)
        {
            string retval = s_copyrightHoldersPattern.Replace(s, "copyright-holders");
            retval = s_copyrightHoldersPatternLf.Replace(retval, "copyright-holders\n");
            retval = s_copyrightOwnersPattern.Replace(retval, "copyright-owners");
            retval = s_copyrightOwnersPatternLf.Replace(retval, "copyright-owners\n");
            retval = s_copyrightHolderPattern.Replace(retval, "copyright-holder");
            retval = s_copyrightHolderPatternLf.Replace(retval, "copyright-holder\n");
            retval = s_copyrightOwnerPattern.Replace(retval, "copyright-owner");
            retval = s_copyrightOwnerPatternLf.Replace(retval, "copyright-owner\n");
            retval = s_perCentPattern.Replace(retval, "percent");
            // NOTE: The second "per cent" replacement in Java seemed redundant and is omitted.
            retval = s_copyrightSymbolPattern.Replace(retval, "-c-");
            return retval;
        }

        /// <summary>
        /// Normalizes quotes, protocols, and special space/dash characters.
        /// </summary>
        /// <param name="s">String to normalize.</param>
        /// <returns>The normalized string.</returns>
        public static string NormalizeText(string s)
        {
            return s.Replace("http://", "https://")
                    .Replace('‘', '\'').Replace('’', '\'').Replace('‚', '\'').Replace('`', '\'').Replace('‛', '\'')
                    .Replace("''", "\"")
                    .Replace('“', '"').Replace('”', '"').Replace('„', '"').Replace('‟', '"')
                    .Replace('\u00A0', ' ') // non-breaking space
                    .Replace('—', '-').Replace('–', '-') // em dash, en dash
                    .Replace('\u2028', '\n'); // line separator
        }

        /// <summary>
        /// Removes decorative line separators (e.g., "----", "***") from the end of a string.
        /// </summary>
        /// <param name="s">Input string.</param>
        /// <returns>The string without line separators.</returns>
        public static string RemoveLineSeparators(string s)
        {
            return Regex.Replace(s, "[-=*]{3,}\\s*$", "");
        }

        private static bool IsNullOrWhiteSpace([NotNullWhen(false)] string? s) => string.IsNullOrWhiteSpace(s);
    }
}
