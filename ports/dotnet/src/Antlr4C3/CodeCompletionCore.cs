/*
 * Copyright © 2017 VMware, Inc. All Rights Reserved.
 *
 * SPDX-License-Identifier: MIT
 *
 * See LICENSE file for more info.
 *
 * .NET port of the Java antlr4-c3 CodeCompletionCore (ANTLR 4.7).
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;

namespace Antlr4C3
{
    public class CodeCompletionCore
    {
        private static readonly Dictionary<string, Dictionary<int, FollowSetsHolder>> followSetsByATN =
            new Dictionary<string, Dictionary<int, FollowSetsHolder>>();

        private static readonly string[] atnStateTypeMap =
        {
            "invalid",
            "basic",
            "rule start",
            "block start",
            "plus block start",
            "star block start",
            "token start",
            "rule stop",
            "block end",
            "star loop back",
            "star loop entry",
            "plus loop back",
            "loop end"
        };

        // Debugging options
        public bool showResult = false;
        public bool showDebugOutput = false;
        public bool debugOutputWithTransitions = false;
        public bool showRuleStack = false;

        public ISet<int> ignoredTokens = new HashSet<int>();
        public ISet<int> preferredRules = new HashSet<int>();
        public bool translateRulesTopDown = false;

        private readonly Parser parser;
        private readonly ATN atn;
        private readonly IVocabulary vocabulary;
        private readonly string[] ruleNames;
        private List<IToken> tokens;
        private List<int> precedenceStack;

        private int tokenStartIndex = 0;
        private int statesProcessed = 0;

        private readonly Dictionary<int, Dictionary<int, ISet<int>>> shortcutMap =
            new Dictionary<int, Dictionary<int, ISet<int>>>();

        private readonly CandidatesCollection candidates = new CandidatesCollection();

        public CodeCompletionCore(Parser parser)
        {
            this.parser = parser;
            this.atn = parser.Atn;
            this.vocabulary = parser.Vocabulary;
            this.ruleNames = parser.RuleNames;
        }

        public CandidatesCollection CollectCandidates(int caretTokenIndex, ParserRuleContext context)
        {
            this.shortcutMap.Clear();
            this.candidates.Rules.Clear();
            this.candidates.Tokens.Clear();
            this.statesProcessed = 0;
            this.precedenceStack = new List<int>();

            this.tokenStartIndex = context != null && context.Start != null ? context.Start.TokenIndex : 0;
            ITokenStream tokenStream = this.parser.TokenStream;

            this.tokens = new List<IToken>();
            int offset = this.tokenStartIndex;
            while (true)
            {
                IToken token = tokenStream.Get(offset++);
                if (token == null)
                {
                    break;
                }

                if (token.Channel == TokenConstants.DefaultChannel)
                {
                    this.tokens.Add(token);

                    if (token.TokenIndex >= caretTokenIndex || token.Type == TokenConstants.EOF)
                    {
                        break;
                    }
                }

                if (token.Type == TokenConstants.EOF)
                {
                    break;
                }
            }

            List<RuleWithStartToken> callStack = new List<RuleWithStartToken>();
            int startRule = context != null ? context.RuleIndex : 0;
            this.ProcessRule(this.atn.ruleToStartState[startRule], 0, callStack, 0, 0);

            if (this.showResult)
            {
                Console.WriteLine("States processed: " + this.statesProcessed);
                Console.WriteLine("\n\nCollected rules:\n");
                foreach (KeyValuePair<int, CandidateRule> entry in this.candidates.Rules)
                {
                    string path = string.Join(" ", entry.Value.RuleList.Select(ruleIndex => this.ruleNames[ruleIndex]));
                    Console.WriteLine(this.ruleNames[entry.Key] + ", path: " + path);
                }

                SortedSet<string> sortedTokens = new SortedSet<string>(StringComparer.Ordinal);
                foreach (KeyValuePair<int, List<int>> entry in this.candidates.Tokens)
                {
                    string value = this.vocabulary.GetDisplayName(entry.Key);
                    foreach (int following in entry.Value)
                    {
                        value += " " + this.vocabulary.GetDisplayName(following);
                    }
                    sortedTokens.Add(value);
                }

                Console.WriteLine("\n\nCollected tokens:\n");
                foreach (string symbol in sortedTokens)
                {
                    Console.WriteLine(symbol);
                }
                Console.WriteLine("\n\n");
            }

            return this.candidates;
        }

        private bool CheckPredicate(PredicateTransition transition)
        {
            return transition.Predicate.Eval(this.parser, ParserRuleContext.EmptyContext);
        }

        private bool TranslateStackToRuleIndex(List<RuleWithStartToken> ruleWithStartTokenList)
        {
            if (this.preferredRules.Count == 0)
            {
                return false;
            }

            if (this.translateRulesTopDown)
            {
                for (int i = ruleWithStartTokenList.Count - 1; i >= 0; i--)
                {
                    if (this.TranslateToRuleIndex(i, ruleWithStartTokenList))
                    {
                        return true;
                    }
                }
            }
            else
            {
                for (int i = 0; i < ruleWithStartTokenList.Count; i++)
                {
                    if (this.TranslateToRuleIndex(i, ruleWithStartTokenList))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TranslateToRuleIndex(int i, List<RuleWithStartToken> ruleWithStartTokenList)
        {
            RuleWithStartToken ruleWithStartToken = ruleWithStartTokenList[i];
            int ruleIndex = ruleWithStartToken.RuleIndex;
            if (this.preferredRules.Contains(ruleIndex))
            {
                int startTokenIndex = ruleWithStartToken.StartTokenIndex;
                List<int> path = ruleWithStartTokenList.Take(i).Select(r => r.RuleIndex).ToList();

                bool addNew = true;
                foreach (KeyValuePair<int, CandidateRule> entry in this.candidates.Rules)
                {
                    if (entry.Key != ruleIndex || entry.Value.RuleList.Count != path.Count)
                    {
                        continue;
                    }

                    bool samePath = true;
                    for (int j = 0; j < path.Count; j++)
                    {
                        if (path[j] != entry.Value.RuleList[j])
                        {
                            samePath = false;
                            break;
                        }
                    }

                    if (samePath)
                    {
                        addNew = false;
                        break;
                    }
                }

                if (addNew)
                {
                    this.candidates.Rules[ruleIndex] = new CandidateRule(startTokenIndex, path);
                    if (this.showDebugOutput)
                    {
                        Console.WriteLine("=====> collected: " + this.ruleNames[ruleIndex]);
                    }
                }

                return true;
            }

            return false;
        }

        private List<int> GetFollowingTokens(Transition transition)
        {
            List<int> result = new List<int>();
            Stack<ATNState> pipeline = new Stack<ATNState>();
            pipeline.Push(transition.target);

            while (pipeline.Count > 0)
            {
                ATNState state = pipeline.Pop();

                foreach (Transition outgoing in state.TransitionsArray)
                {
                    if (outgoing.TransitionType == TransitionType.ATOM)
                    {
                        if (!outgoing.IsEpsilon)
                        {
                            IntervalSet label = outgoing.Label;
                            if (label != null && label.Count == 1 && !this.ignoredTokens.Contains(label.MinElement))
                            {
                                result.Add(label.MinElement);
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
            List<FollowSetWithPath> sets = new List<FollowSetWithPath>();
            List<ATNState> stateStack = new List<ATNState>();
            List<int> ruleStack = new List<int>();
            bool isExhaustive = this.CollectFollowSets(start, stop, sets, stateStack, ruleStack);

            IntervalSet combined = new IntervalSet();
            foreach (FollowSetWithPath set in sets)
            {
                combined.AddAll(set.Intervals);
            }

            return new FollowSetsHolder(sets, combined, isExhaustive);
        }

        private bool CollectFollowSets(ATNState s, ATNState stopState, List<FollowSetWithPath> followSets,
            List<ATNState> stateStack, List<int> ruleStack)
        {
            if (stateStack.Contains(s))
            {
                return true;
            }
            stateStack.Add(s);

            if (s == stopState || s.StateType == StateType.RuleStop)
            {
                stateStack.RemoveAt(stateStack.Count - 1);
                return false;
            }

            bool isExhaustive = true;
            foreach (Transition transition in s.TransitionsArray)
            {
                if (transition.TransitionType == TransitionType.RULE)
                {
                    RuleTransition ruleTransition = (RuleTransition)transition;
                    if (ruleStack.Contains(ruleTransition.target.ruleIndex))
                    {
                        continue;
                    }

                    ruleStack.Add(ruleTransition.target.ruleIndex);
                    bool ruleFollowSetsIsExhaustive = this.CollectFollowSets(
                        transition.target, stopState, followSets, stateStack, ruleStack);
                    ruleStack.RemoveAt(ruleStack.Count - 1);

                    if (!ruleFollowSetsIsExhaustive)
                    {
                        bool nextStateFollowSetsIsExhaustive = this.CollectFollowSets(
                            ruleTransition.followState, stopState, followSets, stateStack, ruleStack);
                        isExhaustive &= nextStateFollowSetsIsExhaustive;
                    }
                }
                else if (transition.TransitionType == TransitionType.PREDICATE)
                {
                    if (this.CheckPredicate((PredicateTransition)transition))
                    {
                        bool nextStateFollowSetsIsExhaustive = this.CollectFollowSets(
                            transition.target, stopState, followSets, stateStack, ruleStack);
                        isExhaustive &= nextStateFollowSetsIsExhaustive;
                    }
                }
                else if (transition.IsEpsilon)
                {
                    bool nextStateFollowSetsIsExhaustive = this.CollectFollowSets(
                        transition.target, stopState, followSets, stateStack, ruleStack);
                    isExhaustive &= nextStateFollowSetsIsExhaustive;
                }
                else if (transition.TransitionType == TransitionType.WILDCARD)
                {
                    FollowSetWithPath set = new FollowSetWithPath();
                    set.Intervals = IntervalSet.Of(TokenConstants.MinUserTokenType, this.atn.maxTokenType);
                    set.Path = new List<int>(ruleStack);
                    followSets.Add(set);
                }
                else
                {
                    IntervalSet label = transition.Label;
                    if (label != null && label.Count != 0)
                    {
                        if (transition.TransitionType == TransitionType.NOT_SET)
                        {
                            label = label.Complement(TokenConstants.MinUserTokenType, this.atn.maxTokenType);
                        }
                        FollowSetWithPath set = new FollowSetWithPath();
                        set.Intervals = label;
                        set.Path = new List<int>(ruleStack);
                        set.Following = this.GetFollowingTokens(transition);
                        followSets.Add(set);
                    }
                }
            }
            stateStack.RemoveAt(stateStack.Count - 1);

            return isExhaustive;
        }

        private ISet<int> ProcessRule(RuleStartState startState, int tokenListIndex,
            List<RuleWithStartToken> callStack, int precedence, int indentation)
        {
            if (!this.shortcutMap.TryGetValue(startState.ruleIndex, out Dictionary<int, ISet<int>> positionMap))
            {
                positionMap = new Dictionary<int, ISet<int>>();
                this.shortcutMap[startState.ruleIndex] = positionMap;
            }

            if (positionMap.TryGetValue(tokenListIndex, out ISet<int> cached))
            {
                if (this.showDebugOutput)
                {
                    Console.WriteLine("=====> shortcut");
                }
                return cached;
            }

            ISet<int> result = new HashSet<int>();

            string parserKey = this.parser.GetType().FullName;
            if (!followSetsByATN.TryGetValue(parserKey, out Dictionary<int, FollowSetsHolder> setsPerState))
            {
                setsPerState = new Dictionary<int, FollowSetsHolder>();
                followSetsByATN[parserKey] = setsPerState;
            }

            if (!setsPerState.TryGetValue(startState.stateNumber, out FollowSetsHolder followSets))
            {
                RuleStopState stop = this.atn.ruleToStopState[startState.ruleIndex];
                followSets = this.DetermineFollowSets(startState, stop);
                setsPerState[startState.stateNumber] = followSets;
            }

            int startTokenIndex = this.tokens[tokenListIndex].TokenIndex;
            callStack.Add(new RuleWithStartToken(startTokenIndex, startState.ruleIndex));

            if (tokenListIndex >= this.tokens.Count - 1)
            {
                if (this.preferredRules.Contains(startState.ruleIndex))
                {
                    this.TranslateStackToRuleIndex(callStack);
                }
                else
                {
                    foreach (FollowSetWithPath set in followSets.Sets)
                    {
                        List<RuleWithStartToken> fullPath = new List<RuleWithStartToken>(callStack);
                        List<RuleWithStartToken> followSetPath = set.Path
                            .Select(ruleIndex => new RuleWithStartToken(startTokenIndex, ruleIndex))
                            .ToList();
                        fullPath.AddRange(followSetPath);

                        if (!this.TranslateStackToRuleIndex(fullPath))
                        {
                            foreach (int symbol in set.Intervals.ToList())
                            {
                                if (!this.ignoredTokens.Contains(symbol))
                                {
                                    if (this.showDebugOutput)
                                    {
                                        Console.WriteLine("=====> collected: " + this.vocabulary.GetDisplayName(symbol));
                                    }
                                    if (!this.candidates.Tokens.ContainsKey(symbol))
                                    {
                                        this.candidates.Tokens[symbol] = set.Following;
                                    }
                                    else
                                    {
                                        if (!this.candidates.Tokens[symbol].SequenceEqual(set.Following))
                                        {
                                            this.candidates.Tokens[symbol] = new List<int>();
                                        }
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
                int currentSymbol = this.tokens[tokenListIndex].Type;
                if (followSets.IsExhaustive && !followSets.Combined.Contains(currentSymbol))
                {
                    callStack.RemoveAt(callStack.Count - 1);
                    return result;
                }
            }

            if (startState.isPrecedenceRule)
            {
                this.precedenceStack.Add(precedence);
            }

            Stack<PipelineEntry> statePipeline = new Stack<PipelineEntry>();
            statePipeline.Push(new PipelineEntry(startState, tokenListIndex));

            while (statePipeline.Count > 0)
            {
                PipelineEntry currentEntry = statePipeline.Pop();
                this.statesProcessed++;

                int currentSymbol = this.tokens[currentEntry.TokenListIndex].Type;
                bool atCaret = currentEntry.TokenListIndex >= this.tokens.Count - 1;

                if (this.showDebugOutput)
                {
                    this.PrintDescription(indentation, currentEntry.State,
                        this.GenerateBaseDescription(currentEntry.State), currentEntry.TokenListIndex);
                    if (this.showRuleStack)
                    {
                        this.PrintRuleState(callStack);
                    }
                }

                if (currentEntry.State.StateType == StateType.RuleStop)
                {
                    result.Add(currentEntry.TokenListIndex);
                    continue;
                }

                foreach (Transition transition in currentEntry.State.TransitionsArray)
                {
                    switch (transition.TransitionType)
                    {
                        case TransitionType.RULE:
                        {
                            RuleTransition ruleTransition = (RuleTransition)transition;
                            ISet<int> endStatus = this.ProcessRule((RuleStartState)transition.target,
                                currentEntry.TokenListIndex, callStack, ruleTransition.precedence, indentation + 1);
                            foreach (int position in endStatus)
                            {
                                statePipeline.Push(new PipelineEntry(ruleTransition.followState, position));
                            }
                            break;
                        }

                        case TransitionType.PREDICATE:
                        {
                            if (this.CheckPredicate((PredicateTransition)transition))
                            {
                                statePipeline.Push(new PipelineEntry(transition.target, currentEntry.TokenListIndex));
                            }
                            break;
                        }

                        case TransitionType.PRECEDENCE:
                        {
                            PrecedencePredicateTransition predTransition = (PrecedencePredicateTransition)transition;
                            if (predTransition.precedence >= this.precedenceStack[this.precedenceStack.Count - 1])
                            {
                                statePipeline.Push(new PipelineEntry(transition.target, currentEntry.TokenListIndex));
                            }
                            break;
                        }

                        case TransitionType.WILDCARD:
                        {
                            if (atCaret)
                            {
                                if (!this.TranslateStackToRuleIndex(callStack))
                                {
                                    foreach (int token in IntervalSet.Of(TokenConstants.MinUserTokenType, this.atn.maxTokenType).ToList())
                                    {
                                        if (!this.ignoredTokens.Contains(token))
                                        {
                                            this.candidates.Tokens[token] = new List<int>();
                                        }
                                    }
                                }
                            }
                            else
                            {
                                statePipeline.Push(new PipelineEntry(transition.target, currentEntry.TokenListIndex + 1));
                            }
                            break;
                        }

                        default:
                        {
                            if (transition.IsEpsilon)
                            {
                                statePipeline.Push(new PipelineEntry(transition.target, currentEntry.TokenListIndex));
                                continue;
                            }

                            IntervalSet set = transition.Label;
                            if (set != null && set.Count != 0)
                            {
                                if (transition.TransitionType == TransitionType.NOT_SET)
                                {
                                    set = set.Complement(TokenConstants.MinUserTokenType, this.atn.maxTokenType);
                                }
                                if (atCaret)
                                {
                                    if (!this.TranslateStackToRuleIndex(callStack))
                                    {
                                        List<int> list = new List<int>(set.ToList());
                                        bool hasTokenSequence = list.Count == 1;
                                        foreach (int symbol in list)
                                        {
                                            if (!this.ignoredTokens.Contains(symbol))
                                            {
                                                if (this.showDebugOutput)
                                                {
                                                    Console.WriteLine("=====> collected: " +
                                                        this.vocabulary.GetDisplayName(symbol));
                                                }

                                                List<int> followingTokens = hasTokenSequence
                                                    ? this.GetFollowingTokens(transition)
                                                    : new List<int>();
                                                if (!this.candidates.Tokens.ContainsKey(symbol))
                                                {
                                                    this.candidates.Tokens[symbol] = followingTokens;
                                                }
                                                else
                                                {
                                                    this.candidates.Tokens[symbol] =
                                                        LongestCommonPrefix(followingTokens, this.candidates.Tokens[symbol]);
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (set.Contains(currentSymbol))
                                    {
                                        if (this.showDebugOutput)
                                        {
                                            Console.WriteLine("=====> consumed: " +
                                                this.vocabulary.GetDisplayName(currentSymbol));
                                        }
                                        statePipeline.Push(new PipelineEntry(transition.target,
                                            currentEntry.TokenListIndex + 1));
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            }

            callStack.RemoveAt(callStack.Count - 1);
            if (startState.isPrecedenceRule)
            {
                this.precedenceStack.RemoveAt(this.precedenceStack.Count - 1);
            }

            positionMap[tokenListIndex] = result;
            return result;
        }

        private string GenerateBaseDescription(ATNState state)
        {
            string stateValue = state.stateNumber == ATNState.InvalidStateNumber ? "Invalid"
                : state.stateNumber.ToString();
            string typeName = atnStateTypeMap[(int)state.StateType];

            return "[" + stateValue + " " + typeName + "] in " + this.ruleNames[state.ruleIndex];
        }

        private void PrintDescription(int indentation, ATNState state, string baseDescription, int tokenIndex)
        {
            string indent = string.Concat(Enumerable.Repeat("  ", indentation));
            System.Text.StringBuilder output = new System.Text.StringBuilder(indent);

            System.Text.StringBuilder transitionDescription = new System.Text.StringBuilder();
            if (this.debugOutputWithTransitions)
            {
                foreach (Transition transition in state.TransitionsArray)
                {
                    System.Text.StringBuilder labels = new System.Text.StringBuilder();
                    IntervalSet label = transition.Label;
                    List<int> symbols = label != null ? new List<int>(label.ToList()) : new List<int>();

                    if (symbols.Count > 2)
                    {
                        labels.Append(this.vocabulary.GetDisplayName(symbols[0]))
                            .Append(" .. ")
                            .Append(this.vocabulary.GetDisplayName(symbols[symbols.Count - 1]));
                    }
                    else
                    {
                        foreach (int symbol in symbols)
                        {
                            if (labels.Length > 0)
                            {
                                labels.Append(", ");
                            }
                            labels.Append(this.vocabulary.GetDisplayName(symbol));
                        }
                    }

                    if (labels.Length == 0)
                    {
                        labels.Append("ε");
                    }

                    string typeName = atnStateTypeMap[(int)transition.target.StateType];
                    transitionDescription.Append("\n").Append(indent).Append("\t(").Append(labels)
                        .Append(") [").Append(transition.target.stateNumber).Append(" ")
                        .Append(typeName).Append("] in ").Append(this.ruleNames[transition.target.ruleIndex]);
                }
            }

            if (tokenIndex >= this.tokens.Count - 1)
            {
                output.Append("<<").Append(this.tokenStartIndex + tokenIndex).Append(">> ");
            }
            else
            {
                output.Append("<").Append(this.tokenStartIndex + tokenIndex).Append("> ");
            }
            Console.WriteLine(output.ToString() + "Current state: " + baseDescription + transitionDescription);
        }

        private void PrintRuleState(List<RuleWithStartToken> stack)
        {
            if (stack.Count == 0)
            {
                Console.WriteLine("<empty stack>");
                return;
            }

            foreach (RuleWithStartToken rule in stack)
            {
                Console.WriteLine(this.ruleNames[rule.RuleIndex]);
            }
        }

        private static List<int> LongestCommonPrefix(List<int> a, List<int> b)
        {
            int minLength = Math.Min(a.Count, b.Count);
            List<int> result = new List<int>();

            for (int i = 0; i < minLength; i++)
            {
                if (a[i] == b[i])
                {
                    result.Add(a[i]);
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        // Supporting classes
        public class CandidatesCollection
        {
            public Dictionary<int, List<int>> Tokens = new Dictionary<int, List<int>>();
            public Dictionary<int, CandidateRule> Rules = new Dictionary<int, CandidateRule>();
        }

        public class CandidateRule
        {
            public int StartTokenIndex;
            public List<int> RuleList;

            public CandidateRule(int startTokenIndex, List<int> ruleList)
            {
                this.StartTokenIndex = startTokenIndex;
                this.RuleList = ruleList;
            }
        }

        public class RuleWithStartToken
        {
            public int StartTokenIndex;
            public int RuleIndex;

            public RuleWithStartToken(int startTokenIndex, int ruleIndex)
            {
                this.StartTokenIndex = startTokenIndex;
                this.RuleIndex = ruleIndex;
            }
        }

        private class FollowSetWithPath
        {
            public IntervalSet Intervals;
            public List<int> Path = new List<int>();
            public List<int> Following = new List<int>();
        }

        private class FollowSetsHolder
        {
            public List<FollowSetWithPath> Sets;
            public IntervalSet Combined;
            public bool IsExhaustive;

            public FollowSetsHolder(List<FollowSetWithPath> sets, IntervalSet combined, bool isExhaustive)
            {
                this.Sets = sets;
                this.Combined = combined;
                this.IsExhaustive = isExhaustive;
            }
        }

        private class PipelineEntry
        {
            public ATNState State;
            public int TokenListIndex;

            public PipelineEntry(ATNState state, int tokenListIndex)
            {
                this.State = state;
                this.TokenListIndex = tokenListIndex;
            }
        }
    }
}



