using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;

namespace AntlrC3
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Atn;
    using Antlr4.Runtime.Misc;
    using RuleList = List<int>;
    using TokenList = List<int>;
    using RuleEndStatus = HashSet<int>;
    using FollowSetsPerState = Dictionary<int, FollowSetsHolder>;

    public class CandidateRule
    {
        public int StartTokenIndex { get; set; }
        public RuleList RuleList { get; set; } = new();
    }

    public class RuleWithStartToken
    {
        public int StartTokenIndex { get; set; }
        public int RuleIndex { get; set; }
    }

    /// <summary>
    /// Все найденные кандидаты — токены и правила.
    /// </summary>
    public class CandidatesCollection
    {
        public Dictionary<int, TokenList> Tokens { get; } = new();
        public Dictionary<int, CandidateRule> Rules { get; } = new();
    }

    class FollowSetWithPath
    {
        public IntervalSet Intervals { get; set; }
        public RuleList Path { get; set; } = new();
        public TokenList Following { get; set; } = new();
    }

    class FollowSetsHolder
    {
        public List<FollowSetWithPath> Sets { get; set; }
        public IntervalSet Combined { get; set; }
        public bool IsExhaustive { get; set; }
    }

    class PipelineEntry
    {
        public ATNState State { get; set; }
        public int TokenListIndex { get; set; }
    }


    /// <summary>
    /// Основной класс для сбора кандидатов автодополнения.
    /// </summary>
    public class CodeCompletionCore
    {
        private static readonly Dictionary<string, FollowSetsPerState> followSetsByATN = new();

        private static readonly string[] atnStateTypeMap = new[]
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

        public bool ShowResult { get; set; } = false;
        public bool ShowDebugOutput { get; set; } = false;
        public bool DebugOutputWithTransitions { get; set; } = false;
        public bool ShowRuleStack { get; set; } = false;

        public HashSet<int> IgnoredTokens { get; set; } = new();
        public HashSet<int> PreferredRules { get; set; } = new();
        public bool TranslateRulesTopDown { get; set; } = false;

        private readonly Parser parser;
        private readonly ATN atn;
        private readonly IVocabulary vocabulary;
        private readonly string[] ruleNames;
        private List<IToken> tokens;
        private List<int> precedenceStack;

        private int tokenStartIndex = 0;
        private int statesProcessed = 0;

        private readonly Dictionary<int, Dictionary<int, RuleEndStatus>> shortcutMap = new();
        private readonly CandidatesCollection candidates = new();

        public CodeCompletionCore(Parser parser)
        {
            this.parser = parser;
            this.atn = parser.Atn;
            this.vocabulary = parser.Vocabulary;
            this.ruleNames = parser.RuleNames;
        }

        public CandidatesCollection CollectCandidates(int caretTokenIndex, ParserRuleContext context = null)
        {
            shortcutMap.Clear();
            candidates.Rules.Clear();
            candidates.Tokens.Clear();
            statesProcessed = 0;
            precedenceStack = new();

            tokenStartIndex = context?.Start?.TokenIndex ?? 0;
            var tokenStream = parser.TokenStream;

            tokens = new List<IToken>();
            int offset = tokenStartIndex;
            while (true)
            {
                var token = tokenStream.Get(offset++);
                if (token == null)
                    break;

                if (token.Channel == TokenConstants.DefaultChannel)
                {
                    tokens.Add(token);
                    if (token.TokenIndex >= caretTokenIndex || token.Type == TokenConstants.EOF)
                        break;
                }

                if (token.Type == TokenConstants.EOF)
                    break;
            }

            var callStack = new List<RuleWithStartToken>();
            int startRule = context != null ? context.RuleIndex : 0;
            ProcessRule((RuleStartState)atn.ruleToStartState[startRule], 0, callStack, 0, 0);

            if (ShowResult)
            {
                Console.WriteLine($"States processed: {statesProcessed}");
                Console.WriteLine("\nCollected rules:");
                foreach (var rule in candidates.Rules)
                {
                    string path = string.Join(" ", rule.Value.RuleList.Select(i => ruleNames[i]));
                    Console.WriteLine($"{ruleNames[rule.Key]} path: {path}");
                }

                var sortedTokens = new SortedSet<string>();
                foreach (var token in candidates.Tokens)
                {
                    string value = vocabulary.GetDisplayName(token.Key);
                    foreach (var following in token.Value)
                        value += " " + vocabulary.GetDisplayName(following);
                    sortedTokens.Add(value);
                }

                Console.WriteLine("\nCollected tokens:");
                foreach (var symbol in sortedTokens)
                    Console.WriteLine(symbol);
            }

            return candidates;
        }

        private bool CheckPredicate(PredicateTransition transition)
        {
            return transition.Predicate.Eval(parser, ParserRuleContext.EmptyContext);
        }

        private bool TranslateStackToRuleIndex(List<RuleWithStartToken> stack)
        {
            if (PreferredRules.Count == 0)
                return false;

            IEnumerable<int> range = TranslateRulesTopDown
            ? Enumerable.Range(0, stack.Count).Reverse()
            : Enumerable.Range(0, stack.Count);

            foreach (var i in range)
            {
                if (TranslateToRuleIndex(i, stack))
                    return true;
            }

            return false;
        }

        private bool TranslateToRuleIndex(int i, List<RuleWithStartToken> list)
        {
            var ruleIndex = list[i].RuleIndex;
            var startTokenIndex = list[i].StartTokenIndex;

            if (PreferredRules.Contains(ruleIndex))
            {
                var path = list.Take(i).Select(x => x.RuleIndex).ToList();
                bool addNew = true;

                foreach (var rule in candidates.Rules)
                {
                    var existing = rule.Value;
                    if (rule.Key == ruleIndex && existing.RuleList.SequenceEqual(path))
                    {
                        addNew = false;
                        break;
                    }
                }

                if (addNew)
                {
                    candidates.Rules[ruleIndex] = new CandidateRule
                    {
                        StartTokenIndex = startTokenIndex,
                        RuleList = path
                    };
                    if (ShowDebugOutput)
                        Console.WriteLine("=====> collected: " + ruleNames[ruleIndex]);
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
                for (int ti = 0; ti < state.NumberOfTransitions; ++ti)
                {
                    var outgoing = state.Transition(ti);
                    if (outgoing.TransitionType == TransitionType.ATOM)
                    {
                        if (!outgoing.IsEpsilon)
                        {
                            var list = outgoing.Label?.ToArray();
                            if (list != null && list.Length == 1 && !IgnoredTokens.Contains(list[0]))
                            {
                                result.Add(list[0]);
                                pipeline.Push(outgoing.target);
                            }
                        }
                        else
                            pipeline.Push(outgoing.target);
                    }
                }
            }
            return result;
        }

        private FollowSetsHolder DetermineFollowSets(ATNState start, ATNState stop)
        {
            var sets = new List<FollowSetWithPath>();
            var stateStack = new Stack<ATNState>();
            var ruleStack = new Stack<int>();
            bool isExhaustive = CollectFollowSets(start, stop, sets, stateStack, ruleStack);

            var combined = new IntervalSet();
            foreach (var set in sets)
                combined.AddAll(set.Intervals);

            return new FollowSetsHolder { Sets = sets, Combined = combined, IsExhaustive = isExhaustive };
        }

        private bool CollectFollowSets(ATNState s, ATNState stopState,
        List<FollowSetWithPath> followSets, Stack<ATNState> stateStack, Stack<int> ruleStack)
        {
            if (stateStack.Contains(s))
                return true;

            stateStack.Push(s);

            if (s == stopState || s is RuleStopState)
            {
                stateStack.Pop();
                return false;
            }

            bool isExhaustive = true;
            for (int ti = 0; ti < s.NumberOfTransitions; ++ti)
            {
                var transition = s.Transition(ti);
                switch (transition.TransitionType)
                {
                    case TransitionType.RULE:
                        var ruleTransition = (RuleTransition)transition;
                        if (ruleStack.Contains(ruleTransition.target.ruleIndex))
                            continue;

                        ruleStack.Push(ruleTransition.target.ruleIndex);
                        bool subExhaustive = CollectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
                        ruleStack.Pop();

                        if (!subExhaustive)
                        {
                            bool nextEx = CollectFollowSets(ruleTransition.followState, stopState, followSets, stateStack, ruleStack);
                            isExhaustive &= nextEx;
                        }
                        break;

                    case TransitionType.PREDICATE:
                        if (CheckPredicate((PredicateTransition)transition))
                        {
                            bool nextEx = CollectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
                            isExhaustive &= nextEx;
                        }
                        break;

                    default:
                        if (transition.IsEpsilon)
                        {
                            bool nextEx = CollectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
                            isExhaustive &= nextEx;
                        }
                        else
                        {
                            var label = transition.Label;
                            if (label != null && !label.IsNil)
                            {
                                var set = new FollowSetWithPath
                                {
                                    Intervals = label,
                                    Path = ruleStack.Reverse().ToList(),
                                    Following = GetFollowingTokens(transition)
                                };
                                followSets.Add(set);
                            }
                        }
                        break;
                }
            }

            stateStack.Pop();
            return isExhaustive;
        }

        private RuleEndStatus ProcessRule(RuleStartState startState, int tokenListIndex,
        List<RuleWithStartToken> callStack, int precedence, int indentation)
        {
            if (!shortcutMap.TryGetValue(startState.ruleIndex, out var posMap))
            {
                posMap = new();
                shortcutMap[startState.ruleIndex] = posMap;
            }
            else if (posMap.ContainsKey(tokenListIndex))
                return posMap[tokenListIndex];

            var result = new RuleEndStatus();

            if (!followSetsByATN.TryGetValue(parser.GetType().Name, out var setsPerState))
            {
                setsPerState = new();
                followSetsByATN[parser.GetType().Name] = setsPerState;
            }

            if (!setsPerState.TryGetValue(startState.stateNumber, out var followSets))
            {
                var stop = atn.ruleToStopState[startState.ruleIndex];
                followSets = DetermineFollowSets(startState, stop);
                setsPerState[startState.stateNumber] = followSets;
            }

            var startTokenIndex = tokens[tokenListIndex].TokenIndex;
            callStack.Add(new RuleWithStartToken { StartTokenIndex = startTokenIndex, RuleIndex = startState.ruleIndex });

            if (tokenListIndex >= tokens.Count - 1)
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
                        fullPath.AddRange(set.Path.Select(p => new RuleWithStartToken
                        {
                            StartTokenIndex = startTokenIndex,
                            RuleIndex = p
                        }));

                        if (!TranslateStackToRuleIndex(fullPath))
                        {
                            foreach (var symbol in set.Intervals.ToArray())
                            {
                                if (!IgnoredTokens.Contains(symbol))
                                    candidates.Tokens[symbol] = set.Following;
                            }
                        }
                    }
                }

                if (!followSets.IsExhaustive)
                    result.Add(tokenListIndex);

                callStack.RemoveAt(callStack.Count - 1);
                return result;
            }

            if (followSets.IsExhaustive && !followSets.Combined.Contains(tokens[tokenListIndex].Type))
            {
                callStack.RemoveAt(callStack.Count - 1);
                return result;
            }

            if (startState.isPrecedenceRule)
                precedenceStack.Add(precedence);

            var pipeline = new Stack<PipelineEntry>();
            pipeline.Push(new PipelineEntry { State = startState, TokenListIndex = tokenListIndex });

            while (pipeline.Count > 0)
            {
                var current = pipeline.Pop();
                statesProcessed++;

                if (current.State is RuleStopState)
                {
                    result.Add(current.TokenListIndex);
                    continue;
                }

                if (tokens.Count == current.TokenListIndex)
                    break;

                var currentSymbol = tokens[current.TokenListIndex].Type;
                bool atCaret = current.TokenListIndex >= tokens.Count - 1;

                for (int ti = 0; ti < current.State.NumberOfTransitions; ++ti)
                {
                    var transition = current.State.Transition(ti);
                    switch (transition.TransitionType)
                    {
                        case TransitionType.RULE:
                            var ruleTransition = (RuleTransition)transition;
                            var endStatus = ProcessRule((RuleStartState)transition.target, current.TokenListIndex,
                            callStack, ruleTransition.precedence, indentation + 1);
                            foreach (var pos in endStatus)
                                pipeline.Push(new PipelineEntry { State = ruleTransition.followState, TokenListIndex = pos });
                            break;

                        default:
                            if (transition.IsEpsilon)
                            {
                                pipeline.Push(new PipelineEntry { State = transition.target, TokenListIndex = current.TokenListIndex });
                            }
                            else if (transition.Label?.Contains(currentSymbol) == true)
                            {
                                pipeline.Push(new PipelineEntry { State = transition.target, TokenListIndex = current.TokenListIndex + 1 });
                            }
                            break;
                    }
                }
            }

            callStack.RemoveAt(callStack.Count - 1);
            if (startState.isPrecedenceRule)
                precedenceStack.RemoveAt(precedenceStack.Count - 1);

            posMap[tokenListIndex] = result;
            return result;
        }
    }
}
