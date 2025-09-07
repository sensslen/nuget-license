// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SPDXLicenseMatcher.JavaPort;

namespace SPDXLicenseMatcher
{
    public class LicenseMatcher : ILicenseMatcher
    {
        public string? Match(string licenseText) => string.Join(" OR ", MatchingStandardLicenseIds(licenseText));

        private static IEnumerable<string> MatchingStandardLicenseIds(string licenseText)
        {
            string cleanedLicense = LicenseTextHelper.RemoveLineSeparators(RemoveCommentChars(licenseText));
            foreach (KeyValuePair<string, string> license in s_cleanedLicenseTemplates)
            {
                if (!IsTextStandardLicense(license.Value, cleanedLicense).IsDifferenceFound)
                {
                    yield return license.Key;
                }
            }
        }

        private static readonly IImmutableDictionary<string, string> s_cleanedLicenseTemplates;

        public static CompareTemplateOutputHandler.DifferenceDescription IsTextStandardLicense(string template, string compareText)
        {
            CompareTemplateOutputHandler compareTemplateOutputHandler;
            try
            {
                compareTemplateOutputHandler = new CompareTemplateOutputHandler(compareText);
            }
            catch (IOException e1)
            {
                throw new SpdxCompareException("IO Error reading the compare text: " + e1.Message, e1);
            }
            try
            {
                //TODO: The remove comment chars will not be removed for lines beginning with a template << or ending with >>
                SpdxLicenseTemplateHelper.ParseTemplate(template, compareTemplateOutputHandler);
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

        private const string START_COMMENT_CHAR_PATTERN = "(//|/\\*|\\*|#|' |REM |<!--|--|;|\\(\\*|\\{-)|\\.\\\\\"";

        private static readonly Regex s_endCommentPattern;
        private static readonly Regex s_startCommentPattern;
        private static readonly Regex s_beginOptionalCommentPattern;

        public static string RemoveCommentChars(string s)
        {
            // Use string.IsNullOrEmpty for a concise null or empty check.
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }

            var sb = new StringBuilder();
            // 'using' ensures the StringReader is properly disposed.
            using (var reader = new StringReader(s))
            {
                string? line;
                bool firstLine = true;
                // Read the string line by line, just like the Java BufferedReader.
                while ((line = reader.ReadLine()) != null)
                {
                    // The order of these replacements is preserved from the original Java method.
                    line = s_endCommentPattern.Replace(line, "");
                    line = s_startCommentPattern.Replace(line, "");
                    line = s_beginOptionalCommentPattern.Replace(line, "<<beginOptional>>");

                    if (!firstLine)
                    {
                        sb.Append('\n');
                    }
                    else
                    {
                        firstLine = false;
                    }
                    sb.Append(line);
                }
            }
            return sb.ToString();
        }

        static LicenseMatcher()
        {
            s_endCommentPattern = new Regex("(\\*/|-->|-}|\\*\\)|\\s\\*)\\s*$", RegexOptions.Compiled);
            s_startCommentPattern = new Regex("^\\s*" + START_COMMENT_CHAR_PATTERN, RegexOptions.Compiled);
            s_beginOptionalCommentPattern = new Regex("^\\s*<<beginOptional>>\\s*" + START_COMMENT_CHAR_PATTERN, RegexOptions.Compiled);

            s_cleanedLicenseTemplates = Spdx.Licenses.SpdxLicenseStore.Licenses.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    string licenseTemplate = kvp.Value.StandardLicenseTemplate;
                    if (string.IsNullOrWhiteSpace(licenseTemplate))
                    {
                        licenseTemplate = kvp.Value.LicenseText;
                    }
                    return RemoveCommentChars(licenseTemplate);
                });
        }
    }
}
