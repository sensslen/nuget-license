// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FileLicenseMatcher.SPDX.JavaCore;
using Spdx.Licenses;

namespace FileLicenseMatcher.SPDX.JavaLibrary;

internal static class LicenseCompareHelper
{
    private const string START_COMMENT_CHAR_PATTERN = "(//|/\\*|\\*|#|' |REM |<!--|--|;|\\(\\*|\\{-)|\\.\\\\\"";

#pragma warning disable IDE1006
    private static readonly Regex END_COMMENT_PATTERN = new Regex("(\\*/|-->|-}|\\*\\)|\\s\\*)\\s*$", RegexOptions.Compiled);
    private static readonly Regex START_COMMENT_PATTERN = new Regex("^\\s*" + START_COMMENT_CHAR_PATTERN, RegexOptions.Compiled);
    private static readonly Regex BEGIN_OPTIONAL_COMMENT_PATTERN = new Regex("^\\s*<<beginOptional>>\\s*" + START_COMMENT_CHAR_PATTERN, RegexOptions.Compiled);
#pragma warning restore IDE1006

    #region Additions to optimize matching performance (not available in the original java code)
    private static readonly IImmutableDictionary<string, string> s_cleanedTemplateMap = BuildLicenseMap();

    public static IEnumerable<string> GetMatchingLicenses(string licenseText)
    {
        string cleanedLicenseText = LicenseTextHelper.removeLineSeparators(removeCommentChars(licenseText));
        foreach (KeyValuePair<string, string> license in s_cleanedTemplateMap)
        {
            CompareTemplateOutputHandler compareTemplateOutputHandler;
            try
            {
                compareTemplateOutputHandler = new CompareTemplateOutputHandler(cleanedLicenseText);
            }
            catch (IOException e1)
            {
                throw new SpdxCompareException("IO Error reading the compare text: " + e1.Message, e1);
            }
            try
            {
                SpdxLicenseTemplateHelper.parseTemplate(license.Value, compareTemplateOutputHandler);
            }
            catch (LicenseTemplateRuleException e)
            {
                throw new SpdxCompareException("Invalid template rule found during compare: " + e.Message, e);
            }
            catch (LicenseParserException e)
            {
                throw new SpdxCompareException("Invalid template found during compare: " + e.Message, e);
            }
            if (!compareTemplateOutputHandler.Differences.DifferenceFound)
            {
                yield return license.Key;
            }
        }
    }

    private static IImmutableDictionary<string, string> BuildLicenseMap()
    {
        ImmutableDictionary<string, string>.Builder builder = ImmutableDictionary.CreateBuilder<string, string>();
        foreach (KeyValuePair<string, ILicense> license in SpdxLicenseStore.Licenses)
        {
            string licenseTemplate = license.Value.StandardLicenseTemplate;
            if (string.IsNullOrWhiteSpace(licenseTemplate))
            {
                licenseTemplate = license.Value.LicenseText;
            }
            if (!string.IsNullOrWhiteSpace(licenseTemplate))
            {
                builder[license.Key] = removeCommentChars(licenseTemplate);
            }
        }
        return builder.ToImmutable();
    }
    #endregion

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
    public static DifferenceDescription IsTextMatchingTemplate(string template, string compareText)
    {
        CompareTemplateOutputHandler compareTemplateOutputHandler;
        try
        {
            compareTemplateOutputHandler = new CompareTemplateOutputHandler(LicenseTextHelper.removeLineSeparators(removeCommentChars(compareText)));
        }
        catch (IOException e1)
        {
            throw new SpdxCompareException("IO Error reading the compare text: " + e1.Message, e1);
        }
        try
        {
            SpdxLicenseTemplateHelper.parseTemplate(removeCommentChars(template), compareTemplateOutputHandler);
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
    public static string locateOriginalText(string fullLicenseText, int startToken, int endToken,
            IDictionary<int, LineColumn> tokenToLocation, IReadOnlyList<string> tokens)
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
                return line.Substring(start.Column, end.Column + end.Len - start.Column);
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
                if (line != null && end.Column + end.Len > 0)
                {
                    sb.Append("\n");
                    sb.Append(line, 0, end.Column + end.Len);
                }
                return sb.ToString();
            }
        }
        catch (IOException)
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
    public static bool isSingleTokenString(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        MatchCollection m = LicenseTextHelper.TOKEN_SPLIT_PATTERN.Matches(text);
        bool found = false;
        foreach (Match _ in m.Cast<Match>().Where(m => !string.IsNullOrWhiteSpace(m.Groups[1].Value)))
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
     * Remove common comment characters from either a template or license text
     * strings
     *
     * @param s string source
     * @return string without comment characters
     */
    public static string removeCommentChars(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }
        StringBuilder sb = new StringBuilder();
        using var reader = new StringReader(s);
        try
        {
            string? line = reader.ReadLine();
            bool firstLine = true;
            while (line != null)
            {
                line = END_COMMENT_PATTERN.Replace(line, "");
                line = START_COMMENT_PATTERN.Replace(line, "");
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
            return s;
        }
        return sb.ToString();
    }
}
