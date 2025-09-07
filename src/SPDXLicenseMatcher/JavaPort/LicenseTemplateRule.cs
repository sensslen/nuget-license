// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Copyright (c) 2013 Source Auditor Inc.
///
///    Licensed under the Apache License, Version 2.0 (the "License");
///    you may not use this file except in compliance with the License.
///    You may obtain a copy of the License at
///
///        http://www.apache.org/licenses/LICENSE-2.0
///
///    Unless required by applicable law or agreed to in writing, software
///    distributed under the License is distributed on an "AS IS" BASIS,
///    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
///    See the License for the specific language governing permissions and
///    limitations under the License.
/// </summary>
namespace SPDXLicenseMatcher.JavaPort
{
    /// <summary>
    /// Implements a license rule.
    /// </summary>
    public class LicenseTemplateRule
    {
        public enum RuleType { Variable, BeginOptional, EndOptional };

        // Using a static Regex object is more efficient than compiling on each use.
        private static readonly Regex s_splitRegex = new Regex("[^\\\\];");
        private const string ExampleKeyword = "example";
        private const string NameKeyword = "name";
        private const string OriginalKeyword = "original";
        private const string MatchKeyword = "match";
        private const string VariableRuleTypeStr = "var";
        private const string BeginOptionalTypeStr = "beginOptional";
        private const string EndOptionalTypeStr = "endOptional";
        private const string ValueSeparator = "=";

        public RuleType? Type { get; set; }
        public string? Original { get; set; }
        public string? Name { get; set; }
        public string? Example { get; set; }
        public string? Match { get; set; }

        /// <summary>
        /// Creates a new LicenseTemplateRule.
        /// </summary>
        /// <param name="name">Name of the rule - must not be null for variable rules.</param>
        /// <param name="type">Type of rule.</param>
        /// <param name="original">Original text - must not be null for variable rules.</param>
        /// <param name="match">The regex match pattern.</param>
        /// <param name="example">Example text - may be null.</param>
        /// <exception cref="LicenseTemplateRuleException">Thrown if validation fails.</exception>
        public LicenseTemplateRule(string name, RuleType type, string original, string match, string example)
        {
            Type = type;
            Original = FormatValue(original);
            Name = name;
            Example = FormatValue(example);
            Match = match;
            Validate();
        }

        /// <summary>
        /// Creates a new License Template Rule by parsing a rule string compliant with the SPDX
        /// License Template text.
        /// </summary>
        /// <param name="parseableLicenseTemplateRule">The rule string to parse.</param>
        /// <exception cref="LicenseTemplateRuleException">Thrown if parsing or validation fails.</exception>
        public LicenseTemplateRule(string parseableLicenseTemplateRule)
        {
            ParseLicenseTemplateRule(parseableLicenseTemplateRule);
            Validate();
        }

        /// <summary>
        /// Creates a new License Template Rule for non-variable rules.
        /// </summary>
        /// <param name="ruleName">Name of the rule.</param>
        /// <param name="ruleType">Type of the rule.</param>
        /// <exception cref="LicenseTemplateRuleException">Thrown if validation fails.</exception>
        public LicenseTemplateRule(string ruleName, RuleType ruleType)
        {
            Name = ruleName;
            Type = ruleType;
            Validate();
        }

        /// <summary>
        /// Provides a string representation of the rule.
        /// </summary>
        public override string ToString()
        {
            return Type switch
            {
                RuleType.Variable => $"var: {Name ?? ""}",
                RuleType.BeginOptional => "beginOptional",
                RuleType.EndOptional => "endOptional",
                _ => "Unknown",
            };
        }

        /// <summary>
        /// Validates that the LicenseTemplateRule is properly initialized.
        /// </summary>
        /// <exception cref="LicenseTemplateRuleException">Thrown if a required property is missing.</exception>
        public void Validate()
        {
            // Note: A non-nullable enum does not need a null check in C#.
            if (Type == RuleType.Variable && string.IsNullOrEmpty(Name))
            {
                throw new LicenseTemplateRuleException("Rule name cannot be null for a variable rule.");
            }
            if (Type == RuleType.Variable && Original == null)
            {
                throw new LicenseTemplateRuleException("Rule original text cannot be null.");
            }
            if (Type == RuleType.Variable && Match == null)
            {
                throw new LicenseTemplateRuleException("Rule match regular expression cannot be null.");
            }
        }

