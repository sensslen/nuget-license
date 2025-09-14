// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Linq;
using SPDXLicenseMatcher.JavaCore;

/**
 * SPDX-FileCopyrightText: Copyright (c) 2019 Source Auditor Inc.
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
namespace SPDXLicenseMatcher.JavaLibrary;

/**
 * Compares the output of a parsed license template to text.  The method matches is called after
 * the document is parsed to determine if the text matches.
 * 
 * @author Gary O'Neall
 */
public class CompareTemplateOutputHandler : ILicenseTemplateOutputHandler
{
    private readonly IReadOnlyList<string> _compareTokens;
    private readonly string _compareText;
    private readonly IDictionary<int, LineColumn> _tokenToLocation = new Dictionary<int, LineColumn>();
    private readonly ParseInstruction _topLevelInstruction = new ParseInstruction(null, null);
    public DifferenceDescription Differences { get; } = new DifferenceDescription();
    private ParseInstruction? _currentOptionalInstruction = null;
    private bool _parsingComplete = false;

    /**
     * Construct a new {@link CompareTemplateOutputHandler} with the specified text to compare
     * <p>
     * This handler is used to compare the output of a parsed SPDX license template to the provided
     * text. It tokenizes the input text and prepares it for comparison against the parsed license
     * template.
     *
     * @param compareText The text to compare the parsed SPDX license template to.
     * @throws IOException This is not to be expected since we are using StringReaders
     */
    public CompareTemplateOutputHandler(string compareText)
    {
        _compareText = LicenseTextHelper.normalizeText(
                LicenseTextHelper.replaceMultWord(LicenseTextHelper.replaceSpaceComma(compareText)));
        _compareTokens = LicenseTextHelper.tokenizeLicenseText(_compareText, _tokenToLocation);
    }

