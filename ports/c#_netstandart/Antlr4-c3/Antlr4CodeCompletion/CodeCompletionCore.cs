using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using System.Diagnostics;

namespace AntlrC3
{
    using RuleList = List<int>;
    using TokenList = List<int>;

    public struct CandidateRule
    {
        public int StartTokenIndex;
        public RuleList RuleList;

        public override bool Equals(object? obj)
        {
            if (obj is not CandidateRule other) return false;
            return StartTokenIndex == other.StartTokenIndex &&
                   RuleList.SequenceEqual(other.RuleList);
        }

        public override int GetHashCode() => HashCode.Combine(StartTokenIndex, RuleList);
    }

    public class CandidatesCollection
    {
        public SortedDictionary<int, TokenList> Tokens { get; } = new();
        public SortedDictionary<int, CandidateRule> Rules { get; } = new();
        public bool IsCancelled { get; set; } = false;
    }

    public class Parameters
    {
        public ParserRuleContext? Context { get; set; } = null;
        public TimeSpan? Timeout { get; set; } = null;
        public AtomicBoolean? IsCancelled { get; set; } = null;
    }

    public class DebugOptions
    {
        public bool ShowResult { get; set; } = false;
        public bool ShowDebugOutput { get; set; } = false;
        public bool ShowTransitions { get; set; } = false;
        public bool ShowRuleStack { get; set; } = false;
    }

    public class CodeCompletionCore
    {
        // internal helper classes
        private class PipelineEntry
        {
            public ATNState State = null!;
            public int TokenListIndex;
        }

        private class RuleWithStartToken
        {
            public int StartTokenIndex;
            public int RuleIndex;
        }

        private class FollowSetWithPath
        {
            public IntervalSet Intervals = new IntervalSet();
            public RuleList Path = new RuleList();
            public TokenList Following = new TokenList();
        }

        private class FollowSetsHolder
        {
            public List<FollowSetWithPath> Sets = new();
            public IntervalSet Combined = new IntervalSet();
            public bool IsExhaustive;
        }

        private class RuleEndStatus : HashSet<int> { }

        // thread-local cache of follow sets per parser type
        private static readonly ThreadLocal<Dictionary<Type, Dictionary<int, FollowSetsHolder>>> followSetsByATN
            = new(() => new Dictionary<Type, Dictionary<int, FollowSetsHolder>>());

        private static readonly List<string> atnStateTypeMap = new()
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

        // instance fields
        private readonly Parser parser;
        private readonly ATN atn;
        private readonly IVocabulary vocabulary;
        private readonly IList<string> ruleNames;

        private List<IToken> tokens = new();
        private List<int> precedenceStack = new();

        private int tokenStartIndex = 0;
        private int statesProcessed = 0;

        // shortcutMap: ruleIndex -> (tokenListIndex -> RuleEndStatus)
        private readonly Dictionary<int, Dictionary<int, RuleEndStatus>> shortcutMap = new();

        private readonly CandidatesCollection candidates = new();

        private TimeSpan? timeout;
        private AtomicBoolean? cancel;
        private Stopwatch timeoutStart = new();

        // public options
        public HashSet<int> IgnoredTokens { get; } = new();
        public HashSet<int> PreferredRules { get; } = new();
        public bool TranslateRulesTopDown { get; set; } = false;
        public DebugOptions DebugOptions { get; } = new();

        public CodeCompletionCore(Parser parser)
        {
            this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
            this.atn = parser.Atn;
            this.vocabulary = parser.Vocabulary;
            this.ruleNames = parser.RuleNames;
        }

