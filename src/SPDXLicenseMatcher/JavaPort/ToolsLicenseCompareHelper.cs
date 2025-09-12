// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root


using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/**
 * Copyright (c) 2013 Source Auditor Inc.
 * Copyright (c) 2013 Black Duck Software Inc.
 *
 *   Licensed under the Apache License, Version 2.0 (the "License");
 *   you may not use this file except in compliance with the License.
 *   You may obtain a copy of the License at
 *
 *       http://www.apache.org/licenses/LICENSE-2.0
 *
 *   Unless required by applicable law or agreed to in writing, software
 *   distributed under the License is distributed on an "AS IS" BASIS,
 *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *   See the License for the specific language governing permissions and
 *   limitations under the License.
 *
*/

namespace SPDXLicenseMatcher.JavaPort;

/**
 * Primarily a static class of helper functions for comparing two SPDX licenses
 * @author Gary O'Neall
 *
 */
public static class ToolsLicenseCompareHelper
{

    private const string TOKEN_SPLIT_REGEX = "(^|[^\\s\\.,?'();:\"/]+)((\\s|\\.|,|\\?|'|\"|\\(|\\)|;|:|/|$)+)";
    private static readonly Regex s_tOKEN_SPLIT_PATTERN = new Regex(TOKEN_SPLIT_REGEX, RegexOptions.Compiled);

    private static readonly ImmutableHashSet<string> s_pUNCTUATION = [".", ",", "?", "\"", "'", "(", ")", ";", ":", "/"];

    // most of these are comments for common programming languages (C style, Java, Ruby, Python)
    private static readonly ImmutableHashSet<string> s_sKIPPABLE_TOKENS = ["//", "/*", "*/", "/**", "#", "##", "*", "**", "\"\"\"", "/", "=begin", "=end"];

    private static readonly ImmutableDictionary<string, string> s_nORMALIZE_TOKENS = ImmutableDictionary.CreateRange(
        [
                new KeyValuePair<string,string>("acknowledgment","acknowledgement"),
                new KeyValuePair<string,string>("analogue","analog"),
                new KeyValuePair<string,string>("analyse","analyze"),
                new KeyValuePair<string,string>("artefact","artifact"),
                new KeyValuePair<string,string>("authorisation","authorization"),
                new KeyValuePair<string,string>("authorised","authorized"),
                new KeyValuePair<string,string>("calibre","caliber"),
                new KeyValuePair<string,string>("cancelled","canceled"),
                new KeyValuePair<string,string>("apitalisations","apitalizations"),
                new KeyValuePair<string,string>("catalogue","catalog"),
                new KeyValuePair<string,string>("categorise","categorize"),
                new KeyValuePair<string,string>("centre","center"),
                new KeyValuePair<string,string>("emphasised","emphasized"),
                new KeyValuePair<string,string>("favour","favor"),
                new KeyValuePair<string,string>("favourite","favorite"),
                new KeyValuePair<string,string>("fulfil","fulfill"),
                new KeyValuePair<string,string>("fulfilment","fulfillment"),
                new KeyValuePair<string,string>("initialise","initialize"),
                new KeyValuePair<string,string>("judgment","judgement"),
                new KeyValuePair<string,string>("labelling","labeling"),
                new KeyValuePair<string,string>("labour","labor"),
                new KeyValuePair<string,string>("licence","license"),
                new KeyValuePair<string,string>("maximise","maximize"),
                new KeyValuePair<string,string>("modelled","modeled"),
                new KeyValuePair<string,string>("modelling","modeling"),
                new KeyValuePair<string,string>("offence","offense"),
                new KeyValuePair<string,string>("optimise","optimize"),
                new KeyValuePair<string,string>("organisation","organization"),
                new KeyValuePair<string,string>("organise","organize"),
                new KeyValuePair<string,string>("practise","practice"),
                new KeyValuePair<string,string>("programme","program"),
                new KeyValuePair<string,string>("realise","realize"),
                new KeyValuePair<string,string>("recognise","recognize"),
                new KeyValuePair<string,string>("signalling","signaling"),
                new KeyValuePair<string,string>("utilisation","utilization"),
                new KeyValuePair<string,string>("whilst","while"),
                new KeyValuePair<string,string>("wilful","wilfull"),
                new KeyValuePair<string,string>("non-commercial","noncommercial"),
                new KeyValuePair<string,string>("copyright-owner", "copyright-holder"),
                new KeyValuePair<string,string>("sublicense", "sub-license"),
                new KeyValuePair<string,string>("non-infringement", "noninfringement"),
                new KeyValuePair<string,string>("(c)", "-c-"),
                new KeyValuePair<string,string>("copyright", "-c-"),
                new KeyValuePair<string,string>("©", "-c-"),
                new KeyValuePair<string,string>("\"", "'")
        ]);


