// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Linq;
using System.Text.RegularExpressions;

/**
 * SPDX-FileCopyrightText: Copyright (c) 2013 Source Auditor Inc.
 * SPDX-FileType: SOURCE
 * SPDX-License-Identifier: Apache-2.0
 */

namespace SPDXLicenseMatcher.JavaCore;

/**
 * Implements a license rule
 * @author Gary O'Neall
 *
 */
public class LicenseTemplateRule
{
    public enum RuleType { VARIABLE, BEGIN_OPTIONAL, END_OPTIONAL }

    public RuleType? Type { get; set; }
    public string? Original { get; set; } = null;
    public string? Name { get; set; }
    public string? Example { get; set; } = null;
    public string? Match { get; set; } = null;

#pragma warning disable IDE1006
    static readonly Regex SPLIT_REGEX = new Regex("[^\\\\];", RegexOptions.Compiled);
#pragma warning restore IDE1006
    private const string EXAMPLE_KEYWORD = "example";
    private const string NAME_KEYWORD = "name";
    private const string ORIGINAL_KEYWORD = "original";
    private const string MATCH_KEYWORD = "match";
    private const string VARIABLE_RULE_TYPE_STR = "var";
    private const string BEGIN_OPTIONAL_TYPE_STR = "beginOptional";
    private const string END_OPTIONAL_TYPE_STR = "endOptional";
    private const string VALUE_SEPARATOR = "=";

    /**
	 * Create a new LicenseTemplateRule
	 * @param name Name of the rule - must not be null
	 * @param type Type of rule
	 * @param original Original text - must not be null
	 * @param example Example text - may be null
	 * @throws LicenseTemplateRuleException if the license template could not be parsed
	 */
    public LicenseTemplateRule(string name, RuleType type, string original, string match, string example)
    {
        Type = type;
        Original = formatValue(original);
        Name = name;
        Example = formatValue(example);
        Match = match;
        validate();
    }

    /**
     * Create a new License Template Rule by parsing a rule string compliant with the SPDX
     * License Template text
     * @param parseableLicenseTemplateRule license template rule string
     * @throws LicenseTemplateRuleException if the license template could not be parsed
     */
    public LicenseTemplateRule(string parseableLicenseTemplateRule)
    {
        parseLicenseTemplateRule(parseableLicenseTemplateRule);
        validate();
    }

    /**
     * @param ruleName rule name
     * @param ruleType rule type
     * @throws LicenseTemplateRuleException if the license template could not be parsed
     */
    public LicenseTemplateRule(string ruleName, RuleType ruleType)
    {
        Name = ruleName;
        Type = ruleType;
        validate();
    }

    public override string ToString()
    {
        if (RuleType.VARIABLE.Equals(Type))
        {
            return $"var: {Name}";
        }
        else if (RuleType.BEGIN_OPTIONAL.Equals(Type))
        {
            return BEGIN_OPTIONAL_TYPE_STR;
        }
        else if (RuleType.END_OPTIONAL.Equals(Type))
        {
            return END_OPTIONAL_TYPE_STR;
        }
        else
        {
            return "Unknown";
        }
    }

    /**
     * Validates that the LicenseTemplateRule is properly initialized
     * @throws LicenseTemplateRuleException if the license template could not be parsed
     */
    public void validate()
    {
        if (Type == null)
        {
            throw new LicenseTemplateRuleException("Rule type can not be null.");
        }
        if (Type == RuleType.VARIABLE && Name == null)
        {
            throw new LicenseTemplateRuleException("Rule name can not be null for a variable or alt rule.");
        }
        if (Type == RuleType.VARIABLE && Original == null)
        {
            throw new LicenseTemplateRuleException("Rule original text can not be null.");
        }
        if (Type == RuleType.VARIABLE && Match == null)
        {
            throw new LicenseTemplateRuleException("Rule match regular expression can not be null.");
        }
    }