        // ---------------------------
        // Public main entry
        // ---------------------------
        public CandidatesCollection CollectCandidates(int caretTokenIndex, Parameters? parameters = null)
        {
            parameters ??= new Parameters();


            timeout = parameters.Timeout;
            cancel = parameters.IsCancelled;
            timeoutStart = Stopwatch.StartNew();

            shortcutMap.Clear();
            candidates.Tokens.Clear();
            candidates.Rules.Clear();
            candidates.IsCancelled = false;
            statesProcessed = 0;
            precedenceStack.Clear();

            tokenStartIndex = (parameters.Context != null && parameters.Context.Start != null)
                ? parameters.Context.Start.TokenIndex
                : 0;


            var tokenStream = parser.TokenStream;

            tokens = new List<IToken>();
            int offset = tokenStartIndex;
            while (true)
            {
                IToken token = tokenStream.Get(offset++);
                if (token.Channel == TokenConstants.DefaultChannel)
                {
                    tokens.Add(token);
                    if (token.TokenIndex >= caretTokenIndex)
                    {
                        break;
                    }
                }

                // Do not check token index here; we want to end with first unhidden token on or after caret
                if (token.Type == TokenConstants.EOF)
                {
                    break;
                }
            }

            var callStack = new List<RuleWithStartToken>();
            var startRule = (parameters.Context != null) ? parameters.Context.RuleIndex : 0;

            // processRule expects RuleStartState
            var startState = atn.ruleToStartState[startRule];
            bool timedOut = false;
            ProcessRule(startState, 0, callStack, 0, 0, ref timedOut);

            // remove ignored tokens from candidates
            var keys = candidates.Tokens.Keys.ToList();
            foreach (var symbol in keys)
            {
                var following = candidates.Tokens[symbol];
                var filtered = following.Where(t => !IgnoredTokens.Contains(t)).ToList();
                candidates.Tokens[symbol] = filtered;
            }

            PrintOverallResults();

            return candidates;
        }

        // ---------------------------
        // Helper: check predicate
        // ---------------------------
        private bool CheckPredicate(PredicateTransition transition)
        {
            if (transition == null) return false;
            // C++ version calls transition->getPredicate()->eval(parser, &ParserRuleContext::EMPTY)
            // In C# runtime predicate evaluation API may differ. Attempt to call SemanticContext evaluation if available.
            // CHECK-RUNTIME-API: adjust if your runtime exposes different predicate API.
            try
            {
                // If runtime has `Predicate` property exposing a SemanticContext-like object with Eval
                var pred = transition.Predicate; // CHECK-RUNTIME-API
                if (pred == null) return false;

                // Many runtimes implement Eval(Parser, ParserRuleContext) via SemanticContext
                // We'll attempt a dynamic invocation to be tolerant:
                dynamic dpred = pred;
                var res = dpred.Eval(parser, ParserRuleContext.EmptyContext); // CHECK-RUNTIME-API
                return (bool)res;
            }
            catch
            {
                // Fallback: if can't evaluate, assume true (conservative).
                return true;
            }
        }

        // ---------------------------
        // Translate stack -> preferred rules
        // ---------------------------
        private bool TranslateStackToRuleIndex(List<RuleWithStartToken> ruleWithStartTokenList)
        {
            if (!PreferredRules.Any()) return false;

            // create forward index sequence 0..n-1
            var forward = Enumerable.Range(0, ruleWithStartTokenList.Count).ToList();
            var backward = forward.AsEnumerable().Reverse();

            if (TranslateRulesTopDown)
            {
                foreach (var idx in backward)
                {
                    if (TranslateToRuleIndex(idx, ruleWithStartTokenList)) return true;
                }
                return false;
            }

            foreach (var idx in forward)
            {
                if (TranslateToRuleIndex(idx, ruleWithStartTokenList)) return true;
            }
            return false;
        }

