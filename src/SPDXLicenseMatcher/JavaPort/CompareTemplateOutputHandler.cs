// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SPDXLicenseMatcher.JavaPort
{
    /// <summary>
    /// Compares the output of a parsed license template to text. The Matches() method is called after
    /// the document is parsed to determine if the text matches.
    /// </summary>
    public class CompareTemplateOutputHandler : ILicenseTemplateOutputHandler
    {
        private const int MaxNextNormalTextSearchLength = 15;
        private const int MinTokensNormalTextSearch = 3;

        private readonly string[] _compareTokens;
        private readonly string _compareText;
        private readonly Dictionary<int, LineColumn> _tokenToLocation = new();
        private readonly ParseInstruction _topLevelInstruction = new(null, null, null);
        private ParseInstruction? _currentOptionalInstruction;
        private bool _parsingComplete;

        /// <summary>
        /// Gets the details of any differences found during the comparison.
        /// </summary>
        public DifferenceDescription Differences { get; } = new();

        public CompareTemplateOutputHandler(string compareText)
        {
            _compareText = LicenseTextHelper.NormalizeText(
                LicenseTextHelper.ReplaceMultiWord(LicenseTextHelper.ReplaceSpaceComma(compareText)));
            _compareTokens = LicenseTextHelper.TokenizeLicenseText(_compareText, _tokenToLocation);
        }

        public void Text(string text)
        {
            var instruction = new ParseInstruction(null, text, _currentOptionalInstruction);
            (_currentOptionalInstruction ?? _topLevelInstruction).AddSubInstruction(instruction);
        }

        public void VariableRule(LicenseTemplateRule rule)
        {
            var instruction = new ParseInstruction(rule, null, _currentOptionalInstruction);
            (_currentOptionalInstruction ?? _topLevelInstruction).AddSubInstruction(instruction);
        }

        public void BeginOptional(LicenseTemplateRule rule)
        {
            var optionalInstruction = new ParseInstruction(rule, null, _currentOptionalInstruction);
            (_currentOptionalInstruction ?? _topLevelInstruction).AddSubInstruction(optionalInstruction);
            _currentOptionalInstruction = optionalInstruction;
        }

        public void EndOptional(LicenseTemplateRule rule)
        {
            if (_currentOptionalInstruction != null)
            {
                _currentOptionalInstruction = _currentOptionalInstruction.Parent;
                if (_currentOptionalInstruction?.Rule?.Type != LicenseTemplateRule.RuleType.BeginOptional)
                {
                    _currentOptionalInstruction = null;
                }
            }
        }

        public void CompleteParsing()
        {
            int nextTokenIndex = _topLevelInstruction.Match(_compareTokens, 0, _compareTokens.Length - 1, _compareText, Differences, _tokenToLocation);

            if (nextTokenIndex > 0 && nextTokenIndex < _compareTokens.Length)
            {
                _tokenToLocation.TryGetValue(nextTokenIndex, out LineColumn? location);
                Differences.AddDifference(
                    location,
                    LicenseTextHelper.GetTokenAt(_compareTokens, nextTokenIndex),
                    "Additional text found after the end of the expected license text.",
                    null, null, null);
            }
            _parsingComplete = true;
        }

        public bool Matches()
        {
            if (!_parsingComplete)
            {
                throw new LicenseParserException("Matches() was called before CompleteParsing() has been called.");
            }
            return !Differences.IsDifferenceFound;
        }

        public int TextEquivalent(string text, int startToken)
        {
            var textLocations = new Dictionary<int, LineColumn>();
            string[] textTokens = LicenseTextHelper.TokenizeLicenseText(text, textLocations);
            return CompareText(textTokens, _compareTokens, startToken, null);
        }

        private int CompareText(string[] textTokens, string[] matchTokens, int startToken, ParseInstruction? instruction)
        {
            if (textTokens.Length == 0) return startToken;

            int textTokenCounter = 0;
            string? nextTextToken = LicenseTextHelper.GetTokenAt(textTokens, textTokenCounter++);
            int matchTokenCounter = startToken;
            string? nextMatchToken = LicenseTextHelper.GetTokenAt(matchTokens, matchTokenCounter++);

            while (nextTextToken != null)
            {
                if (nextMatchToken == null)
                {
                    while (LicenseTextHelper.CanSkip(nextTextToken))
                    {
                        nextTextToken = LicenseTextHelper.GetTokenAt(textTokens, textTokenCounter++);
                    }
                    if (nextTextToken != null) return -matchTokenCounter;
                }
                else if (LicenseTextHelper.TokensEquivalent(nextTextToken, nextMatchToken))
                {
                    nextTextToken = LicenseTextHelper.GetTokenAt(textTokens, textTokenCounter++);
                    if (nextTextToken != null)
                    {
                        nextMatchToken = LicenseTextHelper.GetTokenAt(matchTokens, matchTokenCounter++);
                    }
                }
                else
                {
                    while (LicenseTextHelper.CanSkip(nextMatchToken))
                    {
                        nextMatchToken = LicenseTextHelper.GetTokenAt(matchTokens, matchTokenCounter++);
                    }
                    while (LicenseTextHelper.CanSkip(nextTextToken))
                    {
                        nextTextToken = LicenseTextHelper.GetTokenAt(textTokens, textTokenCounter++);
                    }

                    if (LicenseTextHelper.TokensEquivalent(nextMatchToken, nextTextToken))
                    {
                        nextTextToken = LicenseTextHelper.GetTokenAt(textTokens, textTokenCounter++);
                        if (nextTextToken != null)
                        {
                            nextMatchToken = LicenseTextHelper.GetTokenAt(_compareTokens, matchTokenCounter++);
                        }
                    }
                    else
                    {
                        if (textTokenCounter == textTokens.Length && instruction?.IsFollowingInstructionOptionalSingleToken() == true && nextMatchToken != null)
                        {
                            string compareToken = nextTextToken + instruction.GetNextOptionalTextTokens()[0];
                            if (LicenseTextHelper.TokensEquivalent(compareToken, nextMatchToken))
                            {
                                instruction.SkipNextInstruction();
                                return matchTokenCounter;
                            }

                            ParseInstruction? nextNormal = instruction.GetNextNormalTextInstruction();
                            string? nextNormalText = LicenseCompareHelper.GetFirstLicenseToken(nextNormal?.Text);
                            if (nextNormalText != null)
                            {
                                compareToken += nextNormalText;
                                string compareWithoutOptional = nextTextToken + nextNormalText;
                                if (LicenseTextHelper.TokensEquivalent(compareToken, nextMatchToken) || LicenseTextHelper.TokensEquivalent(compareWithoutOptional, nextMatchToken))
                                {
                                    instruction.SkipNextInstruction();
                                    nextNormal!.SkipFirstTextToken = true;
                                    return matchTokenCounter;
                                }
                            }
                        }
                        return -matchTokenCounter;
                    }
                }
            }
            return matchTokenCounter;
        }

        #region Nested Classes

        private sealed class ParseInstruction
        {
            private DifferenceDescription? _lastOptionalDifference;

            public LicenseTemplateRule? Rule { get; set; }
            public string? Text { get; set; }
            public List<ParseInstruction> SubInstructions { get; }
            public ParseInstruction? Parent { get; private set; }
            public bool Skip { get; set; }
            public bool SkipFirstTextToken { get; set; }

            public ParseInstruction(LicenseTemplateRule? rule, string? text, ParseInstruction? parent)
            {
                Rule = rule;
                Text = text;
                SubInstructions = new List<ParseInstruction>();
                Parent = parent;
            }

            public override string ToString()
            {
                if (Rule != null) return Rule.ToString();
                if (Text != null)
                {
                    return Text.Length > 10 ? $"TEXT: '{Text.Substring(0, 10)}...'" : $"TEXT: '{Text}'";
                }
                return "NONE";
            }

            public void AddSubInstruction(ParseInstruction instruction)
            {
                if (instruction.Rule?.Type == LicenseTemplateRule.RuleType.Variable &&
                    SubInstructions.Count > 0 &&
                    SubInstructions[^1].Rule is { } lastRule &&
                    lastRule.Type == LicenseTemplateRule.RuleType.Variable)
                {
                    lastRule.Match = $"({lastRule.Match})\\s*({instruction.Rule.Match})";
                    lastRule.Name = $"combined-{lastRule.Name}-{instruction.Rule.Name}";
                    lastRule.Original += " " + instruction.Rule.Original;
                }
                else
                {
                    instruction.Parent = this;
                    SubInstructions.Add(instruction);
                }
            }

            public int Match(string[] matchTokens, int startToken, int endToken, string originalText, DifferenceDescription differences, Dictionary<int, LineColumn> tokenToLocation, bool ignoreOptionalDifferences = false)
            {
                if (Skip) return startToken;

                if (Rule == null)
                {
                    return MatchText(matchTokens, startToken, endToken, originalText, differences, tokenToLocation, ignoreOptionalDifferences);
                }

                return Rule.Type switch
                {
                    LicenseTemplateRule.RuleType.BeginOptional => MatchOptionalBlock(matchTokens, startToken, endToken, originalText, differences, tokenToLocation, ignoreOptionalDifferences),
                    LicenseTemplateRule.RuleType.Variable => MatchVariableBlock(matchTokens, startToken, endToken, originalText, differences, tokenToLocation),
                    _ => throw new LicenseParserException("Unexpected parser state: instruction rule type is invalid.")
                };
            }

            private int MatchText(string[] matchTokens, int startToken, int endToken, string originalText, DifferenceDescription differences, Dictionary<int, LineColumn> tokenToLocation, bool ignoreOptionalDifferences)
            {
                int nextToken = startToken;
                if (Text != null)
                {
                    var textLocations = new Dictionary<int, LineColumn>();
                    string[] textTokens = LicenseTextHelper.TokenizeLicenseText(Text, textLocations);
                    if (SkipFirstTextToken)
                    {
                        textTokens = textTokens.Skip(1).ToArray();
                    }

                    nextToken = new CompareTemplateOutputHandler("").CompareText(textTokens, matchTokens, nextToken, this);

                    if (nextToken < 0)
                    {
                        int errorLocation = -nextToken - 1;
                        tokenToLocation.TryGetValue(errorLocation, out LineColumn? location);
                        differences.AddDifference(location, LicenseTextHelper.GetTokenAt(matchTokens, errorLocation),
                            "Normal text of license does not match", Text, null, GetLastOptionalDifference());
                    }
                    if (SubInstructions.Any())
                    {
                        throw new LicenseParserException("Parser error: Sub-expressions are not allowed for plain text nodes.");
                    }
                }
                else
                {
                    foreach (ParseInstruction sub in SubInstructions)
                    {
                        nextToken = sub.Match(matchTokens, nextToken, endToken, originalText, differences, tokenToLocation, ignoreOptionalDifferences);
                        if (nextToken < 0) return nextToken;
                    }
                }
                return nextToken;
            }

            private int MatchOptionalBlock(string[] matchTokens, int startToken, int endToken, string originalText, DifferenceDescription differences, Dictionary<int, LineColumn> tokenToLocation, bool ignoreOptionalDifferences)
            {
                if (Text != null) throw new LicenseParserException("Parser error: Text cannot be associated with a begin optional rule.");

                List<int> nextNormalTextStarts = Parent?.FindNextNonVarTextStartTokens(this, matchTokens, startToken, endToken, originalText, differences, tokenToLocation) ?? [];
                return MatchOptional(nextNormalTextStarts, matchTokens, startToken, originalText, tokenToLocation, ignoreOptionalDifferences);
            }

            private int MatchOptional(List<int> matchingStartTokens, string[] matchTokens, int startToken, string originalText, Dictionary<int, LineColumn> tokenToLocation, bool ignoreOptionalDifferences)
            {
                foreach (int matchingStartToken in matchingStartTokens)
                {
                    var tempDifferences = new DifferenceDescription();
                    int currentToken = startToken;
                    bool matchedAllSubInstructions = true;

                    foreach (ParseInstruction sub in SubInstructions)
                    {
                        currentToken = sub.Match(matchTokens, currentToken, matchingStartToken - 1, originalText, tempDifferences, tokenToLocation, true);
                        if (currentToken < 0)
                        {
                            matchedAllSubInstructions = false;
                            break;
                        }
                    }

                    if (matchedAllSubInstructions && currentToken >= 0) return currentToken;

                    if (!ignoreOptionalDifferences)
                    {
                        SetLastOptionalDifference(tempDifferences);
                    }
                }
                return startToken;
            }

            private int MatchVariableBlock(string[] matchTokens, int startToken, int endToken, string originalText, DifferenceDescription differences, Dictionary<int, LineColumn> tokenToLocation)
            {
                if (differences.IsDifferenceFound || Rule is null) return -1;

                List<int> nextNormalTextStarts = Parent?.FindNextNonVarTextStartTokens(this, matchTokens, startToken, endToken, originalText, differences, tokenToLocation) ?? [];

                foreach (int matchingStart in nextNormalTextStarts)
                {
                    string textToMatch = LicenseCompareHelper.LocateOriginalText(originalText, startToken, matchingStart - 1, tokenToLocation, matchTokens);
                    var regex = new Regex(Rule.Match ?? string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    Match match = regex.Match(textToMatch);

                    if (match.Success && match.Index == 0)
                    {
                        int tokensConsumed = NumTokensMatched(textToMatch, match.Length);
                        return startToken + tokensConsumed;
                    }
                }

                tokenToLocation.TryGetValue(startToken, out LineColumn? location);
                differences.AddDifference(location, LicenseTextHelper.GetTokenAt(matchTokens, startToken),
                    $"Variable text rule '{Rule.Name}' did not match the text.", null, Rule, GetLastOptionalDifference());
                return -1;
            }

            public DifferenceDescription? GetLastOptionalDifference()
            {
                if (_lastOptionalDifference != null) return _lastOptionalDifference;
                return Parent?.GetLastOptionalDifference();
            }

            public void SetLastOptionalDifference(DifferenceDescription optionalDifference)
            {
                if (optionalDifference?.IsDifferenceFound == true)
                {
                    _lastOptionalDifference = optionalDifference;
                    Parent?.SetLastOptionalDifference(optionalDifference);
                }
            }

            public bool IsFollowingInstructionOptionalSingleToken()
            {
                ParseInstruction? next = Parent?.FindFollowingInstruction(this);
                if (next?.Rule?.Type != LicenseTemplateRule.RuleType.BeginOptional || next.SubInstructions.Count != 1)
                {
                    return false;
                }
                string? optionalText = next.SubInstructions[0].Text;
                return LicenseCompareHelper.IsSingleTokenString(optionalText);
            }

            public string[] GetNextOptionalTextTokens()
            {
                ParseInstruction? next = Parent?.FindFollowingInstruction(this);
                if (next?.Rule?.Type != LicenseTemplateRule.RuleType.BeginOptional)
                {
                    return System.Array.Empty<string>();
                }

                var sb = new StringBuilder();
                foreach (ParseInstruction inst in next.SubInstructions)
                {
                    if (inst.Text != null) sb.Append(inst.Text);
                }
                return LicenseTextHelper.TokenizeLicenseText(sb.ToString(), new Dictionary<int, LineColumn>());
            }

            public void SkipNextInstruction()
            {
                ParseInstruction? nextInst = Parent?.FindFollowingInstruction(this);
                if (nextInst != null)
                {
                    nextInst.Skip = true;
                }
            }

            public ParseInstruction? GetNextNormalTextInstruction()
            {
                if (Parent == null) return null;

                List<ParseInstruction> siblings = Parent.SubInstructions;
                int myIndex = siblings.IndexOf(this);
                if (myIndex < 0) return null;

                for (int i = myIndex + 1; i < siblings.Count; i++)
                {
                    if (siblings[i].Text != null) return siblings[i];
                }

                return Parent.GetNextNormalTextInstruction();
            }

            private List<int> FindNextNonVarTextStartTokens(ParseInstruction afterChild, string[] matchTokens, int startToken, int endToken, string originalText, DifferenceDescription differences, Dictionary<int, LineColumn> tokenToLocation)
            {
                var result = new List<int>();
                int childIndex = SubInstructions.IndexOf(afterChild);
                if (childIndex < 0) throw new LicenseParserException("Parser error: Could not locate sub-instruction.");

                int searchStartIndex = childIndex + 1;
                if (searchStartIndex >= SubInstructions.Count)
                {
                    result.Add(endToken + 1);
                    return result;
                }

                int firstNormalTextIndex = -1;
                var leadingOptionalInstructions = new List<int>();

                for (int i = searchStartIndex; i < SubInstructions.Count && firstNormalTextIndex < 0; i++)
                {
                    ParseInstruction current = SubInstructions[i];
                    if (current.Rule?.Type == LicenseTemplateRule.RuleType.BeginOptional)
                    {
                        leadingOptionalInstructions.Add(i);
                    }
                    else if (current.Text != null && !string.IsNullOrWhiteSpace(current.Text))
                    {
                        firstNormalTextIndex = i;
                    }
                }

                int nextMatchingStart = startToken;
                foreach (int optionalSubIndex in leadingOptionalInstructions)
                {
                    var tempDiff = new DifferenceDescription();
                    int nextOptStart = nextMatchingStart;
                    int optTokenAfterMatch = SubInstructions[optionalSubIndex].Match(matchTokens, nextOptStart, endToken, originalText, tempDiff, tokenToLocation, true);
                    while (optTokenAfterMatch <= nextOptStart && -optTokenAfterMatch <= endToken && !tempDiff.IsDifferenceFound && nextOptStart <= endToken)
                    {
                        nextOptStart++;
                        optTokenAfterMatch = SubInstructions[optionalSubIndex].Match(matchTokens, nextOptStart, endToken, originalText, tempDiff, tokenToLocation, true);
                    }

                    if (optTokenAfterMatch > 0 && !tempDiff.IsDifferenceFound && nextOptStart <= endToken)
                    {
                        if (optTokenAfterMatch - nextOptStart > MinTokensNormalTextSearch)
                        {
                            result.Add(nextOptStart);
                        }
                        nextMatchingStart = optTokenAfterMatch;
                    }
                }

                if (firstNormalTextIndex < 0)
                {
                    result.Add(endToken + 1);
                    return result;
                }

                string[] textToFindTokens = LicenseTextHelper.TokenizeLicenseText(SubInstructions[firstNormalTextIndex].Text ?? string.Empty, new Dictionary<int, LineColumn>());
                if (textToFindTokens.Length > MaxNextNormalTextSearchLength)
                {
                    textToFindTokens = textToFindTokens.Take(MaxNextNormalTextSearchLength).ToArray();
                }

                bool foundEnoughTokens = false;
                while (!foundEnoughTokens && nextMatchingStart <= endToken && !differences.IsDifferenceFound)
                {
                    int tokenAfterMatch = new CompareTemplateOutputHandler("").CompareText(textToFindTokens, matchTokens, nextMatchingStart, null);
                    while (tokenAfterMatch < 0 && -tokenAfterMatch <= endToken)
                    {
                        nextMatchingStart++;
                        tokenAfterMatch = new CompareTemplateOutputHandler("").CompareText(textToFindTokens, matchTokens, nextMatchingStart, null);
                    }

                    if (tokenAfterMatch < 0)
                    {
                        string ruleDesc = afterChild.Rule?.Type == LicenseTemplateRule.RuleType.Variable ? $"variable rule '{afterChild.Rule.Name}'" : "optional rule";
                        tokenToLocation.TryGetValue(nextMatchingStart, out LineColumn? location);
                        differences.AddDifference(location, "",
                            $"Unable to find the text '{SubInstructions[firstNormalTextIndex].Text}' following a {ruleDesc}.",
                            null, Rule, GetLastOptionalDifference());
                    }
                    else if (textToFindTokens.Length >= MinTokensNormalTextSearch)
                    {
                        result.Add(nextMatchingStart);
                        foundEnoughTokens = true;
                    }
                    else
                    {
                        var tempDiff = new DifferenceDescription();
                        int nextCheckToken = SubInstructions[firstNormalTextIndex].Match(matchTokens, nextMatchingStart, endToken, originalText, tempDiff, tokenToLocation, true);
                        int nextCheckSubInstruction = firstNormalTextIndex + 1;
                        while (nextCheckToken > 0 && nextCheckToken - tokenAfterMatch < MinTokensNormalTextSearch && nextCheckSubInstruction < SubInstructions.Count)
                        {
                            nextCheckToken = SubInstructions[nextCheckSubInstruction++].Match(matchTokens, nextCheckToken, endToken, originalText, tempDiff, tokenToLocation, true);
                        }

                        if (nextCheckToken < 0)
                        {
                            nextMatchingStart++;
                        }
                        else
                        {
                            result.Add(nextMatchingStart);
                            foundEnoughTokens = true;
                        }
                    }
                }
                return result;
            }

            private ParseInstruction? FindFollowingInstruction(ParseInstruction instruction)
            {
                int index = SubInstructions.IndexOf(instruction);
                if (index >= 0 && index + 1 < SubInstructions.Count)
                {
                    return SubInstructions[index + 1];
                }
                return Parent?.FindFollowingInstruction(this);
            }

            private static int NumTokensMatched(string text, int endIndex)
            {
                if (string.IsNullOrWhiteSpace(text) || endIndex == 0) return 0;
                string subText = text.Length <= endIndex ? text : text.Substring(0, endIndex);
                return LicenseTextHelper.TokenizeLicenseText(subText, new Dictionary<int, LineColumn>()).Length;
            }
        }

        public class DifferenceDescription
        {
            private const int MaxDiffTextLength = 100;
            public bool IsDifferenceFound { get; private set; }
            public string DifferenceMessage { get; private set; }
            public List<LineColumn> Differences { get; }

            public DifferenceDescription()
            {
                IsDifferenceFound = false;
                DifferenceMessage = "No difference found.";
                Differences = new List<LineColumn>();
            }

            public void AddDifference(LineColumn? location, string? token, string msg, string? text, LicenseTemplateRule? rule, DifferenceDescription? lastOptionalDifference)
            {
                var messageBuilder = new StringBuilder(msg ?? "An unknown difference was found");
                token ??= "";

                if (location != null)
                {
                    messageBuilder.Append($" starting at line #{location.Line} column #{location.Column} with token \"{token}\".");
                    Differences.Add(location);
                }
                else
                {
                    messageBuilder.Append(" at the end of the text.");
                }

                if (text != null)
                {
                    string clippedText = text.Length > MaxDiffTextLength ? text.Substring(0, MaxDiffTextLength) + "..." : text;
                    messageBuilder.Append($" The template text being compared was \"{clippedText}\".");
                }

                if (rule != null)
                {
                    messageBuilder.Append($" This occurred while processing rule: {rule}.");
                }

                if (lastOptionalDifference != null)
                {
                    messageBuilder.Append($"\n\tThe last optional block failed to match due to: {lastOptionalDifference.DifferenceMessage}");
                }

                DifferenceMessage = messageBuilder.ToString();
                IsDifferenceFound = true;
            }
        }
        #endregion
    }
}
