// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

/**
 * Copyright (c) 2013 Source Auditor Inc.
 *
 *   Licensed under the Apache License, Version 2.0 (the "License");
 *   you may not use this file except in compliance with the License.
 *   You may obtain a copy of the License at
 *
 *       http://www.apache.org/licenses/LICENSE-2.0
 *
 *   Unless required by applicable law or agreed to in writing, software
 *   distributed under the License is distributed on an "AS IS" BASIS,
 *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *   See the License for the specific language governing permissions and
 *   limitations under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SPDXLicenseMatcher.JavaPort;

/**
 * Compares the output of a parsed license template to text.  The method matches is called after
 * the document is parsed to determine if the text matches.
 * @author Gary O'Neall
 *
 */
public class CompareTemplateOutputHandler : ILicenseTemplateOutputHandler
{
    private const int MAX_NEXT_NORMAL_TEXT_SEARCH_LENGTH = 15;   // Maximum number of tokens to compare when searching for a normal text match
    private const int MIN_TOKENS_NORMAL_TEXT_SEARCH = 3; // Minimum number of tokens to match of normal text to match after a variable block to bound greedy regex var text

    /**
     * Locate the original text starting with the start token and ending with the end token
     * @param fullLicenseText
     * @param startToken
     * @param endToken
     * @param tokenToLocation
     * @return
     */
    public static string LocateOriginalText(string fullLicenseText, int startToken, int endToken,
            Dictionary<int, LineColumn> tokenToLocation, string[] tokens)
    {
        if (startToken > endToken)
        {
            return "";
        }
        LineColumn start = tokenToLocation[startToken];
        if (start == null)
        {
            return "";
        }
        LineColumn end = tokenToLocation[endToken];
        // If end == null, then we read to the end
        using var reader = new StringReader(fullLicenseText);
        try
        {
            int currentLine = 1;
            string? line = reader.ReadLine();
            while (line != null && currentLine < start.Line)
            {
                currentLine++;
                line = reader.ReadLine();
            }
            if (line == null)
            {
                return "";
            }
            if (end == null)
            {
                // read until the end of the stream
                StringBuilder sb = new StringBuilder(line.Substring(start.Column, line.Length - start.Column));
                currentLine++;
                line = reader.ReadLine();
                while (line != null)
                {
                    sb.Append(line);
                    currentLine++;
                    line = reader.ReadLine();
                }
                return sb.ToString();
            }
            else if (end.Line == currentLine)
            {
                return line.Substring(start.Column, end.Column + end.Length - start.Column);
            }
            else
            {
                StringBuilder sb = new StringBuilder(line.Substring(start.Column, line.Length - start.Column));
                currentLine++;
                line = reader.ReadLine();
                while (line != null && currentLine < end.Line)
                {
                    sb.Append("\n");
                    sb.Append(line);
                    currentLine++;
                    line = reader.ReadLine();
                }
                if (line != null && end.Column + end.Length > 0)
                {
                    sb.Append("\n");
                    sb.Append(line.Substring(0, end.Column + end.Length));
                }
                return sb.ToString();
            }
        }
        catch (Exception)
        {
            // just build with spaces - not ideal, but close enough most of the time
            StringBuilder sb = new StringBuilder(tokens[startToken]);
            for (int i = startToken + 1; i <= endToken; i++)
            {
                sb.Append(' ');
                sb.Append(tokens[i]);
            }
            return sb.ToString();
        }
    }

    private sealed class ParseInstruction
    {
        public LicenseTemplateRule? Rule { get; set; }
        public string? Text { get; set; }
        public IReadOnlyList<ParseInstruction> SubInstructions => _subInstructions;
        public ParseInstruction? Parent { get; set; }

        public readonly List<ParseInstruction> _subInstructions;
        private readonly string[] _compareTokens;

        public bool Skip { get; set; } = false;   // skip this instruction in matching
        public bool SkipFirstTextToken { get; set; } = false; // skip the first text token
        private DifferenceDescription? _lastOptionalDifference = null;

