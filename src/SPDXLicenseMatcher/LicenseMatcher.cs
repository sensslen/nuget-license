// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
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
            foreach (KeyValuePair<string, Spdx.Licenses.ILicense> license in Spdx.Licenses.SpdxLicenseStore.Licenses)
            {
                if (!IsTextStandardLicense(license.Value, licenseText).IsDifferenceFound)
                {
                    yield return license.Key;
                }
            }
        }

        public static CompareTemplateOutputHandler.DifferenceDescription IsTextStandardLicense(Spdx.Licenses.ILicense license, string compareText)
        {

            string licenseTemplate = license.StandardLicenseTemplate;
            if (string.IsNullOrWhiteSpace(licenseTemplate))
            {
                licenseTemplate = license.LicenseText;
            }
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
                SpdxLicenseTemplateHelper.ParseTemplate(RemoveCommentChars(licenseTemplate), compareTemplateOutputHandler);
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

        private static readonly Regex s_endCommentPattern = new Regex("(\\*/|-->|-}|\\*\\)|\\s\\*)\\s*$", RegexOptions.Compiled);
        private static readonly Regex s_startCommentPattern = new Regex("^\\s*" + START_COMMENT_CHAR_PATTERN, RegexOptions.Compiled);
        private static readonly Regex s_beginOptionalCommentPattern = new Regex("^\\s*<<beginOptional>>\\s*" + START_COMMENT_CHAR_PATTERN, RegexOptions.Compiled);

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
    }
}
