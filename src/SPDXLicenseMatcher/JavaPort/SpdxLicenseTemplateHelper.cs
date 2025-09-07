// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Text.RegularExpressions;

namespace SPDXLicenseMatcher.JavaPort
{
    /// <summary>
    /// Implements common conversion methods for processing SPDX license templates.
    /// </summary>
    public static class SpdxLicenseTemplateHelper
    {
        private const string StartRule = "<<";
        private const string EndRule = ">>";
        public static readonly Regex RulePattern = new Regex(StartRule + "\\s*((beginOptional|endOptional|var)(.|\\s)*?)\\s*" + EndRule);

        /// <summary>
        /// Parses a license template, calling the templateOutputHandler for any text and rules found.
        /// </summary>
        /// <param name="licenseTemplate">License template to be parsed.</param>
        /// <param name="templateOutputHandler">Handler for the parsed text and rules.</param>
        public static void ParseTemplate(string licenseTemplate, ILicenseTemplateOutputHandler templateOutputHandler)
        {
            MatchCollection matches = RulePattern.Matches(licenseTemplate);
            int lastIndex = 0;
            int optionalNestLevel = 0;

            foreach (Match match in matches)
            {
                // Capture the plain text between the last rule and this one.
                string textBeforeRule = licenseTemplate.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(textBeforeRule))
                {
                    templateOutputHandler.Text(textBeforeRule);
                }

                lastIndex = match.Index + match.Length;

                string ruleString = match.Groups[1].Value;
                var rule = new LicenseTemplateRule(ruleString);
                optionalNestLevel = ProcessRule(templateOutputHandler, optionalNestLevel, textBeforeRule, rule);
            }

            if (optionalNestLevel > 0)
            {
                throw new LicenseTemplateRuleException("Missing one or more EndOptional rules at the end of the template.");
            }

            // Capture any remaining text after the last rule.
            string remainingText = licenseTemplate.Substring(lastIndex);
            if (!string.IsNullOrEmpty(remainingText))
            {
                templateOutputHandler.Text(remainingText);
            }
            templateOutputHandler.CompleteParsing();
        }

        private static int ProcessRule(ILicenseTemplateOutputHandler templateOutputHandler, int currentOptionalNestLevel, string textBeforeRule, LicenseTemplateRule rule)
        {
            switch (rule.Type)
            {
                case LicenseTemplateRule.RuleType.Variable:
                    templateOutputHandler.VariableRule(rule);
                    break;
                case LicenseTemplateRule.RuleType.BeginOptional:
                    templateOutputHandler.BeginOptional(rule);
                    currentOptionalNestLevel++;
                    break;
                case LicenseTemplateRule.RuleType.EndOptional:
                    currentOptionalNestLevel--;
                    if (currentOptionalNestLevel < 0)
                    {
                        throw new LicenseTemplateRuleException($"EndOptional rule found without a matching BeginOptional rule after text: '{textBeforeRule}'");
                    }
                    templateOutputHandler.EndOptional(rule);
                    break;
                default:
                    throw new LicenseTemplateRuleException($"Unrecognized rule type '{rule.Type}' after text: '{textBeforeRule}'");
            }
            return currentOptionalNestLevel;
        }
    }
}