        public ParseInstruction(LicenseTemplateRule? rule, string? text, ParseInstruction? parent, string[] compareTokens)
        {
            Rule = rule;
            Text = text;
            _subInstructions = [];
            Parent = parent;
            _compareTokens = compareTokens;
        }

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
         * @param instruction
         */
        public void AddSubInstruction(ParseInstruction instruction)
        {
            if (instruction.Rule != null && LicenseTemplateRule.RuleType.Variable.Equals(instruction.Rule.Type) &&
                    SubInstructions.Count > 0 &&
                    SubInstructions[^1].Rule is { } lastRule &&
                    LicenseTemplateRule.RuleType.Variable.Equals(lastRule.Type))
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
         * @return true iff there are only text instructions as sub instructions
         */
        public bool OnlyText()
        {
            if (SubInstructions.Count < 1)
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
         * @param matchTokens Tokens to match the instruction against
         * @param startToken Index of the tokens to start the match
         * @param endToken Last index of the tokens to use in the match
         * @param originalText Original text used go generate the matchTokens
         * @param differenceDescription Description of differences found
         * @param nextNormalText if there is a nextOptionalText, this would be the normal text that follows the optional text
         * @return Next token index after the match or -1 if no match was found
         * @throws LicenseParserException
         */
        public int Match(string[] matchTokens, int startToken, int endToken, string originalText,
                DifferenceDescription differences, Dictionary<int, LineColumn> tokenToLocation)
        {
            return Match(matchTokens, startToken, endToken, originalText, differences, tokenToLocation, false);
        }

        /**
         * Attempt to match this instruction against a tokenized array
         * @param matchTokens Tokens to match the instruction against
         * @param startToken Index of the tokens to start the match
         * @param endToken Last index of the tokens to use in the match
         * @param originalText Original text used go generate the matchTokens
         * @param differenceDescription Description of differences found
         * @param nextNormalText if there is a nextOptionalText, this would be the normal text that follows the optional text
         * @param ignoreOptionalDifferences if true, don't record any optional differences
         * @return Next token index after the match or -1 if no match was found
         * @throws LicenseParserException
         */
        public int Match(string[] matchTokens, int startToken, int endToken, string originalText,
                DifferenceDescription differences, Dictionary<int, LineColumn> tokenToLocation, bool ignoreOptionalDifferences)
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
                    var textLocations = new Dictionary<int, LineColumn>();
                    string[] textTokens = ToolsLicenseCompareHelper.TokenizeLicenseText(ToolsLicenseCompareHelper.NormalizeText(Text), textLocations);
                    if (SkipFirstTextToken)
                    {
                        textTokens = textTokens.Skip(1).ToArray();
                    }
                    nextToken = CompareText(textTokens, matchTokens, nextToken, this, _compareTokens);
                    if (nextToken < 0)
                    {
                        int errorLocation = -nextToken;
                        tokenToLocation.TryGetValue(errorLocation, out LineColumn? errorLine);
                        differences.AddDifference(errorLine, ToolsLicenseCompareHelper.GetTokenAt(matchTokens, errorLocation),
                                        "Normal text of license does not match", Text, null, _lastOptionalDifference);
                    }
                    if (SubInstructions.Count > 0)
                    {
                        throw new LicenseParserException("License template parser error.  Sub expressions are not allows for plain text.");
                    }
                }
                else
                {
                    // just process the sub instructions
                    foreach (ParseInstruction sub in SubInstructions)
                    {
                        nextToken = sub.Match(matchTokens, nextToken, endToken, originalText, differences,
                                tokenToLocation, ignoreOptionalDifferences);
                        if (nextToken < 0)
                        {
                            return nextToken;
                        }
                    }
                }

            }
            else if (Rule.Type.Equals(LicenseTemplateRule.RuleType.BeginOptional))
            {
                if (Text != null)
                {
                    throw new LicenseParserException("License template parser error - can not have text associated with a begin optional rule");
                }
                if (OnlyText() || Parent == null)
                {
                    // optimization, don't go through the effort to subset the text
                    foreach (ParseInstruction sub in SubInstructions)
                    {
                        DifferenceDescription optionalDifference = new DifferenceDescription();
                        nextToken = sub.Match(matchTokens, nextToken, endToken, originalText,
                                optionalDifference, tokenToLocation);
                        if (nextToken < 0)
                        {
                            if (!ignoreOptionalDifferences)
                            {
                                _lastOptionalDifference = optionalDifference;
                            }
                            return startToken;  // the optional text didn't match, just return the start token
                        }
                    }
                }
                else
                {
                    List<int> matchingNormalTextStartTokens = Parent.FindNextNonVarTextStartTokens(this, matchTokens,
                            startToken, endToken, originalText, differences, tokenToLocation);
                    nextToken = MatchOptional(matchingNormalTextStartTokens, matchTokens,
                            nextToken, originalText, tokenToLocation, ignoreOptionalDifferences);
                }
            }
            else if (Rule.Type.Equals(LicenseTemplateRule.RuleType.Variable))
            {
                List<int> matchingNormalTextStartTokens = Parent?.FindNextNonVarTextStartTokens(this, matchTokens,
                        startToken, endToken, originalText, differences, tokenToLocation) ?? [];
                nextToken = MatchVariable(matchingNormalTextStartTokens, matchTokens,
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
         * @param optionalInstruction Optional Instruction
         * @param matchingStartTokens List of indexes for the start tokens for the next normal text
         * @param matchTokens Tokens to match against
         * @param startToken Index of the first token to search for the match
         * @param endToken Index of the last token to search for the match
         * @param originalText Original text used go generate the matchTokens
         * @param differences Any differences found
         * @param tokenToLocation Map of token index to line/column where the token was found in the original text
         *  @param ignoreOptionalDifferences if true, don't record any optional differences
         * @return the index of the token after the find or -1 if the text did not match
         * @throws LicenseParserException
         */
        private int MatchOptional(List<int> matchingStartTokens,
                string[] matchTokens, int startToken, string originalText, Dictionary<int, LineColumn> tokenToLocation, bool ignoreOptionalDifferences)
        {
            foreach (int matchingStartToken in matchingStartTokens)
            {
                DifferenceDescription matchDifferences = new DifferenceDescription();
                int matchLocation = startToken;
                foreach (ParseInstruction sub in SubInstructions)
                {
                    matchLocation = sub.Match(matchTokens, matchLocation, matchingStartToken - 1, originalText,
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
                    _lastOptionalDifference = matchDifferences;
                }
            }
            // We didn't find any matches, return the original start token
            return startToken;
        }

        /**
         * Find the indexes that match the matching optional or first normal text within the sub-instructions
         * @param afterChild the child after which to start searching for the first normal text
         * @param matchTokens Tokens used to match the text against
         * @param startToken Start of the match tokens to begin the search
         * @param endToken End of the match tokens to end the search
         * @param originalText original text that created the match tokens
         * @param differences Information on any differences found
         * @param tokenToLocation Map of match token indexes to line/column locations
         * @return List of indexes for the start tokens for the next non variable text that matches
         * @throws LicenseParserException
         */
        private List<int> FindNextNonVarTextStartTokens(ParseInstruction afterChild,
                string[] matchTokens, int startToken, int endToken, string originalText,
                DifferenceDescription differences, Dictionary<int, LineColumn> tokenToLocation)
        {
            List<int> retval = [];
            // We find the first index to start our search
            int indexOfChild = _subInstructions.IndexOf(afterChild);
            if (indexOfChild < 0)
            {
                throw new LicenseParserException("Template Parser Error: Could not locate sub instruction");
            }
            int startSubinstructionIndex = indexOfChild + 1;
            if (startSubinstructionIndex >= SubInstructions.Count)
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
            int i = startSubinstructionIndex;
            while (i < SubInstructions.Count && firstNormalTextIndex < 0)
            {
                LicenseTemplateRule? subInstructionRule = SubInstructions[i].Rule;
                if (subInstructionRule != null && subInstructionRule.Type == LicenseTemplateRule.RuleType.BeginOptional)
                {
                    leadingOptionalSubInstructions.Add(i);
                }
                else if (SubInstructions[i].Text != null)
                {
                    firstNormalTextIndex = i;
                }
                i++;
            }
            int nextMatchingStart = startToken;
            // Go through the preceding optional rules.  If there enough token matches, add it to the result list
            foreach (int optionalSub in leadingOptionalSubInstructions)
            {
                DifferenceDescription tempDiffDescription = new DifferenceDescription();
                int nextOptMatchingStart = nextMatchingStart;
                int optTokenAfterMatch = SubInstructions[optionalSub].Match(matchTokens, nextOptMatchingStart, endToken, originalText, tempDiffDescription, tokenToLocation, true);
                while (optTokenAfterMatch <= nextOptMatchingStart && -optTokenAfterMatch <= endToken
                        && !tempDiffDescription.DifferenceFound && nextOptMatchingStart <= endToken)
                {
                    // while we didn't find a match
                    nextOptMatchingStart++;
                    optTokenAfterMatch = SubInstructions[optionalSub].Match(matchTokens, nextOptMatchingStart, endToken, originalText, tempDiffDescription, tokenToLocation, true);
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

            Dictionary<int, LineColumn> normalTextLocations = new();
            string[] textTokens = ToolsLicenseCompareHelper.TokenizeLicenseText(ToolsLicenseCompareHelper.NormalizeText(SubInstructions[firstNormalTextIndex].Text ?? string.Empty), normalTextLocations);
            if (textTokens.Length > MAX_NEXT_NORMAL_TEXT_SEARCH_LENGTH)
            {
                textTokens = textTokens.Take(MAX_NEXT_NORMAL_TEXT_SEARCH_LENGTH).ToArray();
            }

            int tokenAfterMatch = CompareText(textTokens, matchTokens, nextMatchingStart, null, _compareTokens);
            bool foundEnoughTokens = false;
            while (!foundEnoughTokens && nextMatchingStart <= endToken && !differences.DifferenceFound)
            {
                while (tokenAfterMatch < 0 && -tokenAfterMatch <= endToken)
                {
                    nextMatchingStart = nextMatchingStart + 1;
                    tokenAfterMatch = CompareText(textTokens, matchTokens, nextMatchingStart, null, _compareTokens);
                }
                if (tokenAfterMatch < 0)
                {
                    // Can not find the text, report a difference
                    string ruleDesc = "variable or optional rule";
                    if (afterChild.Rule != null)
                    {
                        if (afterChild.Rule.Type == LicenseTemplateRule.RuleType.BeginOptional)
                        {
                            ruleDesc = "optional rule";
                        }
                        else if (afterChild.Rule.Type == LicenseTemplateRule.RuleType.Variable)
                        {
                            ruleDesc = "variable rule '" + afterChild.Rule.Name + "'";
                        }
                    }
                    differences.AddDifference(tokenToLocation[nextMatchingStart], "",
                            "Unable to find the text '" + SubInstructions[firstNormalTextIndex].Text + "' following a " + ruleDesc,
                                    null, Rule, GetLastOptionalDifference());
                }
                else if (textTokens.Length >= MIN_TOKENS_NORMAL_TEXT_SEARCH)
                {
                    retval.Add(nextMatchingStart);
                    foundEnoughTokens = true;
                }
                else
                {
                    // Not enough text tokens, we need to make sure everything matches beyond this point
                    DifferenceDescription tempDiffDescription = new DifferenceDescription();
                    int nextCheckToken = SubInstructions[firstNormalTextIndex].Match(matchTokens, nextMatchingStart, endToken, originalText, tempDiffDescription, tokenToLocation, true);
                    int nextCheckSubInstruction = firstNormalTextIndex + 1;
                    while (nextCheckToken > 0 &&
                            nextCheckToken - tokenAfterMatch < MIN_TOKENS_NORMAL_TEXT_SEARCH &&
                            nextCheckSubInstruction < SubInstructions.Count)
                    {
                        nextCheckToken = SubInstructions[nextCheckSubInstruction++].Match(matchTokens, nextCheckToken, endToken, originalText, tempDiffDescription, tokenToLocation, true);
                    }
                    if (nextCheckToken < 0)
                    {
                        // we didn't match enough, move on to the next
                        nextMatchingStart = nextMatchingStart + 1;
                        tokenAfterMatch = CompareText(textTokens, matchTokens, nextMatchingStart, null, _compareTokens);
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
		 * @param text
		 * @param end End of matching text
		 * @return
		 */
        private static int NumTokensMatched(string text, int end)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }
            if (end == 0)
            {
                return 0;
            }
            Dictionary<int, LineColumn> temp = new();
            string subText = text.Substring(0, end);
            string[] tokenizedString = ToolsLicenseCompareHelper.TokenizeLicenseText(subText, temp);
            return tokenizedString.Length;
        }

        /**
         * Match to a variable rule
         * @param matchingStartTokens List of indexes for the start tokens for the next normal text
         * @param matchTokens Tokens to match against
         * @param startToken Index of the first token to search for the match
         * @param endToken Index of the last token to search for the match
         * @param originalText Original text used go generate the matchTokens
         * @param differences Any differences found
         * @param tokenToLocation Map of token index to line/column where the token was found in the original text
         * @return the index of the token after the find or -1 if the text did not match
         */
        private int MatchVariable(List<int> matchingStartTokens, string[] matchTokens, int startToken,
                string originalText, DifferenceDescription differences, Dictionary<int, LineColumn> tokenToLocation)
        {

            if (differences.DifferenceFound)
            {
                return -1;
            }
            foreach (int matchingStartToken in matchingStartTokens)
            {
                string compareText = LocateOriginalText(originalText, startToken, matchingStartToken - 1, tokenToLocation, matchTokens);
                Match match = Regex.Match(compareText, Rule?.Match ?? string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (match.Success && match.Index <= 0)
                {
                    int numMatched = NumTokensMatched(compareText, match.Index + match.Length);
                    return startToken + numMatched;
                }
            }
            // if we got here, there was no match found
            tokenToLocation.TryGetValue(startToken, out LineColumn? startLocation);
            differences.AddDifference(startLocation, ToolsLicenseCompareHelper.GetTokenAt(matchTokens, startToken), "Variable text rule " + Rule?.Name + " did not match the compare text",
                    null, Rule, GetLastOptionalDifference());
            return -1;
        }

        /**
		 * @return The difference description for the last optional rule which did not match
		 */
        public DifferenceDescription? GetLastOptionalDifference()
        {
            if (_lastOptionalDifference != null)
            {
                return _lastOptionalDifference;
            }
            else if (Parent != null)
            {
                return Parent.GetLastOptionalDifference();
            }
            else
            {
                return null;
            }
        }

        public void SetLastOptionalDifference(DifferenceDescription optionalDifference)
        {
            if (!string.IsNullOrEmpty(optionalDifference?.DifferenceMessage))
            {
                _lastOptionalDifference = optionalDifference!;
                Parent?.SetLastOptionalDifference(optionalDifference!);
            }
        }

        /**
         * @return true if the instruction following this instruction is a beginOptional rule containing text with a single token
         */
        public bool IsFollowingInstructionOptionalSingleToken()
        {
            if (Parent == null)
            {
                return false;
            }
            ParseInstruction? nextInstruction = Parent.FindFollowingInstruction(this);
            if (nextInstruction == null || nextInstruction.Rule == null)
            {
                return false;
            }
            else
            {
                if (!LicenseTemplateRule.RuleType.BeginOptional.Equals(nextInstruction.Rule.Type))
                {
                    return false;
                }
                if (nextInstruction.SubInstructions.Count != 1)
                {
                    return false;
                }
                string? optionalText = nextInstruction.SubInstructions[0].Text;
                return ToolsLicenseCompareHelper.IsSingleTokenString(optionalText ?? string.Empty);
            }
        }

        /**
         * @param parseInstruction subInstruction to find the next parse instruction after
         * @return the next instruction after parseInstruction in the subInstructions
         */
        private ParseInstruction? FindFollowingInstruction(ParseInstruction parseInstruction)
        {
            if (parseInstruction == null)
            {
                return null;
            }
            for (int i = 0; i < SubInstructions.Count; i++)
            {
                if (parseInstruction.Equals(SubInstructions[i]))
                {
                    if (SubInstructions.Count > i + 1)
                    {
                        return SubInstructions[i + 1];
                    }
                    else if (Parent == null)
                    {
                        return null;
                    }
                    else
                    {
                        return Parent.FindFollowingInstruction(this);
                    }
                }
            }
            return null;    // instruction not found
        }

        /**
         * @return the tokens from the next group of optional
         */
        public string[] GetNextOptionalTextTokens()
        {
            if (Parent == null)
            {
                return [];
            }
            ParseInstruction? nextInstruction = Parent.FindFollowingInstruction(this);
            if (nextInstruction == null || nextInstruction.Rule == null)
            {
                return [];
            }
            else
            {
                if (!LicenseTemplateRule.RuleType.BeginOptional.Equals(nextInstruction.Rule.Type))
                {
                    return [];
                }
                StringBuilder sb = new StringBuilder();
                foreach (string text in nextInstruction.SubInstructions.Select(i => i.Text).Where(i => i != null)!)
                {
                    sb.Append(text);
                }
                Dictionary<int, LineColumn> temp = new();
                return ToolsLicenseCompareHelper.TokenizeLicenseText(sb.ToString(), temp);
            }
        }

        /**
		 * Skip the next instruction
		 */
        public void SkipNextInstruction()
        {
            if (Parent == null)
            {
                return;
            }
            ParseInstruction? nextInst = Parent.FindFollowingInstruction(this);
            if (nextInst == null)
            {
                return;
            }
            nextInst.Skip = true;
        }

        /**
         * @return the next sibling parse instruction which is just text (no rules)
         */
        public ParseInstruction? GetNextNormalTextInstruction()
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
                if (siblings[i].Rule != null && LicenseTemplateRule.RuleType.BeginOptional.Equals(siblings[i].Rule?.Type))
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
                return Parent.GetNextNormalTextInstruction();
            }
        }

        /**
         * @param textTokens
         * @param matchTokens
         * @param startToken
         * @param endToken
         * @param instruction
         * @return positive index of the next match token after the match or negative index of the token which first failed the match
         */
        private static int CompareText(string[] textTokens, string[] matchTokens, int startToken, ParseInstruction? instruction, string[] compareTokens)
        {
            int textTokenCounter = 0;
            string? nextTextToken = ToolsLicenseCompareHelper.GetTokenAt(textTokens, textTokenCounter++);
            int matchTokenCounter = startToken;
            string? nextMatchToken = ToolsLicenseCompareHelper.GetTokenAt(matchTokens, matchTokenCounter++);
            while (nextTextToken != null)
            {
                if (nextMatchToken == null)
                {
                    // end of compare text stream
                    while (nextTextToken != null && ToolsLicenseCompareHelper.CanSkip(nextTextToken))
                    {
                        nextTextToken = ToolsLicenseCompareHelper.GetTokenAt(textTokens, textTokenCounter++);
                    }
                    if (nextTextToken != null)
                    {
                        return -matchTokenCounter;  // there is more stuff in the compare license text, so not equiv.
                    }
                }
                else if (ToolsLicenseCompareHelper.TokensEquivalent(nextTextToken, nextMatchToken))
                {
                    // just move onto the next set of tokens
                    nextTextToken = ToolsLicenseCompareHelper.GetTokenAt(textTokens, textTokenCounter++);
                    if (nextTextToken != null)
                    {
                        nextMatchToken = ToolsLicenseCompareHelper.GetTokenAt(matchTokens, matchTokenCounter++);
                    }
                }
                else
                {
                    // see if we can skip through some compare tokens to find a match
                    while (nextMatchToken != null && ToolsLicenseCompareHelper.CanSkip(nextMatchToken))
                    {
                        nextMatchToken = ToolsLicenseCompareHelper.GetTokenAt(matchTokens, matchTokenCounter++);
                    }
                    // just to be sure, skip forward on the text
                    while (nextTextToken != null && ToolsLicenseCompareHelper.CanSkip(nextTextToken))
                    {
                        nextTextToken = ToolsLicenseCompareHelper.GetTokenAt(textTokens, textTokenCounter++);
                    }
                    if (ToolsLicenseCompareHelper.TokensEquivalent(nextMatchToken, nextTextToken))
                    {
                        nextTextToken = ToolsLicenseCompareHelper.GetTokenAt(textTokens, textTokenCounter++);
                        if (nextTextToken != null)
                        {
                            nextMatchToken = ToolsLicenseCompareHelper.GetTokenAt(compareTokens, matchTokenCounter++);
                        }
                    }
                    else
                    {
                        if (textTokenCounter == textTokens.Length &&
                                instruction != null &&
                                instruction.IsFollowingInstructionOptionalSingleToken() &&
                                nextMatchToken != null)
                        {
                            //This is the special case where there may be optional characters which are
                            //less than a token at the end of a compare
                            //Yes - this is a bit of a hack
                            string compareToken = nextTextToken + instruction.GetNextOptionalTextTokens()[0];
                            if (ToolsLicenseCompareHelper.TokensEquivalent(compareToken, nextMatchToken))
                            {
                                instruction.SkipNextInstruction();
                                return matchTokenCounter;
                            }
                            else
                            {
                                ParseInstruction? nextNormal = instruction.GetNextNormalTextInstruction();
                                string? nextNormalText = ToolsLicenseCompareHelper.GetFirstLicenseToken(nextNormal?.Text ?? string.Empty);
                                if (nextNormalText != null)
                                {
                                    compareToken = compareToken + nextNormalText;
                                    string compareWithoutOptional = nextTextToken + nextNormalText;
                                    if (ToolsLicenseCompareHelper.TokensEquivalent(compareToken, nextMatchToken) ||
                                            ToolsLicenseCompareHelper.TokensEquivalent(compareWithoutOptional, nextMatchToken))
                                    {
                                        instruction.SkipNextInstruction();
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
    }

    public class DifferenceDescription
    {
        private const int MAX_DIFF_TEXT_LENGTH = 100;
        public bool DifferenceFound { get; set; }
        public string DifferenceMessage { get; set; }
        private readonly List<LineColumn> _differences;
        public IReadOnlyList<LineColumn> Differences => _differences;

        public DifferenceDescription(bool differenceFound, string differenceMessage, List<LineColumn> differences)
        {
            DifferenceFound = differenceFound;
            DifferenceMessage = differenceMessage;
            _differences = differences;
        }

        public DifferenceDescription()
        {
            DifferenceFound = false;
            DifferenceMessage = "No difference found";
            _differences = [];
        }

        /**
         * @param location Location in the text of the difference
         * @param token Token causing the difference
         * @param msg Message for the difference
         * @param text Template text being compared to
         * @param rule Template rule where difference was found
         * @param lastOptionalDifference The difference for the last optional difference that failed
         */
        public void AddDifference(LineColumn? location, string? token, string? msg, string? text,
                LicenseTemplateRule? rule, DifferenceDescription? lastOptionalDifference)
        {
            if (token == null)
            {
                token = "";
            }
            if (msg == null)
            {
                msg = "UNKNOWN (null)";
            }
            DifferenceMessage = msg;
            if (location != null)
            {
                DifferenceMessage = DifferenceMessage + " starting at line #" +
                        Convert.ToString(location.Line) + " column #" +
                        Convert.ToString(location.Column) + " \"" +
                        token + "\"";
                _differences.Add(location);
            }
            else
            {
                DifferenceMessage = DifferenceMessage + " at end of text";
            }
            if (text != null)
            {
                DifferenceMessage = DifferenceMessage + " when comparing to template text \"";
                if (text.Length > MAX_DIFF_TEXT_LENGTH)
                {
                    DifferenceMessage = DifferenceMessage +
                            text.Substring(0, MAX_DIFF_TEXT_LENGTH) + "...\"";
                }
                else
                {
                    DifferenceMessage = DifferenceMessage + text + "\"";
                }
            }
            if (rule != null)
            {
                DifferenceMessage = DifferenceMessage + " while processing rule " + rule.ToString();
            }
            if (lastOptionalDifference != null)
            {
                DifferenceMessage = DifferenceMessage +
                        ".  Last optional text was not found due to the optional difference: \n\t" +
                        lastOptionalDifference.DifferenceMessage;
            }
            DifferenceFound = true;
        }
    }

    private readonly string[] _compareTokens;
    private readonly string _compareText;
    private readonly Dictionary<int, LineColumn> _tokenToLocation = new();
    private readonly ParseInstruction _topLevelInstruction;
    public DifferenceDescription Differences { get; } = new DifferenceDescription();
    private ParseInstruction? _currentOptionalInstruction = null;

    /**
     * @param compareText Text to compare the parsed SPDX license template to
     * @throws IOException This is not to be expected since we are using StringReaders
     */
    public CompareTemplateOutputHandler(string compareText)
    {
        _compareText = ToolsLicenseCompareHelper.NormalizeText(compareText);
        _compareTokens = ToolsLicenseCompareHelper.TokenizeLicenseText(_compareText, _tokenToLocation);
        _topLevelInstruction = new ParseInstruction(null, null, null, _compareTokens);
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#text(java.lang.String)
     */
    public void Text(string text)
    {
        if (_currentOptionalInstruction != null)
        {
            _currentOptionalInstruction.AddSubInstruction(new ParseInstruction(null, text, _currentOptionalInstruction, _compareTokens));
        }
        else
        {
            _topLevelInstruction.AddSubInstruction(new ParseInstruction(null, text, null, _compareTokens));
        }
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#variableRule(org.spdx.licenseTemplate.LicenseTemplateRule)
     */
    public void VariableRule(LicenseTemplateRule rule)
    {
        if (_currentOptionalInstruction != null)
        {
            _currentOptionalInstruction.AddSubInstruction(new ParseInstruction(rule, null, _currentOptionalInstruction, _compareTokens));
        }
        else
        {
            _topLevelInstruction.AddSubInstruction(new ParseInstruction(rule, null, null, _compareTokens));
        }
    }


    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#beginOptional(org.spdx.licenseTemplate.LicenseTemplateRule)
     */
    public void BeginOptional(LicenseTemplateRule rule)
    {
        ParseInstruction optionalInstruction = new ParseInstruction(rule, null, _currentOptionalInstruction, _compareTokens);
        if (_currentOptionalInstruction != null)
        {
            _currentOptionalInstruction.AddSubInstruction(optionalInstruction);
        }
        else
        {
            _topLevelInstruction.AddSubInstruction(optionalInstruction);
        }
        _currentOptionalInstruction = optionalInstruction;
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#endOptional(org.spdx.licenseTemplate.LicenseTemplateRule)
     */
    public void EndOptional(LicenseTemplateRule rule)
    {
        if (_currentOptionalInstruction != null)
        {
            _currentOptionalInstruction = _currentOptionalInstruction.Parent;
            if (_currentOptionalInstruction == null || _currentOptionalInstruction.Rule == null || _currentOptionalInstruction.Rule.Type != LicenseTemplateRule.RuleType.BeginOptional)
            {
                _currentOptionalInstruction = null;
            }
        }
    }

    /* (non-Javadoc)
     * @see org.spdx.licenseTemplate.ILicenseTemplateOutputHandler#completeParsing()
     */
    public void CompleteParsing()
    {
        _topLevelInstruction.Match(_compareTokens, 0, _compareTokens.Length - 1, _compareText, Differences, _tokenToLocation);
    }
}
