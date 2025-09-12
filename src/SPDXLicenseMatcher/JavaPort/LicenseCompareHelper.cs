// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SPDXLicenseMatcher.JavaPort
{
    public class LicenseCompareHelper
    {
        protected const int CROSS_REF_NUM_WORDS_MATCH = 80;

        protected static readonly Regex REGEX_QUANTIFIER_PATTERN = new Regex(".*\\.\\{(\\d+),(\\d+)}$", RegexOptions.Compiled);
        private const string START_COMMENT_CHAR_PATTERN = "(//|/\\*|\\*|#|' |REM |<!--|--|;|\\(\\*|\\{-)|\\.\\\\\"";

        private static readonly Regex END_COMMENT_PATTERN = new Regex("(\\*/|-->|-}|\\*\\)|\\s\\*)\\s*$", RegexOptions.Compiled);
        private static readonly Regex START_COMMENT_PATTERN = new Regex("^\\s*" + START_COMMENT_CHAR_PATTERN, RegexOptions.Compiled);
        private static readonly Regex BEGIN_OPTIONAL_COMMENT_PATTERN = new Regex("^\\s*<<beginOptional>>\\s*" + START_COMMENT_CHAR_PATTERN, RegexOptions.Compiled);

        /**
         * Remove common comment characters from either a template or license text
         * strings
         *
         * @param s string source
         * @return string without comment characters
         */
        public static string RemoveCommentChars(string? s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            using var reader = new StringReader(s);
            try
            {
                string? line = reader.ReadLine();
                bool firstLine = true;
                while (line != null)
                {
                    line = END_COMMENT_PATTERN.Replace(line, string.Empty);
                    line = START_COMMENT_PATTERN.Replace(line, string.Empty);
                    line = BEGIN_OPTIONAL_COMMENT_PATTERN.Replace(line, "<<beginOptional>>");

                    if (!firstLine)
                    {
                        sb.Append("\n");
                    }
                    else
                    {
                        firstLine = false;
                    }
                    sb.Append(line);
                    line = reader.ReadLine();
                }
            }
            catch (IOException)
            {
                return s!;
            }
            return sb.ToString();
        }

        /**
         * Locate the original text starting with the start token and ending with the
         * end token
         *
         * @param fullLicenseText entire license text
         * @param startToken      starting token
         * @param endToken        ending token
         * @param tokenToLocation token location
         * @return original text starting with the start token and ending with the end
         *         token
         */
        public static string LocateOriginalText(string fullLicenseText, int startToken, int endToken,
                Dictionary<int, LineColumn> tokenToLocation, string[] tokens)
        {
            if (startToken > endToken)
            {
                return "";
            }
            LineColumn start = tokenToLocation[startToken];
            if (start == null)
            {
                return "";
            }
            LineColumn end = tokenToLocation[endToken];
            // If end == null, then we read to the end
            using var reader = new StringReader(fullLicenseText);
            try
            {
                int currentLine = 1;
                string? line = reader.ReadLine();
                while (line != null && currentLine < start.Line)
                {
                    currentLine++;
                    line = reader.ReadLine();
                }
                if (line == null)
                {
                    return "";
                }
                if (end == null)
                {
                    // read until the end of the stream
                    StringBuilder sb = new StringBuilder(line.Substring(start.Column));
                    currentLine++;
                    line = reader.ReadLine();
                    while (line != null)
                    {
                        sb.Append(line);
                        currentLine++;
                        line = reader.ReadLine();
                    }
                    return sb.ToString();
                }
                else if (end.Line == currentLine)
                {
                    return line.Substring(start.Column, end.Column + end.Length - start.Column);
                }
                else
                {
                    StringBuilder sb = new StringBuilder(line.Substring(start.Column));
                    currentLine++;
                    line = reader.ReadLine();
                    while (line != null && currentLine < end.Line)
                    {
                        sb.Append("\n");
                        sb.Append(line);
                        currentLine++;
                        line = reader.ReadLine();
                    }
                    if (line != null && end.Column + end.Length > 0)
                    {
                        sb.Append("\n");
                        sb.Append(line, 0, end.Column + end.Length);
                    }
                    return sb.ToString();
                }
            }
            catch (Exception)
            {
                // just build with spaces - not ideal, but close enough most of the time
                StringBuilder sb = new StringBuilder(tokens[startToken]);
                for (int i = startToken + 1; i <= endToken; i++)
                {
                    sb.Append(' ');
                    sb.Append(tokens[i]);
                }
                return sb.ToString();
            }
            // ignore
        }

        /**
         * Check whether the given text contains only a single token
         * <p>
         * A single token string is a string that contains exactly one token,
         * as identified by the {@link LicenseTextHelper#TOKEN_SPLIT_PATTERN}.
         * Whitespace and punctuation such as dots, commas, question marks,
         * and quotation marks are ignored.
         * </p>
         *
         * @param text The text to test.
         * @return {@code true} if the text contains exactly one token,
         *         {@code false} otherwise.
         */
        public static bool IsSingleTokenString(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            MatchCollection m = LicenseTextHelper.TOKEN_SPLIT_PATTERN.Matches(text);
            bool found = false;
            foreach (string match in m.Cast<Match>().Select(m => m.Groups[1].Value).Where(m => !string.IsNullOrWhiteSpace(m)))
            {
                if (found)
                {
                    return false; // More than one eligible token found
                }
                else
                {
                    found = true; // First eligible token found
                }
            }
            return true; // Zero or one eligible token found
        }

        /**
         * Get the text of a license minus any optional text - note: this includes the default variable text
         * @param licenseTemplate license template containing optional and var tags
         * @param varTextHandling include original, exclude, or include the regex (enclosed with "~~~") for "var" text
         * @return list of strings for all non-optional license text.  
         * @throws SpdxCompareException on comparison errors
         */
        public static IReadOnlyCollection<string> GetNonOptionalLicenseText(string licenseTemplate,
                VarTextHandling varTextHandling)
        {
            return GetNonOptionalLicenseText(licenseTemplate, varTextHandling, OptionalTextHandling.OMIT);
        }

        /**
         * Get the text of a license converting variable and optional text according to the options
         * @param licenseTemplate license template containing optional and var tags
         * @param varTextHandling include original, exclude, or include the regex (enclosed with "~~~") for "var" text
         * @param optionalTextHandling include optional text, exclude, or include a regex for the optional text
         * @return list of strings for all non-optional license text.  
         * @throws SpdxCompareException on comparison errors
         */
        public static IReadOnlyCollection<string> GetNonOptionalLicenseText(string licenseTemplate,
                VarTextHandling varTextHandling, OptionalTextHandling optionalTextHandling)
        {
            FilterTemplateOutputHandler filteredOutput = new FilterTemplateOutputHandler(varTextHandling, optionalTextHandling);
            try
            {
                SpdxLicenseTemplateHelper.ParseTemplate(licenseTemplate, filteredOutput);
            }
            catch (LicenseTemplateRuleException e)
            {
                throw new SpdxCompareException("Invalid template rule found during filter: " + e.Message, e);
            }
            catch (LicenseParserException e)
            {
                throw new SpdxCompareException("Invalid template found during filter: " + e.Message, e);
            }
            return filteredOutput.FilteredText;
        }

        /**
         * Compare the provided text against a license template using SPDX matching
         * guidelines
         *
         * @param template    Template in the standard template format used for
         *                    comparison
         * @param compareText Text to compare using the template
         * @return Any differences found
         * @throws SpdxCompareException on comparison errors
         */
        public static CompareTemplateOutputHandler.DifferenceDescription IsTextMatchingTemplate(string template, string compareText)
        {
            CompareTemplateOutputHandler compareTemplateOutputHandler;
            try
            {
                compareTemplateOutputHandler = new CompareTemplateOutputHandler(LicenseTextHelper.RemoveLineSeparators(RemoveCommentChars(compareText)));
            }
            catch (IOException e1)
            {
                throw new SpdxCompareException("IO Error reading the compare text: " + e1.Message, e1);
            }
            try
            {
                //TODO: The remove comment chars will not be removed for lines beginning with a template << or ending with >>
                SpdxLicenseTemplateHelper.ParseTemplate(RemoveCommentChars(template), compareTemplateOutputHandler);
            }
            catch (LicenseTemplateRuleException e)
            {
                throw new SpdxCompareException("Invalid template rule found during compare: " + e.Message, e);
            }
            catch (LicenseParserException e)
            {
                throw new SpdxCompareException("Invalid template found during compare: " + e.Message, e);
            }
            return compareTemplateOutputHandler.Differences;
        }
    }
}