    /**
     * Parse a license template rule string compliant with the SPDX license template text and
     * replace all properties with the parsed values
     * @param parseableLicenseTemplateRule String representation of a license template rule
     * @throws LicenseTemplateRuleException if the license template could not be parsed
     */
    public void parseLicenseTemplateRule(string parseableLicenseTemplateRule)
    {
        Example = null;
        Name = null;
        Original = null;
        Type = null;
        Match = null;
        MatchCollection rulePartMatcher = SPLIT_REGEX.Matches(parseableLicenseTemplateRule);
        int start = 0;
        // parse out the first field - should be the rule type
        string typeStr;
        if (rulePartMatcher.Count > 0)
        {
            typeStr = parseableLicenseTemplateRule.Substring(start, rulePartMatcher[0].Index + 1 - start).Trim();
            start = rulePartMatcher[0].Index + rulePartMatcher[0].Length;
        }
        else
        {
            typeStr = parseableLicenseTemplateRule.Trim();
            start = parseableLicenseTemplateRule.Length;
        }
        Type = typeStringToType(typeStr);

        // parse out remaining fields
        foreach (Match match in rulePartMatcher.Cast<Match>().Skip(1))
        {
            string rulePart = parseableLicenseTemplateRule.Substring(start, match.Index + 1 - start);
            parseRulePart(rulePart.Trim());
            start = match.Index + match.Length;
        }
        string remainingRuleString = parseableLicenseTemplateRule.Substring(start).Trim();
        if (!string.IsNullOrEmpty(remainingRuleString))
        {
            parseRulePart(remainingRuleString);
        }
        validate();
    }

    /**
     * @param typeStr string representing the type of rule
     * @return rule type
     * @throws LicenseTemplateRuleException if the license template could not be parsed
     */
    private static RuleType typeStringToType(string typeStr) => typeStr switch
    {
        VARIABLE_RULE_TYPE_STR => RuleType.VARIABLE,
        BEGIN_OPTIONAL_TYPE_STR => RuleType.BEGIN_OPTIONAL,
        END_OPTIONAL_TYPE_STR => RuleType.END_OPTIONAL,
        _ => throw new LicenseTemplateRuleException("Unknown rule type: " + typeStr),
    };

    /**
     * Parse the part of a rule and stores the result as a property
     * @param rulePart string representation of the license rule
     * @throws LicenseTemplateRuleException if the license template could not be parsed
     */
    private void parseRulePart(string rulePart)
    {
        if (rulePart.StartsWith(EXAMPLE_KEYWORD))
        {
            string value = getValue(rulePart, EXAMPLE_KEYWORD);
            Example = formatValue(value);
        }
        else if (rulePart.StartsWith(NAME_KEYWORD))
        {
            Name = getValue(rulePart, NAME_KEYWORD);
        }
        else if (rulePart.StartsWith(ORIGINAL_KEYWORD))
        {
            string value = getValue(rulePart, ORIGINAL_KEYWORD);
            Original = formatValue(value);
        }
        else if (rulePart.StartsWith(MATCH_KEYWORD))
        {
            Match = getValue(rulePart, MATCH_KEYWORD);
        }
        else
        {
            throw new LicenseTemplateRuleException("Unknown rule keyword: " + rulePart);
        }
    }

    /**
     * Formats the string interpreting escape characters
     * @param value string to format
     * @return formatted string
     */
    private static string formatValue(string value)
    {
        string retval = value.Replace("\\n", "\n");
        retval = retval.Replace("\\t", "\t");
        return retval;
    }

    /**
     * Retrieve the value portion of a rule part
     * @param rulePart the rule part in string format
     * @param keyword keyword
     * @return the value portion of a rule part
     * @throws LicenseTemplateRuleException if the license template could not be parsed
     */
    private static string getValue(string rulePart, string keyword)
    {
        string retval = rulePart.Substring(keyword.Length);
        retval = retval.Trim();
        if (!retval.StartsWith(VALUE_SEPARATOR))
        {
            throw new LicenseTemplateRuleException("Missing " + VALUE_SEPARATOR + " for " + keyword);
        }
        retval = retval.Substring(1).Trim();
#if NETFRAMEWORK
        if (retval.StartsWith("\""))
#else
        if (retval.StartsWith('"'))
#endif
        {
            retval = retval.Substring(1);
        }
#if NETFRAMEWORK
        if (retval.EndsWith("\""))
#else
        if (retval.EndsWith('"'))
#endif
        {
            retval = retval.Substring(0, retval.Length - 1);
        }
        return retval;
    }

}
