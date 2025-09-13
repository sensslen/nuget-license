// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

    private const int MAX_NEXT_NORMAL_TEXT_SEARCH_LENGTH = 15;   // Maximum number of tokens to compare when searching for a normal text match
    private const int MIN_TOKENS_NORMAL_TEXT_SEARCH = 3; // Minimum number of tokens to match of normal text to match after a variable block to bound greedy regex var text

    private sealed class ParseInstruction
    {
        public LicenseTemplateRule? Rule { get; set; }
        public string? Text { get; set; }
        private readonly List<ParseInstruction> _subInstructions;
        public IReadOnlyList<ParseInstruction> SubInstructions => _subInstructions;
        public ParseInstruction? Parent { get; set; }

        public bool Skip { get; set; } = false;   // skip this instruction in matching
        public bool SkipFirstTextToken { get; set; } = false; // skip the first text token
        private DifferenceDescription? _lastOptionalDifference = null;
        private readonly IReadOnlyList<string> _compareTokens;

        /**
         * Construct a new {@link ParseInstruction} with the specified rule, text, and parent
         * <p>
         * A parse instruction represents a single unit of parsing logic, which may include a rule,
         * associated text, and a hierarchical relationship to a parent instruction.
         *
         * @param rule The {@link LicenseTemplateRule} associated with this parse instruction. Can
         *        be {@code null} if no rule is associated.
         * @param text The text content of this parse instruction. Can be {@code null} if no text is
         *        associated.
         * @param parent The parent {@link ParseInstruction} of this parse instruction. Can be
         *        {@code null} if this parse instruction has no parent.
         */
        public ParseInstruction(LicenseTemplateRule? rule, string? text, ParseInstruction? parent, IReadOnlyList<string> compareTokens)
        {
            Rule = rule;
            Text = text;
            _subInstructions = [];
            Parent = parent;
            _compareTokens = compareTokens;
        }

        /**
         * Return a string representation of this parse instruction
         * <p>
         * If the parse instruction has an associated rule, the rule's string representation is returned.
         * If the parse instruction has associated text, the first 10 characters of the text are returned.
         * If neither a rule nor text is associated, "NONE" is returned.
         *
         * @return A string representation of this parse instruction.
         */
        public override string ToString()
        {
            if (Rule != null)
            {
                return Rule.ToString();
            }
            else if (Text != null)
            {
                string retval = "TEXT: '";
                if (Text.Length > 10)
                {
                    retval = retval + Text.Substring(0, 10) + "...'";
                }
                else
                {
                    retval = retval + Text + "'";
                }
                return retval;
            }
            else
            {
                return "NONE";
            }
        }

        /**
         * Add the instruction to the list of sub-instructions
         *
         * @param instruction instruction to add
         */
        public void addSubInstruction(ParseInstruction instruction)
        {

            if (instruction.Rule != null && LicenseTemplateRule.RuleType.VARIABLE.Equals(instruction.Rule.Type) &&
                    _subInstructions.Any() &&
                    _subInstructions[^1].Rule is { } lastRule &&
                            LicenseTemplateRule.RuleType.VARIABLE.Equals(lastRule.Type))
            {
                // Maybe this is a little bit of a hack, but merge any var instructions so that
                // the match will work
                lastRule.Match = "(" + lastRule.Match + ")\\s*(" + instruction.Rule.Match + ")";
                lastRule.Name = "combined-" + lastRule.Name + "-" + instruction.Rule.Name;
                lastRule.Original = lastRule.Original + " " + lastRule.Original;
            }
            else
            {
                instruction.Parent = this;
                _subInstructions.Add(instruction);
            }
        }

        /**
         * Check whether all sub-instructions of this parse instruction contain only text
         *
         * @return {@code true} if all sub-instructions contain only text, {@code false} otherwise.
         *         Also returns {@code false} if there are no sub-instructions.
         */
        public bool onlyText()
        {
            if (!_subInstructions.Any())
            {
                return false;
            }
            foreach (ParseInstruction subInstr in _subInstructions)
            {
                if (subInstr.Text == null)
                {
                    return false;
                }
            }
            return true;
        }

        /**
         * Attempt to match this instruction against a tokenized array
         *
         * @param matchTokens Tokens to match the instruction against
         * @param startToken Index of the tokens to start the match
         * @param endToken Last index of the tokens to use in the match
         * @param originalText Original text used go generate the matchTokens
         * @param differences Description of differences found
         * @param tokenToLocation Map of the location of tokens
         * @return Next token index after the match or -1 if no match was found
         * @throws LicenseParserException On license parsing errors
         */
        public int match(IReadOnlyList<string> matchTokens, int startToken, int endToken, string originalText,
                DifferenceDescription differences, IDictionary<int, LineColumn> tokenToLocation)
        {
            return match(matchTokens, startToken, endToken, originalText, differences, tokenToLocation, false);
        }

        /**
         * Attempt to match this instruction against a tokenized array
         *
         * @param matchTokens Tokens to match the instruction against
         * @param startToken Index of the tokens to start the match
         * @param endToken Last index of the tokens to use in the match
         * @param originalText Original text used go generate the matchTokens
         * @param differences Description of differences found
         * @param tokenToLocation Map of the location of tokens
         * @param ignoreOptionalDifferences if true, don't record any optional differences
         * @return Next token index after the match or -1 if no match was found
         * @throws LicenseParserException On license parsing errors
         */
        public int match(IReadOnlyList<string> matchTokens, int startToken, int endToken, string originalText,
                DifferenceDescription differences, IDictionary<int, LineColumn> tokenToLocation, bool ignoreOptionalDifferences)
        {
            if (Skip)
            {
                return startToken;
            }

            int nextToken = startToken;
            if (Rule == null)
            {
                if (Text != null)
                {
                    IDictionary<int, LineColumn> textLocations = new Dictionary<int, LineColumn>();
                    IReadOnlyList<string> textTokens = LicenseTextHelper.tokenizeLicenseText(Text, textLocations);
                    if (SkipFirstTextToken)
                    {
                        textTokens = textTokens.Skip(1).ToArray();
                    }
                    nextToken = compareText(textTokens, matchTokens, nextToken, this, _compareTokens);
                    if (nextToken < 0)
                    {
                        int errorLocation = -nextToken - 1;
                        tokenToLocation.TryGetValue(errorLocation, out LineColumn? errorLine);
                        differences.AddDifference(errorLine, LicenseTextHelper.getTokenAt(matchTokens, errorLocation),
                                        "Normal text of license does not match", Text, null, getLastOptionalDifference());
                    }
                    if (_subInstructions.Any())
                    {
                        throw new LicenseParserException("License template parser error.  Sub expressions are not allows for plain text.");
                    }
                }
                else
                {
                    // just process the sub instructions
                    foreach (ParseInstruction sub in _subInstructions)
                    {
                        nextToken = sub.match(matchTokens, nextToken, endToken, originalText, differences,
                                tokenToLocation, ignoreOptionalDifferences);
                        if (nextToken < 0)
                        {
                            return nextToken;
                        }
                    }
                }

            }
            else if (Rule.Type.Equals(LicenseTemplateRule.RuleType.BEGIN_OPTIONAL))
            {
                if (Text != null)
                {
                    throw new LicenseParserException("License template parser error - can not have text associated with a begin optional rule");
                }
                if (onlyText() || Parent == null)
                {
                    // optimization, don't go through the effort to subset the text
                    foreach (ParseInstruction sub in _subInstructions)
                    {
                        DifferenceDescription optionalDifference = new DifferenceDescription();
                        nextToken = sub.match(matchTokens, nextToken, endToken, originalText,
                                optionalDifference, tokenToLocation);
                        if (nextToken < 0)
                        {
                            if (!ignoreOptionalDifferences)
                            {
                                setLastOptionalDifference(optionalDifference);
                            }
                            return startToken;  // the optional text didn't match, just return the start token
                        }
                    }
                }
                else
                {
                    IReadOnlyList<int> matchingNormalTextStartTokens = Parent.findNextNonVarTextStartTokens(this, matchTokens,
                            startToken, endToken, originalText, differences, tokenToLocation);
                    nextToken = matchOptional(matchingNormalTextStartTokens, matchTokens,
                            nextToken, originalText, tokenToLocation, ignoreOptionalDifferences);
                }
            }
            else if (Rule.Type.Equals(LicenseTemplateRule.RuleType.VARIABLE))
            {
                IReadOnlyList<int> matchingNormalTextStartTokens = Parent?.findNextNonVarTextStartTokens(this, matchTokens,
                        startToken, endToken, originalText, differences, tokenToLocation) ?? [];
                nextToken = matchVariable(matchingNormalTextStartTokens, matchTokens,
                        nextToken, originalText, differences, tokenToLocation);
            }
            else
            {
                throw new LicenseParserException("Unexpected parser state - instruction is not root, optional, variable or text");
            }
            return nextToken;
        }

        /**
         * Match to an optional rule
         *
         * @param matchingStartTokens List of indexes for the start tokens for the next normal text
         * @param matchTokens Tokens to match against
         * @param startToken Index of the first token to search for the match
         * @param originalText Original text used go generate the matchTokens
         * @param tokenToLocation Map of token index to line/column where the token was found in the original text
         * @param ignoreOptionalDifferences if true, don't record any optional differences
         * @return the index of the token after the find or -1 if the text did not match
         * @throws LicenseParserException On license parsing errors
         */
        private int matchOptional(IReadOnlyList<int> matchingStartTokens,
                                  IReadOnlyList<string> matchTokens, int startToken, string originalText,
                                  IDictionary<int, LineColumn> tokenToLocation, bool ignoreOptionalDifferences)
        {
            foreach (int matchingStartToken in matchingStartTokens)
            {
                DifferenceDescription matchDifferences = new DifferenceDescription();
                int matchLocation = startToken;
                foreach (ParseInstruction sub in _subInstructions)
                {
                    matchLocation = sub.match(matchTokens, matchLocation, matchingStartToken - 1, originalText,
                            matchDifferences, tokenToLocation);
                    if (matchLocation < 0)
                    {
                        break;
                    }
                }
                if (matchLocation > 0)
                {
                    return matchLocation;   // found a match
                }
                else if (!ignoreOptionalDifferences)
                {
                    setLastOptionalDifference(matchDifferences);
                }
            }
            // We didn't find any matches, return the original start token
            return startToken;
        }

        /**
         * Find the indexes that match the matching optional or first normal text within the sub-instructions
         *
         * @param afterChild the child after which to start searching for the first normal text
         * @param matchTokens Tokens used to match the text against
         * @param startToken Start of the match tokens to begin the search
         * @param endToken End of the match tokens to end the search
         * @param originalText original text that created the match tokens
         * @param differences Information on any differences found
         * @param tokenToLocation Map of match token indexes to line/column locations
         * @return List of indexes for the start tokens for the next non-variable text that matches
         * @throws LicenseParserException On license parsing errors
         */
        private IReadOnlyList<int> findNextNonVarTextStartTokens(ParseInstruction afterChild,
                IReadOnlyList<string> matchTokens, int startToken, int endToken, string originalText,
                DifferenceDescription differences, IDictionary<int, LineColumn> tokenToLocation)
        {
            List<int> retval = [];
            // We find the first index to start our search
            int indexOfChild = _subInstructions.IndexOf(afterChild);
            if (indexOfChild < 0)
            {
                throw new LicenseParserException("Template Parser Error: Could not locate sub instruction");
            }
            int startSubInstructionIndex = indexOfChild + 1;
            if (startSubInstructionIndex >= _subInstructions.Count)
            {
                // no start tokens found
                // Set return value to the end
                retval.Add(endToken + 1);
                return retval;
            }
            int firstNormalTextIndex = -1;  // initial value for not yet found
                                            // keep track of all optional rules prior to the first solid normal text since the optional
                                            // rules can provide a valid result
            List<int> leadingOptionalSubInstructions = [];
            int i = startSubInstructionIndex;
            while (i < _subInstructions.Count && firstNormalTextIndex < 0)
            {
                LicenseTemplateRule? subInstructionRule = _subInstructions[i].Rule;
                if (subInstructionRule != null && subInstructionRule.Type == LicenseTemplateRule.RuleType.BEGIN_OPTIONAL)
                {
                    leadingOptionalSubInstructions.Add(i);
                }
                else if (_subInstructions[i].Text != null && !string.IsNullOrWhiteSpace(_subInstructions[i].Text))
                {
                    firstNormalTextIndex = i;
                }
                i++;
            }
            int nextMatchingStart = startToken;
            // Go through the preceding optional rules.  If there is enough token matches, add it to the result list
            foreach (int optionalSub in leadingOptionalSubInstructions)
            {
                DifferenceDescription tempDiffDescription = new DifferenceDescription();
                int nextOptMatchingStart = nextMatchingStart;
                int optTokenAfterMatch = _subInstructions[optionalSub].match(matchTokens, nextOptMatchingStart, endToken, originalText, tempDiffDescription, tokenToLocation, true);
                while (optTokenAfterMatch <= nextOptMatchingStart && -optTokenAfterMatch <= endToken
                        && !tempDiffDescription.DifferenceFound && nextOptMatchingStart <= endToken)
                {
                    // while we didn't find a match
                    nextOptMatchingStart++;
                    optTokenAfterMatch = _subInstructions[optionalSub].match(matchTokens, nextOptMatchingStart, endToken, originalText, tempDiffDescription, tokenToLocation, true);
                }
                if (optTokenAfterMatch > 0 && !tempDiffDescription.DifferenceFound && nextOptMatchingStart <= endToken)
                {
                    // we found a match
                    if (optTokenAfterMatch - nextOptMatchingStart > MIN_TOKENS_NORMAL_TEXT_SEARCH)
                    {
                        // Only add possible matches if it matched enough tokens
                        retval.Add(nextOptMatchingStart);
                    }
                    nextMatchingStart = optTokenAfterMatch;
                }
            }
            if (firstNormalTextIndex < 0)
            {
                // Set to the end
                retval.Add(endToken + 1);
                return retval;
            }

            IDictionary<int, LineColumn> normalTextLocations = new Dictionary<int, LineColumn>();
            IReadOnlyList<string> textTokens = LicenseTextHelper.tokenizeLicenseText(_subInstructions[firstNormalTextIndex].Text ?? string.Empty, normalTextLocations);
            if (textTokens.Count > MAX_NEXT_NORMAL_TEXT_SEARCH_LENGTH)
            {
                textTokens = textTokens.Take(MAX_NEXT_NORMAL_TEXT_SEARCH_LENGTH).ToArray();
            }

            int tokenAfterMatch = compareText(textTokens, matchTokens, nextMatchingStart, null, _compareTokens);
            bool foundEnoughTokens = false;
            while (!foundEnoughTokens && nextMatchingStart <= endToken && !differences.DifferenceFound)
            {
                while (tokenAfterMatch < 0 && -tokenAfterMatch <= endToken)
                {
                    nextMatchingStart = nextMatchingStart + 1;
                    tokenAfterMatch = compareText(textTokens, matchTokens, nextMatchingStart, null, _compareTokens);
                }
                if (tokenAfterMatch < 0)
                {
                    // Can not find the text, report a difference
                    string ruleDesc = "variable or optional rule";
                    if (afterChild.Rule != null)
                    {
                        if (afterChild.Rule.Type == LicenseTemplateRule.RuleType.BEGIN_OPTIONAL)
                        {
                            ruleDesc = "optional rule";
                        }
                        else if (afterChild.Rule.Type == LicenseTemplateRule.RuleType.VARIABLE)
                        {
                            ruleDesc = "variable rule '" + afterChild.Rule.Name + "'";
                        }
                    }
                    tokenToLocation.TryGetValue(nextMatchingStart, out LineColumn? nextMatchingLine);
                    differences.AddDifference(nextMatchingLine, "",
                            "Unable to find the text '" + _subInstructions[firstNormalTextIndex].Text + "' following a " + ruleDesc,
                                    null, Rule, getLastOptionalDifference());
                }
                else if (textTokens.Count >= MIN_TOKENS_NORMAL_TEXT_SEARCH)
                {
                    retval.Add(nextMatchingStart);
                    foundEnoughTokens = true;
                }
                else
                {
                    // Not enough text tokens, we need to make sure everything matches beyond this point
                    DifferenceDescription tempDiffDescription = new DifferenceDescription();
                    int nextCheckToken = _subInstructions[firstNormalTextIndex].match(matchTokens, nextMatchingStart, endToken, originalText, tempDiffDescription, tokenToLocation, true);
                    int nextCheckSubInstruction = firstNormalTextIndex + 1;
                    while (nextCheckToken > 0 &&
                            nextCheckToken - tokenAfterMatch < MIN_TOKENS_NORMAL_TEXT_SEARCH &&
                            nextCheckSubInstruction < _subInstructions.Count)
                    {
                        nextCheckToken = _subInstructions[nextCheckSubInstruction++].match(matchTokens, nextCheckToken, endToken, originalText, tempDiffDescription, tokenToLocation, true);
                    }
                    if (nextCheckToken < 0)
                    {
                        // we didn't match enough, move on to the next
                        nextMatchingStart = nextMatchingStart + 1;
                        tokenAfterMatch = compareText(textTokens, matchTokens, nextMatchingStart, null, _compareTokens);
                    }
                    else
                    {
                        retval.Add(nextMatchingStart);
                        foundEnoughTokens = true;
                    }
                }
            }
            return retval;
        }

        /**
		 * Determine the number of tokens matched from the compare text
		 *
		 * @param text text to search
		 * @param end End of matching text
		 * @return number of tokens in the text
		 */
        private static int numTokensMatched(string text, int end)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }
            if (end == 0)
            {
                return 0;
            }
            IDictionary<int, LineColumn> temp = new Dictionary<int, LineColumn>();
            string subText = text.Substring(0, end);
            IReadOnlyList<string> tokenizedString = LicenseTextHelper.tokenizeLicenseText(subText, temp);
            return tokenizedString.Count;
        }

        /**
         * Match to a variable rule
         *
         * @param matchingStartTokens List of indexes for the start tokens for the next normal text
         * @param matchTokens Tokens to match against
         * @param startToken Index of the first token to search for the match
         * @param originalText Original text used go generate the matchTokens
         * @param differences Any differences found
         * @param tokenToLocation Map of token index to line/column where the token was found in the original text
         * @return the index of the token after the find or -1 if the text did not match
         */
        private int matchVariable(IReadOnlyList<int> matchingStartTokens, IReadOnlyList<string> matchTokens, int startToken,
                                  string originalText, DifferenceDescription differences, IDictionary<int, LineColumn> tokenToLocation)
        {

            if (differences.DifferenceFound)
            {
                return -1;
            }
            foreach (int matchingStartToken in matchingStartTokens)
            {
                string compareText = LicenseCompareHelper.locateOriginalText(originalText, startToken, matchingStartToken - 1, tokenToLocation, matchTokens);
                Match match = Regex.Match(compareText, Rule?.Match ?? string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (match.Success && match.Index <= 0)
                {
                    int numMatched = numTokensMatched(compareText, match.Index + match.Length);
                    return startToken + numMatched;
                }
            }
            // if we got here, there was no match found
            tokenToLocation.TryGetValue(startToken, out LineColumn? difference);
            differences.AddDifference(difference, LicenseTextHelper.getTokenAt(matchTokens, startToken), "Variable text rule " + Rule?.Name + " did not match the compare text",
                    null, Rule, getLastOptionalDifference());
            return -1;
        }

        /**
		 * Retrieve the difference description for the last optional rule that did not match
		 *
		 * @return A {@link DifferenceDescription} object representing the last optional difference,
		 *         or {@code null} if no optional difference was found.
		 */
        public DifferenceDescription? getLastOptionalDifference()
        {
            if (_lastOptionalDifference != null)
            {
                return _lastOptionalDifference;
            }
            else if (Parent != null)
            {
                return Parent.getLastOptionalDifference();
            }
            else
            {
                return null;
            }
        }

        /**
         * Set the last optional difference that did not match.
         *
         * @param optionalDifference A {@link DifferenceDescription} object representing the last
         *        optional difference. This must not be {@code null}, and it must have a non-empty
         *        difference message.
         */
        public void setLastOptionalDifference(DifferenceDescription? optionalDifference)
        {
            if (optionalDifference != null && optionalDifference.DifferenceMessage != null
                    && !string.IsNullOrEmpty(optionalDifference.DifferenceMessage))
            {
                _lastOptionalDifference = optionalDifference;
                Parent?.setLastOptionalDifference(optionalDifference);
            }
        }

        /**
         * Determine if the instruction following this one is an optional rule containing text with
         * a single token
         *
         * @return {@code true} if the instruction following this instruction is a
         *         {@code beginOptional} rule containing text with a single token, {@code false}
         *         otherwise.
         */
        public bool isFollowingInstructionOptionalSingleToken()
        {
            if (Parent == null)
            {
                return false;
            }
            ParseInstruction? nextInstruction = Parent.findFollowingInstruction(this);
            if (nextInstruction == null || nextInstruction.Rule == null)
            {
                return false;
            }
            else
            {
                if (!LicenseTemplateRule.RuleType.BEGIN_OPTIONAL.Equals(nextInstruction.Rule.Type))
                {
                    return false;
                }
                if (nextInstruction.SubInstructions.Count != 1)
                {
                    return false;
                }
                string? optionalText = nextInstruction.SubInstructions[0].Text;
                return LicenseCompareHelper.isSingleTokenString(optionalText);
            }
        }

        /**
         * Find the next parse instruction that follows the given parse instruction in the list of
         * sub-instructions
         *
         * @param parseInstruction subInstruction to find the next parse instruction after.
         * @return The next instruction after parseInstruction in the subInstructions, or
         *         {@code null} if no such instruction exists.
         */
        private ParseInstruction? findFollowingInstruction(ParseInstruction parseInstruction)
        {
            if (parseInstruction == null)
            {
                return null;
            }
            for (int i = 0; i < _subInstructions.Count; i++)
            {
                if (parseInstruction.Equals(_subInstructions[i]))
                {
                    if (_subInstructions.Count > i + 1)
                    {
                        return _subInstructions[i + 1];
                    }
                    else if (Parent == null)
                    {
                        return null;
                    }
                    else
                    {
                        return Parent.findFollowingInstruction(this);
                    }
                }
            }
            return null;    // instruction not found
        }

        /**
         * Retrieve the tokens from the next group of optional text
         *
         * @return The tokens from the next group of optional.
         */
        public IReadOnlyList<string> getNextOptionalTextTokens()
        {
            if (Parent == null)
            {
                return [];
            }
            ParseInstruction? nextInstruction = Parent.findFollowingInstruction(this);
            if (nextInstruction == null || nextInstruction.Rule == null)
            {
                return [];
            }
            else
            {
                if (!LicenseTemplateRule.RuleType.BEGIN_OPTIONAL.Equals(nextInstruction.Rule.Type))
                {
                    return [];
                }
                StringBuilder sb = new StringBuilder();
                foreach (string text in nextInstruction.SubInstructions.Select(i => i.Text).Where(t => t is not null)!)
                {
                    sb.Append(text);
                }
                IDictionary<int, LineColumn> temp = new Dictionary<int, LineColumn>();
                return LicenseTextHelper.tokenizeLicenseText(sb.ToString(), temp);
            }
        }

        /**
		 * Skip the next instruction
		 */
        public void skipNextInstruction()
        {
            if (Parent == null)
            {
                return;
            }
            ParseInstruction? nextInst = Parent.findFollowingInstruction(this);
            if (nextInst is not null)
            {
                nextInst.Skip = true;
            }
        }

        /**
         * Retrieve the next sibling parse instruction that contains only text (no rules)
         *
         * @return The next sibling parse instruction which is just text (no rules).
         */
        public ParseInstruction? getNextNormalTextInstruction()
        {
            if (Parent == null)
            {
                return null;
            }
            IReadOnlyList<ParseInstruction> siblings = Parent.SubInstructions;
            int mySiblingIndex = -1;
            for (int i = 0; i < siblings.Count; i++)
            {
                if (Equals(siblings[i]))
                {
                    mySiblingIndex = i;
                    break;
                }
            }
            if (mySiblingIndex < 0)
            {
                return null;
            }
            int nextOptionalIndex = -1;
            for (int i = mySiblingIndex + 1; i < siblings.Count; i++)
            {
                if (siblings[i].Rule != null && LicenseTemplateRule.RuleType.BEGIN_OPTIONAL.Equals(siblings[i].Rule?.Type))
                {
                    nextOptionalIndex = i;
                    break;
                }
            }
            if (nextOptionalIndex > 0)
            {
                for (int i = nextOptionalIndex + 1; i < siblings.Count; i++)
                {
                    if (siblings[i].Text != null)
                    {
                        return siblings[i];
                    }
                }
                return null; // Note - we could go up to the parent to look for the next text token, but this is getting messy enough as it is
            }
            else
            {
                return Parent.getNextNormalTextInstruction();
            }
        }
    }

    private readonly IReadOnlyList<string> _compareTokens;
    private readonly string _compareText;
    private readonly IDictionary<int, LineColumn> _tokenToLocation = new Dictionary<int, LineColumn>();
    private readonly ParseInstruction _topLevelInstruction;
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
        _topLevelInstruction = new ParseInstruction(null, null, null, _compareTokens);
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
    private static int compareText(IReadOnlyList<string> textTokens, IReadOnlyList<string> matchTokens, int startToken,
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
            _currentOptionalInstruction.addSubInstruction(new ParseInstruction(null, text, _currentOptionalInstruction, _compareTokens));
        }
        else
        {
            _topLevelInstruction.addSubInstruction(new ParseInstruction(null, text, null, _compareTokens));
        }
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#variableRule(org.spdx.licenseTemplate.LicenseTemplateRule)
     */
    public void variableRule(LicenseTemplateRule rule)
    {
        if (_currentOptionalInstruction != null)
        {
            _currentOptionalInstruction.addSubInstruction(new ParseInstruction(rule, null, _currentOptionalInstruction, _compareTokens));
        }
        else
        {
            _topLevelInstruction.addSubInstruction(new ParseInstruction(rule, null, null, _compareTokens));
        }
    }


    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#beginOptional(org.spdx.licenseTemplate.LicenseTemplateRule)
     */
    public void beginOptional(LicenseTemplateRule rule)
    {
        ParseInstruction optionalInstruction = new ParseInstruction(rule, null, _currentOptionalInstruction, _compareTokens);
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
        int nextTokenIndex = _topLevelInstruction.match(_compareTokens, 0, _compareTokens.Count - 1, _compareText, Differences, _tokenToLocation);
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
