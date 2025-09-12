// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/**
 * SPDX-FileCopyrightText: Copyright (c) 2024 Source Auditor Inc.
 * SPDX-FileType: SOURCE
 * SPDX-License-Identifier: Apache-2.0
 */
namespace SPDXLicenseMatcher.JavaPort
{
    /**
     * Static helper class for comparing license text
     * 
     * @author Gary O'Neall
     *
     */
    public static class LicenseTextHelper
    {

        private const string TOKEN_SPLIT_REGEX = "(^|[^\\s.,?'();:\"/\\[\\]<>]{1,100})((\\s|\\.|,|\\?|'|\"|\\(|\\)|;|:|/|\\[|]|<|>|$){1,100})";
        public static readonly Regex TOKEN_SPLIT_PATTERN = new Regex(TOKEN_SPLIT_REGEX, RegexOptions.Compiled);
        private static readonly IImmutableSet<string> s_pUNCTUATION = ImmutableHashSet.Create(".", ",", "?", "\"", "'", "(", ")", ";", ":", "/", "[", "]", "<", ">");
        // most of these are comments for common programming languages (C style, Java, Ruby, Python)
        private static readonly IImmutableSet<string> s_sKIPPABLE_TOKENS = ImmutableHashSet.Create("//", "/*", "*/", "/**", "#", "##", "*", "**", "\"\"\"", "/", "=begin", "=end");
        private static readonly string s_dASHES_REGEX = "[\\u2010\\u2011\\u2012\\u2013\\u2014\\u2015\\uFE58\\uFF0D\\-]{1,2}";
        private static readonly Regex s_sPACE_PATTERN = new Regex("[\\u202F\\u2007\\u2060\\u2009]", RegexOptions.Compiled);
        private static readonly Regex s_cOMMA_PATTERN = new Regex("[\\uFF0C\\uFE10\\uFE50]", RegexOptions.Compiled);
        private static readonly Regex s_pER_CENT_PATTERN = new Regex("per cent", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_cOPYRIGHT_HOLDER_PATTERN = new Regex("copyright holder", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_cOPYRIGHT_HOLDERS_PATTERN = new Regex("copyright holders", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_cOPYRIGHT_OWNERS_PATTERN = new Regex("copyright owners", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_cOPYRIGHT_OWNER_PATTERN = new Regex("copyright owner", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_cOPYRIGHT_HOLDERS_PATTERN_LF = new Regex("copyright\\s{0,100}\\n{1,10}\\s{0,100}holders", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_cOPYRIGHT_HOLDER_PATTERN_LF = new Regex("copyright\\s{0,100}\\n{1,10}\\s{0,100}holder", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_cOPYRIGHT_OWNERS_PATTERN_LF = new Regex("copyright\\s{0,100}\\n{1,10}\\s{0,100}owners", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_cOPYRIGHT_OWNER_PATTERN_LF = new Regex("copyright\\s{0,100}\\n{1,10}\\s{0,100}owner", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_cOPYRIGHT_SYMBOL_PATTERN = new Regex("\\(c\\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly ImmutableDictionary<string, string> NORMALIZE_TOKENS = ImmutableDictionary.CreateRange(
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
            ]
        );

        /**
         * Tokenizes the license text, normalizes quotes, lowercases and converts
         * multi-words for better equiv. comparisons
         * 
         * @param tokenToLocation location for all of the tokens
         * @param licenseText text to tokenize
         * @return tokens array of tokens from the licenseText
         */
        public static string[] TokenizeLicenseText(string licenseText, Dictionary<int, LineColumn> tokenToLocation)
        {
            string textToTokenize = NormalizeText(ReplaceMultWord(ReplaceSpaceComma(licenseText))).ToLower();
            List<string> tokens = new();
            using var reader = new StringReader(textToTokenize);
            try
            {
                int currentLine = 1;
                int currentToken = 0;
                string? line = reader.ReadLine();
                while (line != null)
                {
                    line = RemoveLineSeparators(line);
                    MatchCollection lineMatcher = TOKEN_SPLIT_PATTERN.Matches(line);
                    foreach (Match match in lineMatcher)
                    {
                        string token = match.Groups[1].ToString().Trim();
                        if (!string.IsNullOrEmpty(token))
                        {
                            tokens.Add(token);
                            tokenToLocation[currentToken] = new LineColumn(currentLine, match.Index, token.Length);
                            currentToken++;
                        }
                        string fullMatch = match.Groups[0].ToString();
                        for (int i = match.Groups[1].Length; i < fullMatch.Length; i++)
                        {
                            string possiblePunctuation = fullMatch.Substring(i, 1);
                            if (s_pUNCTUATION.Contains(possiblePunctuation))
                            {
                                tokens.Add(possiblePunctuation);
                                tokenToLocation[currentToken] = new LineColumn(currentLine, match.Index + i, 1);
                                currentToken++;
                            }
                        }
                    }
                    currentLine++;
                    line = reader.ReadLine();
                }
            }
            catch (IOException)
            {
                // Don't fill in the lines, take a simpler approach
                MatchCollection m = TOKEN_SPLIT_PATTERN.Matches(textToTokenize);
                foreach (GroupCollection groups in m.Cast<Match>().Select(m => m.Groups))
                {
                    string word = groups[1].ToString().Trim();
                    string separator = groups[2].ToString().Trim();
                    tokens.Add(word);
                    if (s_pUNCTUATION.Contains(separator))
                    {
                        tokens.Add(separator);
                    }
                }
            }
            // ignore
            return tokens.ToArray();
        }

        /**
         * Returns true if the token can be ignored per the rules
         * @param token token to check
         * @return true if the token can be ignored per the rules
         */
        public static bool CanSkip(string? token)
        {
            if (token == null)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(token))
            {
                return true;
            }
            return s_sKIPPABLE_TOKENS.Contains(token.Trim().ToLower());
        }

        /**
         * Replace different forms of space with a normalized space and different forms of commas with a normalized comma
         * @param s input string
         * @return input string replacing all UTF-8 spaces with " " and all UTF-8 commas with ","
         */
        public static string ReplaceSpaceComma(string s)
        {
            return s_cOMMA_PATTERN.Replace(s_sPACE_PATTERN.Replace(s, " "), ",");
        }

        /**
         * replaces all multi-words with a single token using a dash to separate
         * @param s input string
         * @return input string with all multi-words with a single token using a dash to separate
         */
        public static string ReplaceMultWord(string s)
        {
            string retval = s_cOPYRIGHT_HOLDERS_PATTERN.Replace(s, "copyright-holders");
            retval = s_cOPYRIGHT_HOLDERS_PATTERN_LF.Replace(retval, "copyright-holders\n");
            retval = s_cOPYRIGHT_OWNERS_PATTERN.Replace(retval, "copyright-owners");
            retval = s_cOPYRIGHT_OWNERS_PATTERN_LF.Replace(retval, "copyright-owners\n");
            retval = s_cOPYRIGHT_HOLDER_PATTERN.Replace(retval, "copyright-holder");
            retval = s_cOPYRIGHT_HOLDER_PATTERN_LF.Replace(retval, "copyright-holder\n");
            retval = s_cOPYRIGHT_OWNER_PATTERN.Replace(retval, "copyright-owner");
            retval = s_cOPYRIGHT_OWNER_PATTERN_LF.Replace(retval, "copyright-owner\n");
            retval = s_pER_CENT_PATTERN.Replace(retval, "percent");
            retval = s_pER_CENT_PATTERN.Replace(retval, "percent\n");
            retval = s_cOPYRIGHT_SYMBOL_PATTERN.Replace(retval, "-c-");// replace the parenthesis with a dash so that it results in a single token rather than 3
            return retval;
        }

        /**
         * Normalize quotes and no-break spaces
         * @param s String to normalize
         * @return String normalized for comparison
         */
        public static string NormalizeText(string s)
        {
            // First normalize single quotes, then normalize two single quotes to a double quote, normalize double quotes 
            // then normalize non-breaking spaces to spaces
            string result = Regex.Replace(s, "[‘’‛‚`]", "'");       // Take care of single quotes first
            result = Regex.Replace(result, "^http://", "https://"); // Normalize the http protocol scheme
            result = Regex.Replace(result, "^''", "\"");            // This way, we can change double single quotes to a single double quote
            result = Regex.Replace(result, "[“”‟„]", "\"");         // Now we can normalize the double quotes
            result = Regex.Replace(result, "\\u00A0", " ");         // replace non-breaking spaces with spaces since Java does not handle the former well
            result = Regex.Replace(result, @"[\u2014\u2013]", "-"); // replace em dash, en dash with simple dash
            return Regex.Replace(result, "\\u2028", "\n");          // replace line separator with newline since Java does not handle the former well
        }

        /**
         * @param s Input string
         * @return s without any line separators (---, ***, ===)
         */
        public static string RemoveLineSeparators(string s)
        {
            return Regex.Replace(s, "^\\s*[-=*]{3,}\\s*$", "", RegexOptions.Multiline);  // Remove ----, ***,  and ====
        }
    }
}
