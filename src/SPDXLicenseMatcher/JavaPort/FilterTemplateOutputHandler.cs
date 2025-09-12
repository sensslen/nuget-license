// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/**
 * SPDX-FileCopyrightText: Copyright (c) 2020 Source Auditor Inc.
 * SPDX-FileType: SOURCE
 * SPDX-License-Identifier: Apache-2.0
 * <p>
 *   Licensed under the Apache License, Version 2.0 (the "License");
 *   you may not use this file except in compliance with the License.
 *   You may obtain a copy of the License at
 * <p>
 *       http://www.apache.org/licenses/LICENSE-2.0
 * <p>
 *   Unless required by applicable law or agreed to in writing, software
 *   distributed under the License is distributed on an "AS IS" BASIS,
 *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *   See the License for the specific language governing permissions and
 *   limitations under the License.
 */
namespace SPDXLicenseMatcher.JavaPort;
/**
 * Filter the template output to create a list of strings filtering out optional and/or var text
 * 
 * @deprecated The <code>TemplateRegexMatcher</code> class should be used in place of this class.  This class will be removed in the next major release.
 * @author Gary O'Neall
 */
public class FilterTemplateOutputHandler : ILicenseTemplateOutputHandler
{
    /**
	 * String used to escape
	 */
    public const string REGEX_ESCAPE = "~~~";


    public VarTextHandling VarTextHandling { get; }
    public IReadOnlyList<string> FilteredText => _filteredText;
    private readonly OptionalTextHandling _optionalTextHandling;
    private readonly List<string> _filteredText = new List<string>();
    private readonly StringBuilder _currentString = new StringBuilder();
    private int _optionalDepth = 0;  // depth of optional rules
    private readonly Dictionary<int, List<string>> _optionalTokens = new(); // map of optional dept to a list of tokens for the optional text

    /**
     * @param varTextHandling include original, exclude, or include the regex (enclosed with "~~~") for "var" text
     * @param optionalTextHandling include optional text, exclude, or include a regex for the optional text
     */
    public FilterTemplateOutputHandler(VarTextHandling varTextHandling, OptionalTextHandling optionalTextHandling)
    {
        VarTextHandling = varTextHandling;
        _optionalTextHandling = optionalTextHandling;
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#text(java.lang.String)
     */
    public void Text(string text)
    {
        if (_optionalDepth <= 0 || OptionalTextHandling.ORIGINAL.Equals(_optionalTextHandling))
        {
            _currentString.Append(text);
        }
        else if (OptionalTextHandling.REGEX_USING_TOKENS.Equals(_optionalTextHandling))
        {
            _optionalTokens[_optionalDepth].AddRange(LicenseTextHelper.TokenizeLicenseText(text, []));
        }
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#variableRule(org.spdx.licenseTemplate.LicenseTemplateRule)
     */
    public void VariableRule(LicenseTemplateRule rule)
    {
        if (VarTextHandling.REGEX.Equals(VarTextHandling) && _optionalDepth <= 0)
        {
            _currentString.Append(REGEX_ESCAPE);
            _currentString.Append('(');
            _currentString.Append(rule.Match);
            _currentString.Append(')');
            _currentString.Append(REGEX_ESCAPE);
        }
        else if (VarTextHandling.ORIGINAL.Equals(VarTextHandling) && _optionalDepth <= 0)
        {
            _currentString.Append(rule.Original);
        }
        else if (_optionalDepth > 0 && OptionalTextHandling.REGEX_USING_TOKENS.Equals(_optionalTextHandling))
        {
            _currentString.Append('(');
            _currentString.Append(rule.Match);
            _currentString.Append(')');
        }
        else
        {
            if (_currentString.Length > 0)
            {
                _filteredText.Add(_currentString.ToString());
                _currentString.Length = 0;
            }
        }
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#beginOptional(org.spdx.licenseTemplate.LicenseTemplateRule)
     */
    public void BeginOptional(LicenseTemplateRule rule)
    {
        if (OptionalTextHandling.REGEX_USING_TOKENS.Equals(_optionalTextHandling))
        {
            if (_optionalDepth == 0)
            {
                if (_currentString.Length > 0)
                {
                    _filteredText.Add(_currentString.ToString());
                    _currentString.Length = 0;
                }
                _currentString.Append(REGEX_ESCAPE);
            }
            else
            {
                _currentString.Append(ToTokenRegex(_optionalTokens[_optionalDepth]));
                _optionalTokens[_optionalDepth].Clear();
            }
            _currentString.Append('(');
        }
        else if (_currentString.Length > 0)
        {
            _filteredText.Add(_currentString.ToString());
            _currentString.Length = 0;
        }
        _optionalDepth++;
        _optionalTokens[_optionalDepth] = [];
    }

    /**
     * @return regular expression with quoted tokens
     */
    private static string ToTokenRegex(List<string> tokens)
    {
        StringBuilder sb = new StringBuilder();
        foreach (string token in tokens)
        {
            string t = token.Trim();
            if (LicenseTextHelper.NORMALIZE_TOKENS.ContainsKey(token.ToLower()))
            {
                t = LicenseTextHelper.NORMALIZE_TOKENS[token.ToLower()];
            }
            sb.Append(Regex.Escape(t));
            sb.Append("\\s*");
        }
        return sb.ToString();
    }


    /* (non-Javadoc)
	 * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#endOptional(org.spdx.licenseTemplate.LicenseTemplateRule)
	 */
    public void EndOptional(LicenseTemplateRule rule)
    {
        if (OptionalTextHandling.REGEX_USING_TOKENS.Equals(_optionalTextHandling))
        {
            _currentString.Append(ToTokenRegex(_optionalTokens[_optionalDepth]));
            _currentString.Append(")?");
            if (_optionalDepth == 1)
            {
                _currentString.Append(REGEX_ESCAPE);
                _filteredText.Add(_currentString.ToString());
                _currentString.Length = 0;
            }
        }
        else if (_currentString.Length > 0)
        {
            _filteredText.Add(_currentString.ToString());
            _currentString.Length = 0;
        }
        _optionalTokens.Remove(_optionalDepth);
        _optionalDepth--;
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#completeParsing()
     */
    public void CompleteParsing()
    {
        if (_currentString.Length > 0)
        {
            _filteredText.Add(_currentString.ToString());
            _currentString.Length = 0;
        }
    }

    /**
     * @return the includeVarText
     */
    public bool IsIncludeVarText()
    {
        return VarTextHandling.ORIGINAL.Equals(VarTextHandling);
    }
}