    /**
     * Compares the given text tokens against the match tokens starting from a specific token index
     *
     * @param textTokens source for compare
     * @param matchTokens tokens to match against
     * @param startToken index for the start token
     * @param instruction parse instruction
     * @return positive index of the next match token after the match or negative index of the token
     *         which first failed the match
     */
    public static int compareText(IReadOnlyList<string> textTokens, IReadOnlyList<string> matchTokens, int startToken,
                            ParseInstruction? instruction, IReadOnlyList<string> compareTokens)
    {
        if (textTokens.Count == 0)
        {
            return startToken;
        }
        int textTokenCounter = 0;
        string? nextTextToken = LicenseTextHelper.getTokenAt(textTokens, textTokenCounter++);
        int matchTokenCounter = startToken;
        string? nextMatchToken = LicenseTextHelper.getTokenAt(matchTokens, matchTokenCounter++);
        while (nextTextToken != null)
        {
            if (nextMatchToken == null)
            {
                // end of compare text stream
                while (LicenseTextHelper.canSkip(nextTextToken))
                {
                    nextTextToken = LicenseTextHelper.getTokenAt(textTokens, textTokenCounter++);
                }
                if (nextTextToken != null)
                {
                    return -matchTokenCounter;  // there is more stuff in the compare license text, so not equiv.
                }
            }
            else if (LicenseTextHelper.tokensEquivalent(nextTextToken, nextMatchToken))
            {
                // just move onto the next set of tokens
                nextTextToken = LicenseTextHelper.getTokenAt(textTokens, textTokenCounter++);
                if (nextTextToken != null)
                {
                    nextMatchToken = LicenseTextHelper.getTokenAt(matchTokens, matchTokenCounter++);
                }
            }
            else
            {
                // see if we can skip through some compare tokens to find a match
                while (LicenseTextHelper.canSkip(nextMatchToken))
                {
                    nextMatchToken = LicenseTextHelper.getTokenAt(matchTokens, matchTokenCounter++);
                }
                // just to be sure, skip forward on the text
                while (LicenseTextHelper.canSkip(nextTextToken))
                {
                    nextTextToken = LicenseTextHelper.getTokenAt(textTokens, textTokenCounter++);
                }
                if (LicenseTextHelper.tokensEquivalent(nextMatchToken, nextTextToken))
                {
                    nextTextToken = LicenseTextHelper.getTokenAt(textTokens, textTokenCounter++);
                    if (nextTextToken != null)
                    {
                        nextMatchToken = LicenseTextHelper.getTokenAt(compareTokens, matchTokenCounter++);
                    }
                }
                else
                {
                    if (textTokenCounter == textTokens.Count &&
                            instruction != null &&
                            instruction.isFollowingInstructionOptionalSingleToken() &&
                            nextMatchToken != null)
                    {
                        //This is the special case where there may be optional characters which are
                        //less than a token at the end of a compare
                        //Yes - this is a bit of a hack
                        IReadOnlyList<string> nextOptionalTextTokens = instruction.getNextOptionalTextTokens();
                        string compareToken = nextTextToken + (nextOptionalTextTokens.Any() ? nextOptionalTextTokens[0] : 0);
                        if (LicenseTextHelper.tokensEquivalent(compareToken, nextMatchToken))
                        {
                            instruction.skipNextInstruction();
                            return matchTokenCounter;
                        }
                        else
                        {
                            ParseInstruction? nextNormal = instruction.getNextNormalTextInstruction();
                            string? nextNormalText = LicenseCompareHelper.getFirstLicenseToken(nextNormal?.Text ?? string.Empty);
                            if (nextNormalText != null)
                            {
                                compareToken = compareToken + nextNormalText;
                                string compareWithoutOptional = nextTextToken + nextNormalText;
                                if (LicenseTextHelper.tokensEquivalent(compareToken, nextMatchToken) ||
                                        LicenseTextHelper.tokensEquivalent(compareWithoutOptional, nextMatchToken))
                                {
                                    instruction.skipNextInstruction();
                                    nextNormal!.SkipFirstTextToken = true;
                                    return matchTokenCounter;
                                }
                            }
                        }
                    }
                    return -matchTokenCounter;
                }
            }
        }
        return matchTokenCounter;
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#text(java.lang.String)
     */
    public void text(string text)
    {
        if (_currentOptionalInstruction != null)
        {
            _currentOptionalInstruction.addSubInstruction(new ParseInstruction(null, text));
        }
        else
        {
            _topLevelInstruction.addSubInstruction(new ParseInstruction(null, text));
        }
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#variableRule(org.spdx.licenseTemplate.LicenseTemplateRule)
     */
    public void variableRule(LicenseTemplateRule rule)
    {
        if (_currentOptionalInstruction != null)
        {
            _currentOptionalInstruction.addSubInstruction(new ParseInstruction(rule, null));
        }
        else
        {
            _topLevelInstruction.addSubInstruction(new ParseInstruction(rule, null));
        }
    }


    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#beginOptional(org.spdx.licenseTemplate.LicenseTemplateRule)
     */
    public void beginOptional(LicenseTemplateRule rule)
    {
        ParseInstruction optionalInstruction = new ParseInstruction(rule, null);
        if (_currentOptionalInstruction != null)
        {
            _currentOptionalInstruction.addSubInstruction(optionalInstruction);
        }
        else
        {
            _topLevelInstruction.addSubInstruction(optionalInstruction);
        }
        _currentOptionalInstruction = optionalInstruction;
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#endOptional(org.spdx.licenseTemplate.LicenseTemplateRule)
     */
    public void endOptional(LicenseTemplateRule rule)
    {
        if (_currentOptionalInstruction != null)
        {
            _currentOptionalInstruction = _currentOptionalInstruction.Parent;
            if (_currentOptionalInstruction == null || _currentOptionalInstruction.Rule == null || _currentOptionalInstruction.Rule.Type != LicenseTemplateRule.RuleType.BEGIN_OPTIONAL)
            {
                _currentOptionalInstruction = null;
            }
        }
    }

    /**
     * Performs the actual parsing if it has not been completed
     * <p>
     * NOTE: This should only be called after all text has been added.
     *
     * @return true if no differences were found
     * @throws LicenseParserException on license parsing error
     */
    public bool matches()
    {
        if (!_parsingComplete)
        {
            throw new LicenseParserException("Matches was called prior to completing the parsing.  The method <code>competeParsing()</code> most be called prior to calling <code>matches()</code>");
        }
        return !Differences.DifferenceFound;
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#completeParsing()
     */
    public void completeParsing()
    {
        int nextTokenIndex = _topLevelInstruction.match(_compareTokens, 0, _compareTokens.Count - 1, _compareText, Differences, _tokenToLocation, _compareTokens);
        if (nextTokenIndex > 0 && nextTokenIndex < _compareTokens.Count)
        {
            Differences.AddDifference(_tokenToLocation[nextTokenIndex],
                    LicenseTextHelper.getTokenAt(_compareTokens, nextTokenIndex),
                    "Additional text found after the end of the expected license text", null, null, null);
        }
        _parsingComplete = true;
    }

    /**
     * Compares the text against the compareText
     *
     * @param text text to compare
     * @param startToken token of the compareText to being the comparison
     * @return next token index (positive) if there is a match, negative first token where this is a miss-match if no match
     */
    public int textEquivalent(string text, int startToken)
    {
        IDictionary<int, LineColumn> textLocations = new Dictionary<int, LineColumn>();
        IReadOnlyList<string> textTokens = LicenseTextHelper.tokenizeLicenseText(text, textLocations);
        return compareText(textTokens, _compareTokens, startToken, null, _compareTokens);
    }
}
