/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antlr4CodeCompletion;

public struct CandidateRule
{
    public int StartTokenIndex;
    public List<int> RuleList;
}

public struct RuleWithStartToken
{
    public int StartTokenIndex;
    public int RuleIndex;
}

/// <summary>
/// All the candidates which have been found. Tokens and rules are separated.
/// Token entries include a list of tokens that directly follow them.
/// Rule entries include the index of the starting token within the evaluated rule,
/// along with a call stack of rules found during evaluation.
/// </summary>
public class CandidatesCollection
{
    public Dictionary<int, List<int>> Tokens { get; } = new();
    public Dictionary<int, CandidateRule> Rules { get; } = new();

    public void Clear()
    {
        Tokens.Clear();
        Rules.Clear();
    }
}

/// <summary>
/// The main class for doing the collection process.
/// </summary>
public class CodeCompletionCore
{
    private class FollowSetWithPath
    {
        public IntervalSet Intervals { get; set; } = new();
        public List<int> Path { get; set; } = new();
        public List<int> Following { get; set; } = new();
    }

    private class FollowSetsHolder
    {
        public List<FollowSetWithPath> Sets { get; set; }
        public IntervalSet Combined { get; set; }
        public bool IsExhaustive { get; set; }
    }

    private struct PipelineEntry
    {
        public ATNState State;
        public int TokenListIndex;
    }

    private static readonly Dictionary<string, Dictionary<int, FollowSetsHolder>> s_followSetsByATN = new();

    #region Debugging Options
    /// <summary>
    /// Not dependent on ShowDebugOutput. Prints the collected rules + tokens to the console.
    /// </summary>
    public bool ShowResult { get; set; }

    /// <summary>
    /// Enables printing ATN state info to the console.
    /// </summary>
    public bool ShowDebugOutput { get; set; }

    /// <summary>
    /// Only relevant when ShowDebugOutput is true. Enables transition printing for a state.
    /// </summary>
    public bool DebugOutputWithTransitions { get; set; }

    /// <summary>
    /// Also depends on showDebugOutput. Enables call stack printing for each rule recursion.
    /// </summary>
    public bool ShowRuleStack { get; set; }
    #endregion

    #region Tailoring of the result
    /// <summary>
    /// Tokens which should not appear in the candidates set.
    /// </summary>
    public HashSet<int> IgnoredTokens { get; }

    /// <summary>
    /// Rules which replace any candidate token they contain.
    /// This allows returning descriptive rules (e.g., className, instead of ID/identifier).
    /// </summary>
    public HashSet<int> PreferredRules { get; }

    /// <summary>
    /// Specifies if preferred rules should be translated top-down (higher index rule returns first) or
    /// bottom-up (lower index rule returns first).
    /// </summary>
    public bool TranslateRulesTopDown { get; set; }
    #endregion

    private readonly Parser _parser;
    private readonly ATN _atn;
    private readonly IVocabulary _vocabulary;
    private readonly string[] _ruleNames;
    private List<IToken> _tokens;

    private readonly Stack<int> _precedenceStack = new();
    private int _tokenStartIndex;
    private int _statesProcessed;

    private readonly Dictionary<int, Dictionary<int, HashSet<int>>> _shortcutMap = new();
    private readonly CandidatesCollection _candidates = new();

    public CodeCompletionCore(Parser parser)
    {
        _parser = parser;
        _atn = parser.Atn;
        _vocabulary = parser.Vocabulary;
        _ruleNames = parser.RuleNames;
        IgnoredTokens = new HashSet<int>();
        PreferredRules = new HashSet<int>();
    }

