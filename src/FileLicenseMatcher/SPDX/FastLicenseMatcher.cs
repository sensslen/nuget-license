// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FileLicenseMatcher.SPDX.JavaCore;
using FileLicenseMatcher.SPDX.JavaLibrary;
using Spdx.Licenses;

namespace FileLicenseMatcher.SPDX
{
    /// <summary>
    /// This class implements a faster way to match licenses as compared to the LicenseMatcher class.
    /// The code is not as close to the original java code as the LicenseMatcher class.
    /// </summary>
    public class FastLicenseMatcher : IFileLicenseMatcher
    {
        private const string START_RULE = "<<";
        private const string END_RULE = ">>";

        private const string START_COMMENT_CHAR_PATTERN = "(//|/\\*|\\*|#|' |REM |<!--|--|;|\\(\\*|\\{-)|\\.\\\\\"";

#pragma warning disable IDE1006
        private static readonly Regex RULE_PATTERN = new Regex(START_RULE + "\\s*(beginOptional|endOptional|var)", RegexOptions.Compiled);
        private static readonly Regex END_RULE_PATTERN = new Regex(END_RULE, RegexOptions.Compiled);
        private static readonly Regex END_COMMENT_PATTERN = new Regex("(\\*/|-->|-}|\\*\\)|\\s\\*)\\s*$", RegexOptions.Compiled);
        private static readonly Regex START_COMMENT_PATTERN = new Regex("^\\s*" + START_COMMENT_CHAR_PATTERN, RegexOptions.Compiled);
        private static readonly Regex BEGIN_OPTIONAL_COMMENT_PATTERN = new Regex("^\\s*<<beginOptional>>\\s*" + START_COMMENT_CHAR_PATTERN, RegexOptions.Compiled);
#pragma warning restore IDE1006

        private readonly IImmutableDictionary<string, ParseInstruction> _templateInstructions;

        public FastLicenseMatcher(IDictionary<string, ILicense> licenses)
        {
            ImmutableDictionary<string, ParseInstruction>.Builder builder = ImmutableDictionary.CreateBuilder<string, ParseInstruction>();
            foreach (KeyValuePair<string, ILicense> license in licenses)
            {
                builder.Add(license.Key, ParseTemplate(license.Value));
            }

            _templateInstructions = builder.ToImmutable();
        }
        public string Match(string licenseText) => string.Join(" OR ", FindMatchingLicenses(licenseText));

        public IEnumerable<string> FindMatchingLicenses(string licenseText)
        {
            string cleanedLicenseText = LicenseTextHelper.removeLineSeparators(removeCommentChars(licenseText));
            string normalizedText = LicenseTextHelper.normalizeText(LicenseTextHelper.replaceMultWord(LicenseTextHelper.replaceSpaceComma(cleanedLicenseText)));
            var tokenToLocation = new Dictionary<int, LineColumn>();
            IReadOnlyList<string> compareTokens = LicenseTextHelper.tokenizeLicenseText(normalizedText, tokenToLocation);

            foreach (KeyValuePair<string, ParseInstruction> kvp in _templateInstructions)
            {
                var differences = new DifferenceDescription();
                int nextTokenIndex = kvp.Value.match(compareTokens, 0, compareTokens.Count - 1, normalizedText, differences, tokenToLocation, compareTokens);
                if (nextTokenIndex > 0 && nextTokenIndex < compareTokens.Count)
                {
                    differences.AddDifference(tokenToLocation[nextTokenIndex],
                            LicenseTextHelper.getTokenAt(compareTokens, nextTokenIndex),
                            "Additional text found after the end of the expected license text", null, null, null);
                }
                if (!differences.DifferenceFound)
                {
                    yield return kvp.Key;
                }
            }
        }

        private static ParseInstruction ParseTemplate(ILicense license)
        {
            string licenseTemplate = removeCommentChars(string.IsNullOrWhiteSpace(license.StandardLicenseTemplate) ? license.LicenseText : license.StandardLicenseTemplate);
            IEnumerator<Match> ruleMatches = RULE_PATTERN.Matches(licenseTemplate).Cast<Match>().GetEnumerator();
            var instructionStack = new Stack<ParseInstruction>();
            instructionStack.Push(new ParseInstruction(null, null));

            int end = 0;
            while (ruleMatches.MoveNext())
            {
                // copy everything up to the start of the find
                string upToTheFind = licenseTemplate.Substring(end, ruleMatches.Current.Index - end);
                if (!string.IsNullOrWhiteSpace(upToTheFind))
                {
                    instructionStack.Peek().addSubInstruction(new ParseInstruction(null, upToTheFind));
                }
                Match endMatch = END_RULE_PATTERN.Match(licenseTemplate, ruleMatches.Current.Index + ruleMatches.Current.Length);
                if (!endMatch.Success)
                {
                    throw new LicenseTemplateRuleException("Missing end of rule '>>' after text '" + upToTheFind + "'");
                }
                end = endMatch.Index + endMatch.Length;
                string ruleString = licenseTemplate.Substring(ruleMatches.Current.Index + START_RULE.Length, end - END_RULE.Length - ruleMatches.Current.Index - START_RULE.Length);

                LicenseTemplateRule rule = new LicenseTemplateRule(ruleString);
                if (rule.Type == LicenseTemplateRule.RuleType.VARIABLE)
                {
                    instructionStack.Peek().addSubInstruction(new ParseInstruction(rule, null));
                }
                else if (rule.Type == LicenseTemplateRule.RuleType.BEGIN_OPTIONAL)
                {
                    instructionStack.Push(new ParseInstruction(rule, null));
                }
                else if (rule.Type == LicenseTemplateRule.RuleType.END_OPTIONAL)
                {
                    ParseInstruction optionalInstruction = instructionStack.Pop();
                    if (instructionStack.Count <= 0)
                    {
                        throw new LicenseTemplateRuleException(
                                "End optional rule found without a matching begin optional rule after text '" + upToTheFind + "'");
                    }
                    instructionStack.Peek().addSubInstruction(optionalInstruction);
                }
                else
                {
                    throw new LicenseTemplateRuleException(
                            "Unrecognized rule: " + rule.Type.ToString() + " after text '" + upToTheFind + "'");
                }
            }
            if (instructionStack.Count > 1)
            {
                throw new LicenseTemplateRuleException("Missing EndOptional rule and end of text");
            }
            // copy the rest of the template to the end
            ParseInstruction result = instructionStack.Pop();
            string restOfTemplate = licenseTemplate.Substring(end);
            if (!string.IsNullOrWhiteSpace(restOfTemplate))
            {
                result.addSubInstruction(new ParseInstruction(null, restOfTemplate));
            }
            return result;
        }


        /**
         * Remove common comment characters from either a template or license text
         * strings
         *
         * @param s string source
         * @return string without comment characters
         */
        private static string removeCommentChars(string s)
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
}