    private const string DASHES_REGEX = "[\\u2012\\u2013\\u2014\\u2015]";
    private static readonly Regex s_sPACE_PATTERN = new Regex("[\\u202F\\u2007\\u2060]", RegexOptions.Compiled);
    private static readonly Regex s_pER_CENT_PATTERN = new Regex("per cent", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_cOPYRIGHT_HOLDER_PATTERN = new Regex("copyright holder", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_cOPYRIGHT_HOLDERS_PATTERN = new Regex("copyright holders", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_cOPYRIGHT_OWNERS_PATTERN = new Regex("copyright owners", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_cOPYRIGHT_OWNER_PATTERN = new Regex("copyright owner", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_cOPYRIGHT_HOLDERS_PATTERN_LF = new Regex("copyright\\s*\\n+\\s*holders", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_cOPYRIGHT_HOLDER_PATTERN_LF = new Regex("copyright\\s*\\n+\\s*holder", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_cOPYRIGHT_OWNERS_PATTERN_LF = new Regex("copyright\\s*\\n+\\s*owners", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_cOPYRIGHT_OWNER_PATTERN_LF = new Regex("copyright\\s*\\n+\\s*owner", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_cOPYRIGHT_SYMBOL_PATTERN = new Regex("\\(c\\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /**
     * Returns true if two sets of license text is considered a match per
     * the SPDX License matching guidelines documented at spdx.org (currently http://spdx.org/wiki/spdx-license-list-match-guidelines)
     * There are 2 unimplemented features - bullets/numbering is not considered and comments with no whitespace between text is not skipped
     * @param licenseTextA
     * @param licenseTextB
     * @return
     */
    public static bool IsLicenseTextEquivalent(string? licenseTextA, string? licenseTextB)
    {
        // Need to take care of multi-word equivalent words - convert to single words with hypens

        // tokenize each of the strings
        if (licenseTextA == null)
        {
            return string.IsNullOrEmpty(licenseTextB);
        }
        if (licenseTextB == null)
        {
            return string.IsNullOrEmpty(licenseTextA);
        }
        if (licenseTextA.Equals(licenseTextB))
        {
            return true;
        }
        Dictionary<int, LineColumn> tokenToLocationA = new Dictionary<int, LineColumn>();
        Dictionary<int, LineColumn> tokenToLocationB = new Dictionary<int, LineColumn>();
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
                // end of b stream
                while (nextAToken != null && CanSkip(nextAToken))
                {
                    nextAToken = GetTokenAt(licenseATokens, aTokenCounter++);
                }
                if (nextAToken != null)
                {
                    return false;   // there is more stuff in the license text B, so not equal
                }
            }
            else if (TokensEquivalent(nextAToken, nextBToken))
            {
                // just move onto the next set of tokens
                nextAToken = GetTokenAt(licenseATokens, aTokenCounter++);
                nextBToken = GetTokenAt(licenseBTokens, bTokenCounter++);
            }
            else
            {
                // see if we can skip through some B tokens to find a match
                while (nextBToken != null && CanSkip(nextBToken))
                {
                    nextBToken = GetTokenAt(licenseBTokens, bTokenCounter++);
                }
                // just to be sure, skip forward on the A license
                while (nextAToken != null && CanSkip(nextAToken))
                {
                    nextAToken = GetTokenAt(licenseATokens, aTokenCounter++);
                }
                if (!TokensEquivalent(nextAToken, nextBToken))
                {
                    return false;
                }
                else
                {
                    nextAToken = GetTokenAt(licenseATokens, aTokenCounter++);
                    nextBToken = GetTokenAt(licenseBTokens, bTokenCounter++);
                }
            }
        }
        // need to make sure B is at the end
        while (nextBToken != null && CanSkip(nextBToken))
        {
            nextBToken = GetTokenAt(licenseBTokens, bTokenCounter++);
        }
        return (nextBToken == null);
    }

    /**
     * Normalize quotes and no-break spaces
     * @param s
     * @return
     */
    public static string NormalizeText(string s)
    {
        // First normalize single quotes, then normalize two single quotes to a double quote, normalize double quotes
        // then normalize non-breaking spaces to spaces
        string result = Regex.Replace(s, "‘|’|‛|‚|`", "'");       // Take care of single quotes first
        result = Regex.Replace(result, "http://", "https://");    // Normalize the http protocol scheme
        result = Regex.Replace(result, "''", "\"");               // This way, we can change doulbe single quotes to a single double cquote
        result = Regex.Replace(result, "“|”|‟|„", "\"");          // Now we can normalize the double quotes
        result = Regex.Replace(result, "\\u00A0", " ");           // replace non-breaking spaces with spaces since Java does not handle the former well
        result = Regex.Replace(result, "—|–", "-");               // replace em dash, en dash with simple dash
        return Regex.Replace(result, "\\u2028", "\n");            // replace line separator with newline since Java does not handle the former well
    }

    /**
     * Tokenizes the license text, normalizes quotes, lowercases and converts multi-words for better equiv. comparisons
     * @param tokenLocations location for all of the tokens
     * @param licenseText
     * @return
     * @throws IOException
     */
    public static string[] TokenizeLicenseText(string licenseText, Dictionary<int, LineColumn> tokenToLocation)
    {
        string textToTokenize = NormalizeText(ReplaceMultWord(ReplaceSpace(licenseText))).ToLower();
        List<string> tokens = [];
        using var reader = new StringReader(textToTokenize);
        try
        {
            int currentLine = 1;
            int currentToken = 0;
            string? line = reader.ReadLine();
            while (line != null)
            {
                MatchCollection lineMatcher = s_tOKEN_SPLIT_PATTERN.Matches(line);
                foreach (Match match in lineMatcher)
                {
                    string? token = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(token))
                    {
                        tokens.Add(token);
                        tokenToLocation[currentToken] = new LineColumn(currentLine, match.Index, token.Length);
                        currentToken++;
                    }
                    string fullMatch = match.Groups[0].Value;
                    for (int i = match.Groups[1].Value.Length; i < fullMatch.Length; i++)
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
            MatchCollection m = s_tOKEN_SPLIT_PATTERN.Matches(textToTokenize);
            foreach (GroupCollection groups in m.Cast<Match>().Select(m => m.Groups))
            {
                string word = groups[1].Value.Trim();
                string seperator = groups[2].Value.Trim();
                tokens.Add(word);
                if (s_pUNCTUATION.Contains(seperator))
                {
                    tokens.Add(seperator);
                }
            }
        }
        return tokens.ToArray();
    }

    /**
     * @param text
     * @return the first token in the license text
     */
    public static string? GetFirstLicenseToken(string text)
    {
        string textToTokenize = NormalizeText(ReplaceMultWord(ReplaceSpace(text))).ToLower();
        MatchCollection m = s_tOKEN_SPLIT_PATTERN.Matches(textToTokenize);
        return m.Cast<Match>().Select(m => m.Groups[1].Value.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));
    }

    /**
     * @param text
     * @return true if the text contains a single token
     */
    public static bool IsSingleTokenString(string text)
    {
        if (text.Contains("\n"))
        {
            return false;
        }
        MatchCollection m = s_tOKEN_SPLIT_PATTERN.Matches(text);
        bool found = false;
        foreach (string? _ in m.Cast<Match>().Select(m => m.Groups[1].Value).Where(m => !string.IsNullOrWhiteSpace(m)))
        {
            if (found)
            {
                return false;
            }
            else
            {
                found = true;
            }
        }
        return true;
    }

    /**
     * Replace different forms of space with a normalized space
     * @param s
     * @return
     */
    private static string ReplaceSpace(string s)
    {
        return s_sPACE_PATTERN.Replace(s, " ");
    }

    /**
     * replaces all mult-words with a single token using a dash to separate
     * @param s
     * @return
     */
    private static string ReplaceMultWord(string s)
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
        return s_cOPYRIGHT_SYMBOL_PATTERN.Replace(retval, "-c-");
    }

    /**
     * Just fetches the string at the index checking for range.  Returns null if index is out of range.
     * @param tokens
     * @param tokenIndex
     * @return
     */
    public static string? GetTokenAt(string[] tokens, int tokenIndex)
    {
        if (tokenIndex >= tokens.Length)
        {
            return null;
        }
        else
        {
            return tokens[tokenIndex];
        }
    }
    /**
     * Returns true if the two tokens can be considered equlivalent per the SPDX license matching rules
     * @param tokenA
     * @param tokenB
     * @return
     */
    public static bool TokensEquivalent(string? tokenA, string? tokenB)
    {
        if (tokenA == null)
        {
            if (tokenB == null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else if (tokenB == null)
        {
            return false;
        }
        else
        {
            string s1 = Regex.Replace(tokenA.Trim().ToLower(), DASHES_REGEX, "-");
            string s2 = Regex.Replace(tokenB.Trim().ToLower(), DASHES_REGEX, "-");
            if (s1.Equals(s2))
            {
                return true;
            }
            else
            {
                // check for equivalent tokens by normalizing the tokens
                if (!s_nORMALIZE_TOKENS.TryGetValue(s1, out string? ns1))
                {
                    ns1 = s1;
                }
                if (!s_nORMALIZE_TOKENS.TryGetValue(s2, out string? ns2))
                {
                    ns2 = s2;
                }
                return ns1.Equals(ns2);
            }
        }
    }
    /**
     * Returns true if the token can be ignored per the rules
     * @param token
     * @return
     */
    public static bool CanSkip(string token)
    {
        if (token == null)
        {
            return false;
        }
        if (string.IsNullOrEmpty(token.Trim()))
        {
            return true;
        }
        return s_sKIPPABLE_TOKENS.Contains(token.Trim().ToLower());
    }
}