    /// <summary>
    /// This is the main entry point. The caret token index specifies the token stream index for the token which
    /// currently covers the caret (or any other position you want to get code completion candidates for).
    /// Optionally you can pass in a parser rule context which limits the ATN walk to only that or called rules.
    /// This can significantly speed up the retrieval process but might miss some candidates (if they are outside of
    /// the given context).
    /// </summary>
    /// <param name="caretTokenIndex">The index of the token at the caret position.</param>
    /// <param name="context">An optional parser rule context to limit the search space.</param>
    /// <returns>The collection of completion candidates.</returns>
    public CandidatesCollection CollectCandidates(int caretTokenIndex, ParserRuleContext context = null)
    {
        _shortcutMap.Clear();
        _candidates.Clear();
        _statesProcessed = 0;
        _precedenceStack.Clear();

        _tokenStartIndex = context?.Start?.TokenIndex ?? 0;
        ITokenStream tokenStream = _parser.TokenStream;

        _tokens = new List<IToken>();
        int offset = _tokenStartIndex;
        while (true)
        {
            IToken token = tokenStream.Get(offset++);
            if (token == null) break;
            if (token.Channel == TokenConstants.DefaultChannel)
            {
                _tokens.Add(token);
                if (token.TokenIndex >= caretTokenIndex || token.Type == TokenConstants.EOF) break;
            }
            if (token.Type == TokenConstants.EOF) break;
        }

        var callStack = new List<RuleWithStartToken>();
        int startRule = context?.RuleIndex ?? 0;
        ProcessRule(_atn.ruleToStartState[startRule], 0, callStack, 0, 0);

        if (ShowResult)
        {
            Console.WriteLine($"States processed: {_statesProcessed}");
            Console.WriteLine("\n\nCollected rules:\n");
            foreach (var rule in _candidates.Rules)
            {
                string path = string.Join(" ", rule.Value.RuleList.Select(token => _ruleNames[token]));
                Console.WriteLine($"{_ruleNames[rule.Key]}, path: {path}");
            }

            var sortedTokens = new SortedSet<string>();
            foreach (var token in _candidates.Tokens)
            {
                string value = _vocabulary.GetDisplayName(token.Key) ?? "";
                value += string.Join("", token.Value.Select(following => " " + _vocabulary.GetDisplayName(following)));
                sortedTokens.Add(value);
            }

            Console.WriteLine("\n\nCollected tokens:\n");
            foreach (string symbol in sortedTokens)
            {
                Console.WriteLine(symbol);
            }
            Console.WriteLine("\n\n");
        }

        return _candidates;
    }

    private bool CheckPredicate(PredicateTransition transition)
    {
        return transition.Predicate.Eval(_parser, ParserRuleContext.EmptyContext);
    }

    private bool TranslateStackToRuleIndex(List<RuleWithStartToken> ruleWithStartTokenList)
    {
        if (PreferredRules.Count == 0) return false;

        if (TranslateRulesTopDown)
        {
            for (int i = ruleWithStartTokenList.Count - 1; i >= 0; i--)
            {
                if (TranslateToRuleIndex(i, ruleWithStartTokenList)) return true;
            }
        }
        else
        {
            for (int i = 0; i < ruleWithStartTokenList.Count; i++)
            {
                if (TranslateToRuleIndex(i, ruleWithStartTokenList)) return true;
            }
        }

        return false;
    }

    private bool TranslateToRuleIndex(int i, List<RuleWithStartToken> ruleWithStartTokenList)
    {
        var ruleInfo = ruleWithStartTokenList[i];
        if (PreferredRules.Contains(ruleInfo.RuleIndex))
        {
            var path = ruleWithStartTokenList.GetRange(0, i).Select(r => r.RuleIndex).ToList();
            bool addNew = true;

            if (_candidates.Rules.TryGetValue(ruleInfo.RuleIndex, out var existingRule))
            {
                if (path.SequenceEqual(existingRule.RuleList))
                {
                    addNew = false;
                }
            }

            if (addNew)
            {
                _candidates.Rules[ruleInfo.RuleIndex] = new CandidateRule
                {
                    StartTokenIndex = ruleInfo.StartTokenIndex,
                    RuleList = path
                };
                if (ShowDebugOutput)
                {
                    Console.WriteLine($"=====> collected: {_ruleNames[ruleInfo.RuleIndex]}");
                }
            }
            return true;
        }
        return false;
    }

    private List<int> GetFollowingTokens(Transition transition)
    {
        var result = new List<int>();
        var pipeline = new Stack<ATNState>();
        pipeline.Push(transition.target);

        while (pipeline.Count > 0)
        {
            var state = pipeline.Pop();
            foreach (var outgoing in state.TransitionsArray)
            {
                if (outgoing.TransitionType == TransitionType.ATOM)
                {
                    if (!outgoing.IsEpsilon)
                    {
                        var list = outgoing.Label.ToList();
                        if (list.Count == 1 && !IgnoredTokens.Contains(list[0]))
                        {
                            result.Add(list[0]);
                            pipeline.Push(outgoing.target);
                        }
                    }
                    else
                    {
                        pipeline.Push(outgoing.target);
                    }
                }
            }
        }
        return result;
    }