        /// <summary>
        /// Parses a license template rule string compliant with the SPDX license template text and
        /// replaces all properties with the parsed values.
        /// </summary>
        /// <param name="parseableLicenseTemplateRule">The rule string to parse.</param>
        /// <exception cref="LicenseTemplateRuleException">Thrown on parsing errors.</exception>
        public void ParseLicenseTemplateRule(string parseableLicenseTemplateRule)
        {
            // Reset fields
            Example = null;
            Name = null;
            Original = null;
            Match = null;

            MatchCollection matches = s_splitRegex.Matches(parseableLicenseTemplateRule);
            int lastPos = 0;

            // First part is always the rule type
            string typeStr;
            if (matches.Count > 0)
            {
                typeStr = parseableLicenseTemplateRule.Substring(lastPos, matches[0].Index + 1).Trim();
                lastPos = matches[0].Index + matches[0].Length;
            }
            else
            {
                typeStr = parseableLicenseTemplateRule.Trim();
                lastPos = parseableLicenseTemplateRule.Length;
            }
            Type = TypeStringToType(typeStr);

            // Parse remaining key-value fields
            foreach (Match match in matches.Cast<Match>().Skip(1))
            {
                string rulePart = parseableLicenseTemplateRule.Substring(lastPos, match.Index + 1 - lastPos).Trim();
                ParseRulePart(rulePart);
                lastPos = match.Index + match.Length;
            }

            // Handle the final part after the last matched semicolon
            string remainingRuleString = parseableLicenseTemplateRule.Substring(lastPos).Trim();
            if (!string.IsNullOrEmpty(remainingRuleString))
            {
                ParseRulePart(remainingRuleString);
            }
            Validate();
        }

        /// <summary>
        /// Converts the string representation of a rule type to the corresponding enum value.
        /// </summary>
        private static RuleType TypeStringToType(string typeStr)
        {
            if (typeStr.Equals(VariableRuleTypeStr, StringComparison.Ordinal))
            {
                return RuleType.Variable;
            }
            if (typeStr.Equals(BeginOptionalTypeStr, StringComparison.Ordinal))
            {
                return RuleType.BeginOptional;
            }
            if (typeStr.Equals(EndOptionalTypeStr, StringComparison.Ordinal))
            {
                return RuleType.EndOptional;
            }
            throw new LicenseTemplateRuleException($"Unknown rule type: {typeStr}");
        }

        /// <summary>
        /// Parses a part of a rule (a key-value pair) and sets the corresponding property.
        /// </summary>
        private void ParseRulePart(string rulePart)
        {
            if (rulePart.StartsWith(ExampleKeyword, StringComparison.Ordinal))
            {
                Example = FormatValue(GetValue(rulePart, ExampleKeyword));
            }
            else if (rulePart.StartsWith(NameKeyword, StringComparison.Ordinal))
            {
                Name = GetValue(rulePart, NameKeyword);
            }
            else if (rulePart.StartsWith(OriginalKeyword, StringComparison.Ordinal))
            {
                Original = FormatValue(GetValue(rulePart, OriginalKeyword));
            }
            else if (rulePart.StartsWith(MatchKeyword, StringComparison.Ordinal))
            {
                Match = GetValue(rulePart, MatchKeyword);
            }
            else
            {
                throw new LicenseTemplateRuleException($"Unknown rule keyword: {rulePart}");
            }
        }

        /// <summary>
        /// Interprets escape characters (e.g., '\n', '\t') in a string value.
        /// </summary>
        private static string? FormatValue(string value)
        {
            if (value == null) return null;
            // The C# compiler handles these escapes, but if reading from a file,
            // this explicit replacement is correct.
            return value.Replace("\\n", "\n").Replace("\\t", "\t");
        }

        /// <summary>
        /// Retrieves the value portion of a rule part (e.g., the "..." from "key="..."").
        /// </summary>
        private static string GetValue(string rulePart, string keyword)
        {
            string value = rulePart.Substring(keyword.Length).Trim();
            if (!value.StartsWith(ValueSeparator))
            {
                throw new LicenseTemplateRuleException($"Missing '{ValueSeparator}' for keyword '{keyword}'");
            }

            value = value.Substring(1).Trim();

            // Trim leading and trailing quotes
#if NET5_0_OR_GREATER
            if (value.StartsWith('"'))
#else
            if (value.StartsWith("\""))
#endif
            {
                value = value.Substring(1);
            }
#if NET5_0_OR_GREATER
            if (value.EndsWith('"'))
#else
            if (value.EndsWith("\""))
#endif
            {
                value = value.Substring(0, value.Length - 1);
            }
            return value;
        }
    }
}
