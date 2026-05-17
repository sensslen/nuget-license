// Licensed to the project contributors.
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
namespace FileLicenseMatcher.SPDX.JavaCore;

/**
 * Static helper class for comparing license text
 * 
 * @author Gary O'Neall
 *
 */
public static class LicenseTextHelper
{

    private const string TOKEN_SPLIT_REGEX = "(^|[^\\s.,?'();:\"/\\[\\]<>]{1,100})((\\s|\\.|,|\\?|'|\"|\\(|\\)|;|:|/|\\[|]|<|>|$){1,100})";
    public static readonly Regex TOKEN_SPLIT_PATTERN = new(TOKEN_SPLIT_REGEX, RegexOptions.Compiled);
#pragma warning disable IDE1006
    private static readonly ImmutableHashSet<string> PUNCTUATION = [".", ",", "?", "\"", "'", "(", ")", ";", ":", "/", "[", "]", "<", ">"];
    // most of these are comments for common programming languages (C style, Java, Ruby, Python)
    private static readonly ImmutableHashSet<string> SKIPPABLE_TOKENS = ["//", "/*", "*/", "/**", "#", "##", "*", "**", "\"\"\"", "/", "=begin", "=end"];
    static readonly Regex DASHES_REGEX = new("[\\u2010\\u2011\\u2012\\u2013\\u2014\\u2015\\uFE58\\uFF0D\\-]{1,2}", RegexOptions.Compiled);
    static readonly Regex SPACE_PATTERN = new("[\\u202F\\u2007\\u2060\\u2009]", RegexOptions.Compiled);
    static readonly Regex COMMA_PATTERN = new("[\\uFF0C\\uFE10\\uFE50]");
    static readonly Regex PER_CENT_PATTERN = new("per cent", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex COPYRIGHT_HOLDER_PATTERN = new("copyright holder", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex COPYRIGHT_HOLDERS_PATTERN = new("copyright holders", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex COPYRIGHT_OWNERS_PATTERN = new("copyright owners", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex COPYRIGHT_OWNER_PATTERN = new("copyright owner", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex PER_CENT_PATTERN_LF = new("per\\s{0,100}\\n{1,10}\\s{0,100}cent", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex COPYRIGHT_HOLDERS_PATTERN_LF = new("copyright\\s{0,100}\\n{1,10}\\s{0,100}holders", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex COPYRIGHT_HOLDER_PATTERN_LF = new("copyright\\s{0,100}\\n{1,10}\\s{0,100}holder", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex COPYRIGHT_OWNERS_PATTERN_LF = new("copyright\\s{0,100}\\n{1,10}\\s{0,100}owners", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex COPYRIGHT_OWNER_PATTERN_LF = new("copyright\\s{0,100}\\n{1,10}\\s{0,100}owner", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex COPYRIGHT_SYMBOL_PATTERN = new("\\(c\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
#pragma warning restore IDE1006
    public static readonly IImmutableDictionary<string, string> NORMALIZE_TOKENS = ImmutableDictionary.CreateRange(
        [
        new KeyValuePair<string,string>("&","and"),
        new KeyValuePair<string,string>("acknowledgment","acknowledgement"),
        new KeyValuePair<string,string>("analogue","analog"),
        new KeyValuePair<string,string>("analyse","analyze"),
        new KeyValuePair<string,string>("artefact","artifact"),
        new KeyValuePair<string,string>("authorisation","authorization"),
        new KeyValuePair<string,string>("authorised","authorized"),
        new KeyValuePair<string,string>("calibre","caliber"),
        new KeyValuePair<string,string>("cancelled","canceled"),
        new KeyValuePair<string,string>("capitalisations","capitalizations"),
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
        new KeyValuePair<string,string>("wilful","willful"),
        new KeyValuePair<string,string>("non-commercial","noncommercial"),
        new KeyValuePair<string,string>("copyright-owner", "copyright-holder"),
        new KeyValuePair<string,string>("sublicense", "sub-license"),
        new KeyValuePair<string,string>("non-infringement", "noninfringement"),
        new KeyValuePair<string,string>("(c)", "-c-"),
        new KeyValuePair<string,string>("©", "-c-"),
        new KeyValuePair<string,string>("copyright", "-c-"),
        new KeyValuePair<string,string>("\"", "'"),
        new KeyValuePair<string,string>("merchantability", "merchantability"),
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
    public static IReadOnlyList<string> tokenizeLicenseText(string licenseText, IDictionary<int, LineColumn> tokenToLocation)
    {
        string textToTokenize = normalizeText(replaceMultWord(replaceSpaceComma(licenseText))).ToLower();
        List<string> tokens = [];
        using var reader = new StringReader(textToTokenize);
        try
        {
            int currentLine = 1;
            int currentToken = 0;
            string? line = reader.ReadLine();
            while (line != null)
            {
                line = removeLineSeparators(line);
                MatchCollection lineMatcher = TOKEN_SPLIT_PATTERN.Matches(line);
                foreach (Match match in lineMatcher)
                {
                    string token = match.Groups[1].Value.Trim();
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
                        if (PUNCTUATION.Contains(possiblePunctuation))
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
            foreach (GroupCollection groups in m.Cast<Match>().Select(match => match.Groups))
            {
                string word = groups[1].Value.Trim();
                string separator = groups[2].Value.Trim();
                tokens.Add(word);
                if (PUNCTUATION.Contains(separator))
                {
                    tokens.Add(separator);
                }
            }
        }
        // ignore
        return tokens;
    }

    /**
     * Just fetches the string at the index checking for range.  Returns null if index is out of range.
     * @param tokens array of tokens
     * @param tokenIndex index of token to retrieve
     * @return the token at the index or null if the token does not exist
     */
    public static string? getTokenAt(IReadOnlyList<string> tokens, int tokenIndex)
    {
        if (tokenIndex >= tokens.Count)
        {
            return null;
        }
        else
        {
            return tokens[tokenIndex];
        }
    }

    /**
     * Returns true if the token can be ignored per the rules
     * @param token token to check
     * @return true if the token can be ignored per the rules
     */
    public static bool canSkip(string? token)
    {
        if (token == null)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }
        return SKIPPABLE_TOKENS.Contains(token.Trim().ToLower());
    }

    /**
     * Returns true if the two tokens can be considered equivalent per the SPDX license matching rules
     * @param tokenA token to compare
     * @param tokenB token to compare
     * @return true if tokenA is equivalent to tokenB
     */
    public static bool tokensEquivalent(string? tokenA, string? tokenB)
    {
        if (tokenA == null)
        {
            return tokenB == null;
        }
        else if (tokenB == null)
        {
            return false;
        }
        else
        {
            string s1 = DASHES_REGEX.Replace(tokenA.Trim().ToLower(), "-");
            string s2 = DASHES_REGEX.Replace(tokenB.Trim().ToLower(), "-");
            if (s1.Equals(s2))
            {
                return true;
            }
            else
            {
                // check for equivalent tokens by normalizing the tokens
                if (!NORMALIZE_TOKENS.TryGetValue(s1, out string? ns1))
                {
                    ns1 = s1;
                }
                if (!NORMALIZE_TOKENS.TryGetValue(s2, out string? ns2))
                {
                    ns2 = s2;
                }
                return ns1.Equals(ns2);
            }
        }
    }

    /**
     * Replace different forms of space with a normalized space and different forms of commas with a normalized comma
     * @param s input string
     * @return input string replacing all UTF-8 spaces with " " and all UTF-8 commas with ","
     */
    public static string replaceSpaceComma(string s)
    {
        return COMMA_PATTERN.Replace(SPACE_PATTERN.Replace(s, " "), ",");
    }

    /**
     * replaces all multi-words with a single token using a dash to separate
     * @param s input string
     * @return input string with all multi-words with a single token using a dash to separate
     */
    public static string replaceMultWord(string s)
    {
        string tmp = COPYRIGHT_HOLDERS_PATTERN.Replace(s, "copyright-holders");
        tmp = COPYRIGHT_HOLDERS_PATTERN_LF.Replace(tmp, "copyright-holders\n");
        tmp = COPYRIGHT_OWNERS_PATTERN.Replace(tmp, "copyright-owners");
        tmp = COPYRIGHT_OWNERS_PATTERN_LF.Replace(tmp, "copyright-owners\n");
        tmp = COPYRIGHT_HOLDER_PATTERN.Replace(tmp, "copyright-holder");
        tmp = COPYRIGHT_HOLDER_PATTERN_LF.Replace(tmp, "copyright-holder\n");
        tmp = COPYRIGHT_OWNER_PATTERN.Replace(tmp, "copyright-owner");
        tmp = COPYRIGHT_OWNER_PATTERN_LF.Replace(tmp, "copyright-owner\n");
        tmp = PER_CENT_PATTERN.Replace(tmp, "percent");
        tmp = PER_CENT_PATTERN_LF.Replace(tmp, "percent\n");
        return COPYRIGHT_SYMBOL_PATTERN.Replace(tmp, "-c-");   // replace the parenthesis with a dash so that it results in a single token rather than 3
    }

    /**
     * Normalize quotes and no-break spaces
     * @param s String to normalize
     * @return String normalized for comparison
     */
    private static readonly Regex s_singleQuotePattern = new("[‘’‛‚`]", RegexOptions.Compiled);
    private static readonly Regex s_doubleQuotePattern = new("[“”‟„]", RegexOptions.Compiled);
    private static readonly Regex s_dashPattern = new("[—–]", RegexOptions.Compiled);
    public static string normalizeText(string s)
    {
        // First normalize single quotes, then normalize two single quotes to a double quote, normalize double quotes 
        // then normalize non-breaking spaces to spaces
        string tmp = s_singleQuotePattern.Replace(s, "'");   // Take care of single quotes first
        tmp = tmp.Replace("http://", "https://");         // Normalize the http protocol scheme
        tmp = tmp.Replace("''", "\"");            // This way, we can change double single quotes to a single double quote
        tmp = s_doubleQuotePattern.Replace(tmp, "\"");        // Now we can normalize the double quotes
        tmp = tmp.Replace('\u00A0', ' ');        // replace non-breaking spaces with spaces since Java does not handle the former well
        tmp = s_dashPattern.Replace(tmp, "-");           // replace em dash, en dash with simple dash
        return tmp.Replace('\u2028', '\n');      // replace line separator with newline since Java does not handle the former well
    }

    /**
     * @param s Input string
     * @return s without any line separators (---, ***, ===)
     */
    private static readonly Regex s_removeLineSeparatorsRegex = new("[-=*]{3,}\\s*$", RegexOptions.Compiled);
    public static string removeLineSeparators(string s)
    {
        return s_removeLineSeparatorsRegex.Replace(s, "");  // Remove ----, ***,  and ====
    }
}