    private FollowSetsHolder DetermineFollowSets(ATNState start, ATNState stop)
    {
        var sets = new List<FollowSetWithPath>();
        var stateStack = new List<ATNState>();
        var ruleStack = new List<int>();
        bool isExhaustive = CollectFollowSets(start, stop, sets, stateStack, ruleStack);

        var combined = new IntervalSet();
        foreach (var set in sets)
        {
            combined.AddAll(set.Intervals);
        }
        return new FollowSetsHolder { Sets = sets, IsExhaustive = isExhaustive, Combined = combined };
    }

    private bool CollectFollowSets(ATNState s, ATNState stopState, List<FollowSetWithPath> followSets,
        List<ATNState> stateStack, List<int> ruleStack)
    {
        if (stateStack.Contains(s)) return true;
        stateStack.Add(s);

        if (s == stopState || s.StateType == StateType.RuleStop)
        {
            stateStack.RemoveAt(stateStack.Count - 1);
            return false;
        }

        bool isExhaustive = true;
        foreach (var transition in s.TransitionsArray)
        {
            if (transition.TransitionType == TransitionType.RULE)
            {
                var ruleTransition = (RuleTransition)transition;
                if (ruleStack.Contains(ruleTransition.ruleIndex)) continue;

                ruleStack.Add(ruleTransition.ruleIndex);
                bool ruleFollowSetsIsExhaustive = CollectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
                ruleStack.RemoveAt(ruleStack.Count - 1);

                if (!ruleFollowSetsIsExhaustive)
                {
                    bool nextStateFollowSetsIsExhaustive = CollectFollowSets(ruleTransition.followState, stopState, followSets, stateStack, ruleStack);
                    isExhaustive &= nextStateFollowSetsIsExhaustive;
                }
            }
            else if (transition.TransitionType == TransitionType.PREDICATE)
            {
                if (CheckPredicate((PredicateTransition)transition))
                {
                    isExhaustive &= CollectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
                }
            }
            else if (transition.IsEpsilon)
            {
                isExhaustive &= CollectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
            }
            else if (transition.TransitionType == TransitionType.WILDCARD)
            {
                var set = new FollowSetWithPath
                {
                    Intervals = IntervalSet.Of(TokenConstants.MinUserTokenType, _atn.maxTokenType),
                    Path = new List<int>(ruleStack)
                };
                followSets.Add(set);
            }
            else
            {
                var label = transition.Label;
                if (label != null && !label.IsNil)
                {
                    if (transition.TransitionType == TransitionType.NOT_SET)
                    {
                        label = label.Complement(IntervalSet.Of(TokenConstants.MinUserTokenType, _atn.maxTokenType));
                    }
                    var set = new FollowSetWithPath
                    {
                        Intervals = label,
                        Path = new List<int>(ruleStack),
                        Following = GetFollowingTokens(transition)
                    };
                    followSets.Add(set);
                }
            }
        }
        stateStack.RemoveAt(stateStack.Count - 1);
        return isExhaustive;
    }

