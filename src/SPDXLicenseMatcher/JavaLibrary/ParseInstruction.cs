// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SPDXLicenseMatcher.JavaCore;

namespace SPDXLicenseMatcher.JavaLibrary;

public sealed class ParseInstruction
{
    private const int MAX_NEXT_NORMAL_TEXT_SEARCH_LENGTH = 15;   // Maximum number of tokens to compare when searching for a normal text match
    private const int MIN_TOKENS_NORMAL_TEXT_SEARCH = 3; // Minimum number of tokens to match of normal text to match after a variable block to bound greedy regex var text

    public LicenseTemplateRule? Rule { get; set; }
    public string? _text { get; }
    public IReadOnlyList<string>? TokenizedText { get; }
    private readonly List<ParseInstruction> _subInstructions;
    public IReadOnlyList<ParseInstruction> SubInstructions => _subInstructions;
    public ParseInstruction? Parent { get; set; }

    public bool Skip { get; set; } = false;   // skip this instruction in matching
    public bool SkipFirstTextToken { get; set; } = false; // skip the first text token
    private DifferenceDescription? _lastOptionalDifference = null;

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
    public ParseInstruction(LicenseTemplateRule? rule, string? text)
    {
        Rule = rule;
        _text = text;
        TokenizedText = text is null ? null : LicenseTextHelper.tokenizeLicenseText(text, new Dictionary<int, LineColumn>());
        _subInstructions = [];
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
        else if (_text != null)
        {
            string retval = "TEXT: '";
            if (_text.Length > 10)
            {
                retval = retval + _text.Substring(0, 10) + "...'";
            }
            else
            {
                retval = retval + _text + "'";
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
        if (instruction.Rule != null &&
            LicenseTemplateRule.RuleType.VARIABLE.Equals(instruction.Rule.Type) &&
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
            if (subInstr.TokenizedText == null)
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
            DifferenceDescription differences, IDictionary<int, LineColumn> tokenToLocation, IReadOnlyList<string> compareTokens)
    {
        return match(matchTokens, startToken, endToken, originalText, differences, tokenToLocation, false, compareTokens);
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
            DifferenceDescription differences, IDictionary<int, LineColumn> tokenToLocation, bool ignoreOptionalDifferences, IReadOnlyList<string> compareTokens)
    {
        if (Skip)
        {
            return startToken;
        }

        int nextToken = startToken;
        if (Rule == null)
        {
            if (TokenizedText != null)
            {
                IReadOnlyList<string> textTokens = TokenizedText;
                if (SkipFirstTextToken)
                {
                    textTokens = textTokens.Skip(1).ToArray();
                }
                nextToken = CompareTemplateOutputHandler.compareText(textTokens, matchTokens, nextToken, this, compareTokens);
                if (nextToken < 0)
                {
                    int errorLocation = -nextToken - 1;
                    tokenToLocation.TryGetValue(errorLocation, out LineColumn? errorLine);
                    differences.AddDifference(errorLine, LicenseTextHelper.getTokenAt(matchTokens, errorLocation),
                                    "Normal text of license does not match", _text, null, getLastOptionalDifference());
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
                            tokenToLocation, ignoreOptionalDifferences, compareTokens);
                    if (nextToken < 0)
                    {
                        return nextToken;
                    }
                }
            }

        }
        else if (Rule.Type.Equals(LicenseTemplateRule.RuleType.BEGIN_OPTIONAL))
        {
            if (TokenizedText != null)
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
                            optionalDifference, tokenToLocation, compareTokens);
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
                        startToken, endToken, originalText, differences, tokenToLocation, compareTokens);
                nextToken = matchOptional(matchingNormalTextStartTokens, matchTokens,
                        nextToken, originalText, tokenToLocation, ignoreOptionalDifferences, compareTokens);
            }
        }
        else if (Rule.Type.Equals(LicenseTemplateRule.RuleType.VARIABLE))
        {
            IReadOnlyList<int> matchingNormalTextStartTokens = Parent?.findNextNonVarTextStartTokens(this, matchTokens,
                    startToken, endToken, originalText, differences, tokenToLocation, compareTokens) ?? [];
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
                              IDictionary<int, LineColumn> tokenToLocation, bool ignoreOptionalDifferences, IReadOnlyList<string> compareTokens)
    {
        foreach (int matchingStartToken in matchingStartTokens)
        {
            DifferenceDescription matchDifferences = new DifferenceDescription();
            int matchLocation = startToken;
            foreach (ParseInstruction sub in _subInstructions)
            {
                matchLocation = sub.match(matchTokens, matchLocation, matchingStartToken - 1, originalText,
                        matchDifferences, tokenToLocation, compareTokens);
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
            DifferenceDescription differences, IDictionary<int, LineColumn> tokenToLocation, IReadOnlyList<string> compareTokens)
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
            else if ((_subInstructions[i].TokenizedText?.Count ?? 0) > 0)
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
            int optTokenAfterMatch = _subInstructions[optionalSub].match(matchTokens, nextOptMatchingStart, endToken, originalText, tempDiffDescription, tokenToLocation, true, compareTokens);
            while (optTokenAfterMatch <= nextOptMatchingStart && -optTokenAfterMatch <= endToken
                    && !tempDiffDescription.DifferenceFound && nextOptMatchingStart <= endToken)
            {
                // while we didn't find a match
                nextOptMatchingStart++;
                optTokenAfterMatch = _subInstructions[optionalSub].match(matchTokens, nextOptMatchingStart, endToken, originalText, tempDiffDescription, tokenToLocation, true, compareTokens);
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

        IReadOnlyList<string> textTokens = _subInstructions[firstNormalTextIndex].TokenizedText ?? [];
        if (textTokens.Count > MAX_NEXT_NORMAL_TEXT_SEARCH_LENGTH)
        {
            textTokens = textTokens.Take(MAX_NEXT_NORMAL_TEXT_SEARCH_LENGTH).ToArray();
        }

        int tokenAfterMatch = CompareTemplateOutputHandler.compareText(textTokens, matchTokens, nextMatchingStart, null, compareTokens);
        bool foundEnoughTokens = false;
        while (!foundEnoughTokens && nextMatchingStart <= endToken && !differences.DifferenceFound)
        {
            while (tokenAfterMatch < 0 && -tokenAfterMatch <= endToken)
            {
                nextMatchingStart = nextMatchingStart + 1;
                tokenAfterMatch = CompareTemplateOutputHandler.compareText(textTokens, matchTokens, nextMatchingStart, null, compareTokens);
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
                        "Unable to find the text '" + _subInstructions[firstNormalTextIndex]._text + "' following a " + ruleDesc,
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
                int nextCheckToken = _subInstructions[firstNormalTextIndex].match(matchTokens, nextMatchingStart, endToken, originalText, tempDiffDescription, tokenToLocation, true, compareTokens);
                int nextCheckSubInstruction = firstNormalTextIndex + 1;
                while (nextCheckToken > 0 &&
                        nextCheckToken - tokenAfterMatch < MIN_TOKENS_NORMAL_TEXT_SEARCH &&
                        nextCheckSubInstruction < _subInstructions.Count)
                {
                    nextCheckToken = _subInstructions[nextCheckSubInstruction++].match(matchTokens, nextCheckToken, endToken, originalText, tempDiffDescription, tokenToLocation, true, compareTokens);
                }
                if (nextCheckToken < 0)
                {
                    // we didn't match enough, move on to the next
                    nextMatchingStart = nextMatchingStart + 1;
                    tokenAfterMatch = CompareTemplateOutputHandler.compareText(textTokens, matchTokens, nextMatchingStart, null, compareTokens);
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
            return (nextInstruction.SubInstructions[0].TokenizedText?.Count ?? 0) == 1;
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
            var tokens = new List<string>();
            foreach (IReadOnlyList<string> t in nextInstruction.SubInstructions.Select(i => i.TokenizedText).Where(t => t is not null)!)
            {
                tokens.AddRange(t);
            }
            return tokens;
        }
    }

    /**
     * Skip the next instruction
     */
    public void skipNextInstruction()
    {
        ParseInstruction? nextInst = Parent?.findFollowingInstruction(this);
        nextInst?.Skip = true;
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
                if (siblings[i].TokenizedText != null)
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