        private bool TranslateToRuleIndex(int index, List<RuleWithStartToken> ruleWithStartTokenList)
        {
            var rwst = ruleWithStartTokenList[index];
            if (PreferredRules.Contains(rwst.RuleIndex))
            {
                // build path (all rules before index)
                var path = new List<int>();
                for (int i = 0; i < index; ++i) path.Add(ruleWithStartTokenList[i].RuleIndex);

                bool addNew = true;
                foreach (var kv in candidates.Rules)
                {
                    var entryRuleIndex = kv.Key;
                    var entryRule = kv.Value;
                    if (entryRuleIndex != rwst.RuleIndex || entryRule.RuleList.Count != path.Count) continue;

                    bool samePath = true;
                    for (int i = 0; i < path.Count; ++i)
                    {
                        if (path[i] != entryRule.RuleList[i])
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
                    var cr = new CandidateRule
                    {
                        StartTokenIndex = rwst.StartTokenIndex,
                        RuleList = path
                    };
                    candidates.Rules[rwst.RuleIndex] = cr;
                    if (DebugOptions.ShowDebugOutput)
                    {
                        Console.WriteLine("=====> collected:  " + ruleNames[rwst.RuleIndex]);
                    }
                }

                return true;
            }

            return false;
        }

        // ---------------------------
        // getFollowingTokens (static)
        // ---------------------------
        //private static TokenList GetFollowingTokens(Transition transition) //iteration1
        //{
        //    var result = new TokenList();
        //    var pipeline = new Stack<ATNState>();
        //    if (transition?.target != null) pipeline.Push(transition.target);

        //    while (pipeline.Count > 0)
        //    {
        //        var state = pipeline.Pop();
        //        if (state == null) continue;
        //        foreach (var outgoing in state.TransitionsArray)
        //        {
        //            // Only care about atom transitions that are not epsilon or epsilon-only
        //            if (outgoing.TransitionType == Antlr4.Runtime.Atn.TransitionType.RULE) continue;
        //            if (outgoing.TransitionType == Antlr4.Runtime.Atn.TransitionType.ATOM)
        //            {
        //                if (!outgoing.IsEpsilon)
        //                {
        //                    // label() in C++ -> Label in C#? runtime might expose Label property/method
        //                    // CHECK-RUNTIME-API: adjust if runtime uses 'Label' or 'Label()'
        //                    IntervalSet label = null;
        //                    try
        //                    {
        //                        label = outgoing.Label; // CHECK-RUNTIME-API
        //                    }
        //                    catch
        //                    {
        //                        try { label = outgoing.Label; } catch { label = new IntervalSet(); }
        //                    }

        //                    var list = label.ToList();
        //                    if (list.Count == 1)
        //                    {
        //                        result.Add(list[0]);
        //                        pipeline.Push(outgoing.target);
        //                    }
        //                }
        //                else
        //                {
        //                    pipeline.Push(outgoing.target);
        //                }
        //            }
        //            else
        //            {
        //                // For other types, if epsilon then continue
        //                if (outgoing.IsEpsilon)
        //                {
        //                    pipeline.Push(outgoing.target);
        //                }
        //            }
        //        }
        //    }

        //    return result;
        //}

        private static TokenList GetFollowingTokens(Transition transition) //iteration2
        {
            var result = new TokenList();
            var pipeline = new Stack<ATNState>();
            var visited = new HashSet<ATNState>();

            if (transition?.target != null)
                pipeline.Push(transition.target);

            while (pipeline.Count > 0)
            {
                var state = pipeline.Pop();
                if (state == null || !visited.Add(state))
                    continue; // уже посещали — пропускаем

                foreach (var outgoing in state.TransitionsArray)
                {
                    switch (outgoing.TransitionType)
                    {
                        case TransitionType.RULE:
                            // Игнорируем вложенные правила
                            continue;

                        case TransitionType.ATOM:
                            {
                                var label = outgoing.Label ?? new IntervalSet();
                                foreach (var symbol in label.ToList())
                                    result.Add(symbol);

                                // продолжаем обход после этого перехода
                                pipeline.Push(outgoing.target);
                                break;
                            }

                        default:
                            // Для других типов (включая epsilon) — просто продолжаем обход
                            if (outgoing.IsEpsilon)
                                pipeline.Push(outgoing.target);
                            break;
                    }
                }
            }

            return result;
        }


        // ---------------------------
        // determineFollowSets
        // ---------------------------
        private FollowSetsHolder DetermineFollowSets(ATNState start, ATNState stop)
        {
            var sets = new List<FollowSetWithPath>();
            var stateStack = new List<ATNState>();
            var ruleStack = new List<int>();
            bool isExhaustive = CollectFollowSets(start, stop, sets, stateStack, ruleStack);

            var combined = new IntervalSet();
            foreach (var s in sets) combined.AddAll(s.Intervals);

            return new FollowSetsHolder
            {
                Sets = sets,
                Combined = combined,
                IsExhaustive = isExhaustive
            };
        }

        // ---------------------------
        // collectFollowSets (recursive)
        // ---------------------------
        private bool CollectFollowSets(
            ATNState state,
            ATNState stopState,
            List<FollowSetWithPath> followSets,
            List<ATNState> stateStack,
            List<int> ruleStack)
        {
            if (stateStack.Contains(state)) return true;
            stateStack.Add(state);

            if (state == stopState || state.StateType == StateType.RuleStop)
            {
                stateStack.RemoveAt(stateStack.Count - 1);
                return false;
            }

            bool isExhaustive = true;
            foreach (var transitionBase in state.TransitionsArray) // state.Transitions in some runtimes // .GetTransitions()
            {
                var transition = transitionBase;
                var tt = transition.TransitionType;

                if (tt == TransitionType.RULE)
                {
                    var ruleTransition = transition as RuleTransition;
                    if (ruleStack.Contains(ruleTransition.target.ruleIndex))
                        continue;

                    ruleStack.Add(ruleTransition.target.ruleIndex);
                    var ruleFollowExhaustive = CollectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
                    ruleStack.RemoveAt(ruleStack.Count - 1);

                    if (!ruleFollowExhaustive)
                    {
                        var nextStateFollow = CollectFollowSets(ruleTransition.followState, stopState, followSets, stateStack, ruleStack);
                        isExhaustive = isExhaustive && nextStateFollow;
                    }
                }
                else if (tt == TransitionType.PREDICATE)
                {
                    var predTransition = transition as PredicateTransition;
                    if (CheckPredicate(predTransition))
                    {
                        var nextStateFollow = CollectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
                        isExhaustive = isExhaustive && nextStateFollow;
                    }
                }
                else if (transition.IsEpsilon)
                {
                    var nextStateFollow = CollectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
                    isExhaustive = isExhaustive && nextStateFollow;
                }
                else if (tt == TransitionType.WILDCARD)
                {
                    followSets.Add(new FollowSetWithPath
                    {
                        Intervals = AllUserTokens(),
                        Path = new RuleList(),
                        Following = new TokenList()
                    });
                }
                else
                {
                    // normal labelled transition
                    IntervalSet label;
                    try
                    {
                        label = transition.Label; // CHECK-RUNTIME-API
                    }
                    catch
                    {
                        // fallback
                        label = new IntervalSet();
                    }

                    if (!label.IsNil)
                    {
                        if (tt == TransitionType.NOT_SET)
                        {
                            label = label.Complement(AllUserTokens());
                        }

                        followSets.Add(new FollowSetWithPath
                        {
                            Intervals = label,
                            Path = new RuleList(ruleStack),
                            Following = GetFollowingTokens(transition)
                        });
                    }
                }
            }

            stateStack.RemoveAt(stateStack.Count - 1);
            return isExhaustive;
        }

        // ---------------------------
        // processRule (main walker)
        // ---------------------------
        private RuleEndStatus ProcessRule(
            RuleStartState startState,
            int tokenListIndex,
            List<RuleWithStartToken> callStack,
            int precedence,
            int indentation,
            ref bool timedOut)
        {
            // Cancelled externally?
            if (cancel?.Value == true)
            {
                timedOut = true;
                return new RuleEndStatus();
            }

            // timeout?
            timedOut = false;
            if (timeout.HasValue && timeoutStart.Elapsed > timeout.Value)
            {
                timedOut = true;
                return new RuleEndStatus();
            }

            // shortcut
            if (!shortcutMap.TryGetValue(startState.ruleIndex, out var positionMap))
            {
                positionMap = new Dictionary<int, RuleEndStatus>();
                shortcutMap[startState.ruleIndex] = positionMap;
            }

            if (positionMap.ContainsKey(tokenListIndex))
            {
                if (DebugOptions.ShowDebugOutput) Console.WriteLine("=====> shortcut");
                return positionMap[tokenListIndex];
            }

            var result = new RuleEndStatus();

            // compute follow sets for this start state if not cached (thread-local per parser type)
            var typeDict = followSetsByATN.Value;
            if (!typeDict.TryGetValue(parser.GetType(), out var perState))
            {
                perState = new Dictionary<int, FollowSetsHolder>();
                typeDict[parser.GetType()] = perState;
            }

            if (!perState.TryGetValue(startState.stateNumber, out var followSets))
            {
                var stop = atn.ruleToStopState[startState.ruleIndex];
                perState[startState.stateNumber] = DetermineFollowSets(startState, stop);
                followSets = perState[startState.stateNumber];
            }

            // start token index of this rule in the original token stream
            var startTokenIndex = tokens[tokenListIndex].TokenIndex;

            callStack.Add(new RuleWithStartToken { StartTokenIndex = startTokenIndex, RuleIndex = startState.ruleIndex });

            // if tokenListIndex at caret (we ended token list)
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
                        // fullPath = callStack + set.path (each derived rule starts same token)
                        var fullPath = new List<RuleWithStartToken>(callStack);
                        foreach (var r in set.Path)
                        {
                            fullPath.Add(new RuleWithStartToken { StartTokenIndex = startTokenIndex, RuleIndex = r });
                        }

                        if (!TranslateStackToRuleIndex(fullPath))
                        {
                            foreach (var symbol in set.Intervals.ToList())
                            {
                                if (!IgnoredTokens.Contains(symbol))
                                {
                                    if (DebugOptions.ShowDebugOutput)
                                        Console.WriteLine("=====> collected:  " + vocabulary.GetDisplayName(symbol));
                                    if (!candidates.Tokens.ContainsKey(symbol))
                                    {
                                        candidates.Tokens[symbol] = set.Following;
                                    }
                                    else if (!Enumerable.SequenceEqual(candidates.Tokens[symbol], set.Following))
                                    {
                                        candidates.Tokens[symbol] = new TokenList(); // ambiguous -> empty following
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

            // Check if follow sets say we can't match current symbol: then stop
            var currentSymbol = tokens[tokenListIndex].Type;
            if (followSets.IsExhaustive && !followSets.Combined.Contains(currentSymbol))
            {
                callStack.RemoveAt(callStack.Count - 1);
                return result;
            }

            if (startState.isPrecedenceRule)
            {
                precedenceStack.Add(precedence);
            }

            // state pipeline
            var statePipeline = new List<PipelineEntry>();
            statePipeline.Add(new PipelineEntry { State = startState, TokenListIndex = tokenListIndex });

            while (statePipeline.Count > 0)
            {
                if (cancel?.Value == true)
                {
                    timedOut = true;
                    return new RuleEndStatus();
                }

                var currentEntry = statePipeline[statePipeline.Count - 1];
                statePipeline.RemoveAt(statePipeline.Count - 1);
                statesProcessed++;

                var curSymbol = tokens[currentEntry.TokenListIndex].Type;
                bool atCaret = currentEntry.TokenListIndex >= tokens.Count - 1;

                if (DebugOptions.ShowDebugOutput)
                {
                    PrintDescription(indentation, currentEntry.State, GenerateBaseDescription(currentEntry.State), currentEntry.TokenListIndex);
                    if (DebugOptions.ShowRuleStack) PrintRuleState(callStack);
                }

                if (currentEntry.State.StateType == StateType.RuleStop)
                {
                    result.Add(currentEntry.TokenListIndex);
                    continue;
                }

                foreach (var transition in currentEntry.State.TransitionsArray) //.GetTransitions()
                {
                    switch (transition.TransitionType)
                    {
                        case TransitionType.RULE:
                            {
                                var ruleTransition = transition as RuleTransition;
                                var ruleStartState = ruleTransition.target as RuleStartState;
                                bool innerCancelled = false;
                                var endStatus = ProcessRule(ruleStartState, currentEntry.TokenListIndex, callStack, ruleTransition.precedence, indentation + 1, ref innerCancelled);
                                if (innerCancelled)
                                {
                                    timedOut = true;
                                    return new RuleEndStatus();
                                }

                                foreach (var pos in endStatus)
                                {
                                    statePipeline.Add(new PipelineEntry { State = ruleTransition.followState, TokenListIndex = pos });
                                }
                            }
                            break;

                        case TransitionType.PREDICATE:
                            {
                                var predTransition = transition as PredicateTransition;
                                if (CheckPredicate(predTransition))
                                {
                                    statePipeline.Add(new PipelineEntry { State = transition.target, TokenListIndex = currentEntry.TokenListIndex });
                                }
                            }
                            break;

                        case TransitionType.PRECEDENCE:
                            {
                                var pp = transition as PrecedencePredicateTransition;
                                if (precedenceStack.Count > 0 && pp.precedence >= precedenceStack[precedenceStack.Count - 1])
                                {
                                    statePipeline.Add(new PipelineEntry { State = transition.target, TokenListIndex = currentEntry.TokenListIndex });
                                }
                            }
                            break;

                        case TransitionType.WILDCARD:
                            {
                                if (atCaret)
                                {
                                    if (!TranslateStackToRuleIndex(callStack))
                                    {
                                        for (int token = TokenConstants.MinUserTokenType; token <= atn.maxTokenType; ++token)
                                        {
                                            if (!IgnoredTokens.Contains(token))
                                            {
                                                candidates.Tokens[token] = new TokenList();
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    statePipeline.Add(new PipelineEntry { State = transition.target, TokenListIndex = currentEntry.TokenListIndex + 1 });
                                }
                            }
                            break;

                        default:
                            {
                                if (transition.IsEpsilon)
                                {
                                    statePipeline.Add(new PipelineEntry { State = transition.target, TokenListIndex = currentEntry.TokenListIndex });
                                    continue;
                                }

                                IntervalSet set;
                                try
                                {
                                    set = transition.Label; // CHECK-RUNTIME-API
                                }
                                catch
                                {
                                    set = new IntervalSet();
                                }

                                if (!set.IsNil)
                                {
                                    if (transition.TransitionType == TransitionType.NOT_SET)
                                    {
                                        set = set.Complement(AllUserTokens());
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
                                                    if (DebugOptions.ShowDebugOutput)
                                                    {
                                                        Console.WriteLine("=====> collected:  " + vocabulary.GetDisplayName(symbol));
                                                    }

                                                    TokenList followingTokens = new();
                                                    if (hasTokenSequence)
                                                    {
                                                        followingTokens = GetFollowingTokens(transition);
                                                    }

                                                    if (!candidates.Tokens.ContainsKey(symbol))
                                                    {
                                                        candidates.Tokens[symbol] = followingTokens;
                                                    }
                                                    else
                                                    {
                                                        // keep longest common prefix
                                                        candidates.Tokens[symbol] = LongestCommonPrefix(followingTokens, candidates.Tokens[symbol]);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (set.Contains(curSymbol))
                                        {
                                            if (DebugOptions.ShowDebugOutput)
                                            {
                                                Console.WriteLine("=====> consumed:  " + vocabulary.GetDisplayName(curSymbol));
                                            }
                                            statePipeline.Add(new PipelineEntry { State = transition.target, TokenListIndex = currentEntry.TokenListIndex + 1 });
                                        }
                                    }
                                }
                            }
                            break;
                    } // switch transition type
                } // foreach transition
            } // while pipeline

            callStack.RemoveAt(callStack.Count - 1);
            if (startState.isPrecedenceRule && precedenceStack.Count > 0)
            {
                precedenceStack.RemoveAt(precedenceStack.Count - 1);
            }

            // cache
            positionMap[tokenListIndex] = result;
            return result;
        }

        // ---------------------------
        // allUserTokens
        // ---------------------------
        private IntervalSet AllUserTokens()
        {
            int min = TokenConstants.MinUserTokenType;
            int max = atn.maxTokenType;
            return IntervalSet.Of(min, max);
        }

        // ---------------------------
        // helper: longest common prefix of two lists
        // ---------------------------
        private static TokenList LongestCommonPrefix(TokenList lhs, TokenList rhs)
        {
            var index = 0;
            var min = Math.Min(lhs.Count, rhs.Count);
            while (index < min)
            {
                if (lhs[index] != rhs[index]) break;
                index++;
            }
            return lhs.Take(index).ToList();
        }

        // ---------------------------
        // descriptions & debug printing
        // ---------------------------
        private string GenerateBaseDescription(ATNState state)
        {
            var stateValue = state.stateNumber == ATNState.InvalidStateNumber ? "Invalid" : state.stateNumber.ToString();
            var output = "[" + stateValue + " " + atnStateTypeMap[(int)state.StateType] + "]";
            output += " in ";
            output += ruleNames[state.ruleIndex];
            return output;
        }

        private void PrintDescription(int indentation, ATNState state, string baseDescription, int tokenIndex)
        {
            var indent = new string(' ', indentation * 2);
            string transitionDescription = "";

            if (DebugOptions.ShowTransitions)
            {
                foreach (var transition in state.TransitionsArray) //state.GetTransitions()
                {
                    var labels = "";
                    IntervalSet label;
                    try { label = transition.Label; } catch { label = new IntervalSet(); }
                    var symbols = label.ToList();
                    if (symbols.Count > 2)
                    {
                        labels = vocabulary.GetDisplayName(symbols[0]) + " .. " + vocabulary.GetDisplayName(symbols[symbols.Count - 1]);
                    }
                    else
                    {
                        for (int i = 0; i < symbols.Count; i++)
                        {
                            if (!string.IsNullOrEmpty(labels)) labels += ", ";
                            labels += vocabulary.GetDisplayName(symbols[i]);
                        }
                    }

                    if (string.IsNullOrEmpty(labels)) labels = "ε";

                    transitionDescription += "\n";
                    transitionDescription += indent;
                    transitionDescription += "\t(" + labels + ") [";
                    transitionDescription += transition.target.stateNumber.ToString();
                    transitionDescription += " " + atnStateTypeMap[(int)transition.target.StateType];
                    transitionDescription += "] in " + ruleNames[transition.target.ruleIndex];
                }
            }

            string output;
            if (tokenIndex >= tokens.Count - 1)
            {
                output = "<<" + (tokenStartIndex + tokenIndex).ToString() + ">> ";
            }
            else
            {
                output = "<" + (tokenStartIndex + tokenIndex).ToString() + "> ";
            }

            Console.WriteLine(indent + output + "Current state: " + baseDescription + transitionDescription);
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
                Console.WriteLine(ruleNames[rule.RuleIndex]);
            }
        }

        private void PrintOverallResults()
        {
            if (!DebugOptions.ShowResult) return;

            if (candidates.IsCancelled) Console.WriteLine("*** TIMED OUT ***");
            Console.WriteLine("States processed: " + statesProcessed);

            Console.WriteLine("\n\nCollected rules:\n\n");
            foreach (var kv in candidates.Rules)
            {
                Console.Write(ruleNames[kv.Key] + ", path:  ");
                foreach (var r in kv.Value.RuleList) Console.Write(ruleNames[r] + " ");
                Console.WriteLine();
            }

            var sortedTokens = new SortedSet<string>();
            foreach (var kv in candidates.Tokens)
            {
                var value = vocabulary.GetDisplayName(kv.Key);
                foreach (var following in kv.Value) value += " " + vocabulary.GetDisplayName(following);
                sortedTokens.Add(value);
            }

            Console.WriteLine("\n\nCollected tokens:\n");
            foreach (var s in sortedTokens) Console.WriteLine(s);
            Console.WriteLine("\n\n");
        }
    }

    // simple atomic boolean wrapper (like std::atomic<bool>)
    public class AtomicBoolean
    {
        private int flag = 0;
        public bool Value
        {
            get => Interlocked.CompareExchange(ref flag, 0, 0) != 0;
            set => Interlocked.Exchange(ref flag, value ? 1 : 0);
        }
    }
}