    private HashSet<int> ProcessRule(RuleStartState startState, int tokenListIndex, List<RuleWithStartToken> callStack,
        int precedence, int indentation)
    {
        if (!_shortcutMap.TryGetValue(startState.ruleIndex, out var positionMap))
        {
            positionMap = new Dictionary<int, HashSet<int>>();
            _shortcutMap[startState.ruleIndex] = positionMap;
        }
        else
        {
            if (positionMap.TryGetValue(tokenListIndex, out var value))
            {
                if (ShowDebugOutput) Console.WriteLine("=====> shortcut");
                return value;
            }
        }

        var result = new HashSet<int>();

        if (!s_followSetsByATN.TryGetValue(_parser.GetType().FullName, out var setsPerState))
        {
            setsPerState = new Dictionary<int, FollowSetsHolder>();
            s_followSetsByATN[_parser.GetType().FullName] = setsPerState;
        }

        if (!setsPerState.TryGetValue(startState.stateNumber, out var followSets))
        {
            var stop = _atn.ruleToStopState[startState.ruleIndex];
            followSets = DetermineFollowSets(startState, stop);
            setsPerState[startState.stateNumber] = followSets;
        }

        int startTokenIndex = _tokens[tokenListIndex].TokenIndex;
        callStack.Add(new RuleWithStartToken { StartTokenIndex = startTokenIndex, RuleIndex = startState.ruleIndex });

        if (tokenListIndex >= _tokens.Count - 1) // At caret?
        {
            if (PreferredRules.Contains(startState.ruleIndex))
            {
                TranslateStackToRuleIndex(callStack);
            }
            else
            {
                foreach (var set in followSets.Sets)
                {
                    var fullPath = new List<RuleWithStartToken>(callStack);
                    var followSetPath = set.Path.Select(path => new RuleWithStartToken
                    {
                        StartTokenIndex = startTokenIndex,
                        RuleIndex = path
                    });
                    fullPath.AddRange(followSetPath);

                    if (!TranslateStackToRuleIndex(fullPath))
                    {
                        foreach (int symbol in set.Intervals.ToList())
                        {
                            if (!IgnoredTokens.Contains(symbol))
                            {
                                if (ShowDebugOutput) Console.WriteLine($"=====> collected: {_vocabulary.GetDisplayName(symbol)}");
                                if (!_candidates.Tokens.ContainsKey(symbol))
                                {
                                    _candidates.Tokens[symbol] = set.Following;
                                }
                                else
                                {
                                    _candidates.Tokens[symbol] = new List<int>(); // More than one following list.
                                }
                            }
                        }
                    }
                }
            }

            if (!followSets.IsExhaustive)
            {
                result.Add(tokenListIndex);
            }

            callStack.RemoveAt(callStack.Count - 1);
            return result;
        }
        else
        {
            int currentSymbol = _tokens[tokenListIndex].Type;
            if (followSets.IsExhaustive && !followSets.Combined.Contains(currentSymbol))
            {
                callStack.RemoveAt(callStack.Count - 1);
                return result;
            }
        }

        if (startState.IsLeftRecursiveRule)
        {
            _precedenceStack.Push(precedence);
        }

        var statePipeline = new Stack<PipelineEntry>();
        statePipeline.Push(new PipelineEntry { State = startState, TokenListIndex = tokenListIndex });

        while (statePipeline.Count > 0)
        {
            var currentEntry = statePipeline.Pop();
            _statesProcessed++;

            int currentSymbol = _tokens[currentEntry.TokenListIndex].Type;
            bool atCaret = currentEntry.TokenListIndex >= _tokens.Count - 1;

            if (ShowDebugOutput)
            {
                PrintDescription(indentation, currentEntry.State, GenerateBaseDescription(currentEntry.State), currentEntry.TokenListIndex);
                if (ShowRuleStack) PrintRuleState(callStack);
            }

            if (currentEntry.State.StateType == StateType.RuleStop)
            {
                result.Add(currentEntry.TokenListIndex);
                continue;
            }

            foreach (var transition in currentEntry.State.TransitionsArray)
            {
                switch (transition.TransitionType)
                {
                    case TransitionType.RULE:
                        var ruleTransition = (RuleTransition)transition;
                        var endStatus = ProcessRule((RuleStartState)transition.target, currentEntry.TokenListIndex, callStack, ruleTransition.precedence, indentation + 1);
                        foreach (var position in endStatus)
                        {
                            statePipeline.Push(new PipelineEntry { State = ruleTransition.followState, TokenListIndex = position });
                        }
                        break;
                    case TransitionType.PREDICATE:
                        if (CheckPredicate((PredicateTransition)transition))
                        {
                            statePipeline.Push(new PipelineEntry { State = transition.target, TokenListIndex = currentEntry.TokenListIndex });
                        }
                        break;
                    case TransitionType.PRECEDENCE:
                        var predTransition = (PrecedencePredicateTransition)transition;
                        if (predTransition.precedence >= _precedenceStack.Peek())
                        {
                            statePipeline.Push(new PipelineEntry { State = transition.target, TokenListIndex = currentEntry.TokenListIndex });
                        }
                        break;
                    case TransitionType.WILDCARD:
                        if (atCaret)
                        {
                            if (!TranslateStackToRuleIndex(callStack))
                            {
                                foreach (int token in IntervalSet.Of(TokenConstants.MinUserTokenType, _atn.maxTokenType).ToList())
                                {
                                    if (!IgnoredTokens.Contains(token)) _candidates.Tokens[token] = new List<int>();
                                }
                            }
                        }
                        else
                        {
                            statePipeline.Push(new PipelineEntry { State = transition.target, TokenListIndex = currentEntry.TokenListIndex + 1 });
                        }
                        break;
                    default:
                        if (transition.IsEpsilon)
                        {
                            statePipeline.Push(new PipelineEntry { State = transition.target, TokenListIndex = currentEntry.TokenListIndex });
                            continue;
                        }
                        var set = transition.Label;
                        if (set != null && !set.IsNil)
                        {
                            if (transition.TransitionType == TransitionType.NOT_SET)
                            {
                                set = set.Complement(IntervalSet.Of(TokenConstants.MinUserTokenType, _atn.maxTokenType));
                            }
                            if (atCaret)
                            {
                                if (!TranslateStackToRuleIndex(callStack))
                                {
                                    var list = set.ToList();
                                    bool hasTokenSequence = list.Count == 1;
                                    foreach (var symbol in list)
                                    {
                                        if (!IgnoredTokens.Contains(symbol))
                                        {
                                            if (ShowDebugOutput) Console.WriteLine($"=====> collected: {_vocabulary.GetDisplayName(symbol)}");
                                            var followingTokens = hasTokenSequence ? GetFollowingTokens(transition) : new List<int>();

                                            if (_candidates.Tokens.TryGetValue(symbol, out var existingFollowing))
                                            {
                                                _candidates.Tokens[symbol] = LongestCommonPrefix(followingTokens, existingFollowing);
                                            }
                                            else
                                            {
                                                _candidates.Tokens[symbol] = followingTokens;
                                            }
                                        }
                                    }
                                }
                            }
                            else if (set.Contains(currentSymbol))
                            {
                                if (ShowDebugOutput) Console.WriteLine($"=====> consumed: {_vocabulary.GetDisplayName(currentSymbol)}");
                                statePipeline.Push(new PipelineEntry { State = transition.target, TokenListIndex = currentEntry.TokenListIndex + 1 });
                            }
                        }
                        break;
                }
            }
        }

        callStack.RemoveAt(callStack.Count - 1);
        if (startState.isLeftRecursiveRule)
        {
            _precedenceStack.Pop();
        }

        positionMap[tokenListIndex] = result;
        return result;
    }

