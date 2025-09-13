// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/**
 * SPDX-FileCopyrightText: Copyright (c) 2013 Source Auditor Inc.
 * SPDX-FileType: SOURCE
 * SPDX-License-Identifier: Apache-2.0
 */
namespace SPDXLicenseMatcher.JavaCore;

/**
 * Implements common conversion methods for processing SPDX license templates
 * 
 * @author Gary O'Neall
 */
public class SpdxLicenseTemplateHelper
{
    private const string START_RULE = "<<";
    private const string END_RULE = ">>";
    public static readonly Regex RULE_PATTERN = new Regex(START_RULE + "\\s*(beginOptional|endOptional|var)", RegexOptions.Compiled);
    public static readonly Regex END_RULE_PATTERN = new Regex(END_RULE, RegexOptions.Compiled);

    private SpdxLicenseTemplateHelper()
    {
        // Utility class - it should not be instantiated
    }

    /**
	 * Parses the license template calling the templateOutputHandler for any text
	 * and rules found
	 * 
	 * @param licenseTemplate       License template to be parsed
	 * @param templateOutputHandler Handles the text, optional text, and variable
	 *                              rules text found
	 * @throws LicenseTemplateRuleException if the rule can not be parsed
	 * @throws LicenseParserException if the license can not be parsed
	 */
    public static void parseTemplate(string licenseTemplate, ILicenseTemplateOutputHandler templateOutputHandler)
    {
        IEnumerator<Match> ruleMatcher = RULE_PATTERN.Matches(licenseTemplate).Cast<Match>().GetEnumerator();
        int end = 0;
        int optionalNestLevel = 0;
        while (ruleMatcher.MoveNext())
        {
            // copy everything up to the start of the find
            string upToTheFind = licenseTemplate.Substring(end, ruleMatcher.Current.Index - end);
            if (!string.IsNullOrWhiteSpace(upToTheFind))
            {
                templateOutputHandler.text(upToTheFind);
            }
            Match endMatch = END_RULE_PATTERN.Match(licenseTemplate, ruleMatcher.Current.Index + ruleMatcher.Current.Length);
            if (!endMatch.Success)
            {
                throw (new LicenseTemplateRuleException("Missing end of rule '>>' after text '" + upToTheFind + "'"));
            }
            end = endMatch.Index + endMatch.Length;
            string ruleString = licenseTemplate.Substring(ruleMatcher.Current.Index + START_RULE.Length, end - END_RULE.Length - ruleMatcher.Current.Index - START_RULE.Length);

            LicenseTemplateRule rule = new LicenseTemplateRule(ruleString);
            if (rule.Type == LicenseTemplateRule.RuleType.VARIABLE)
            {
                templateOutputHandler.variableRule(rule);
            }
            else if (rule.Type == LicenseTemplateRule.RuleType.BEGIN_OPTIONAL)
            {
                templateOutputHandler.beginOptional(rule);
                optionalNestLevel++;
            }
            else if (rule.Type == LicenseTemplateRule.RuleType.END_OPTIONAL)
            {
                optionalNestLevel--;
                if (optionalNestLevel < 0)
                {
                    throw new LicenseTemplateRuleException(
                            "End optional rule found without a matching begin optional rule after text '" + upToTheFind + "'");
                }
                templateOutputHandler.endOptional(rule);
            }
            else
            {
                throw new LicenseTemplateRuleException(
                        "Unrecognized rule: " + rule.Type.ToString() + " after text '" + upToTheFind + "'");
            }
        }
        if (optionalNestLevel > 0)
        {
            throw new LicenseTemplateRuleException("Missing EndOptional rule and end of text");
        }
        // copy the rest of the template to the end
        string restOfTemplate = licenseTemplate.Substring(end);
        if (!string.IsNullOrEmpty(restOfTemplate))
        {
            templateOutputHandler.text(restOfTemplate);
        }
        templateOutputHandler.completeParsing();
    }
}