    private List<T> LongestCommonPrefix<T>(List<T> a, List<T> b)
    {
        if (a == null || b == null) return new List<T>();

        int minLength = Math.Min(a.Count, b.Count);
        for (int i = 0; i < minLength; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
            {
                return a.GetRange(0, i);
            }
        }
        return a.GetRange(0, minLength);
    }

    private string GenerateBaseDescription(ATNState state)
    {
        string stateValue = state.stateNumber == ATNState.InvalidStateNumber ? "Invalid" : state.stateNumber.ToString();
        return $"[{stateValue} {state.StateType}] in {_ruleNames[state.ruleIndex]}";
    }

    private void PrintDescription(int indentation, ATNState state, string baseDescription, int tokenIndex)
    {
        string indent = new(' ', indentation * 2);
        string output = indent;
        string transitionDescription = "";

        if (DebugOutputWithTransitions)
        {
            foreach (var transition in state.TransitionsArray)
            {
                string labels;
                var symbols = transition.Label?.ToList() ?? new List<int>();
                if (symbols.Count > 2)
                {
                    labels = $"{_vocabulary.GetDisplayName(symbols[0])} .. {_vocabulary.GetDisplayName(symbols[symbols.Count - 1])}";
                }
                else
                {
                    labels = string.Join(", ", symbols.Select(s => _vocabulary.GetDisplayName(s)));
                }

                if (string.IsNullOrEmpty(labels)) labels = "ε";
                transitionDescription += $"\n{indent}\t({labels}) [{transition.target.stateNumber} " +
                    $"{transition.target.StateType}] in {_ruleNames[transition.target.ruleIndex]}";
            }
        }

        if (tokenIndex >= _tokens.Count - 1)
        {
            output += $"<<{_tokenStartIndex + tokenIndex}>> ";
        }
        else
        {
            output += $"<{_tokenStartIndex + tokenIndex}> ";
        }
        Console.WriteLine($"{output}Current state: {baseDescription}{transitionDescription}");
    }

    private void PrintRuleState(List<RuleWithStartToken> stack)
    {
        if (stack.Count == 0)
        {
            Console.WriteLine("<empty stack>");
            return;
        }

        foreach (var rule in stack)
        {
            Console.WriteLine(_ruleNames[rule.RuleIndex]);
        }
    }
}