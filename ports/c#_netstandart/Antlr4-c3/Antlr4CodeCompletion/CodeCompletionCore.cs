// // CodeCompletionCore.cs
// // Auto-translated from CodeCompletionCore.java
// // Namespace: AntlrC3
// // Dependencies: Antlr4.Runtime
// // Compatible with .NET 8 and .NET Framework 4.8

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using Antlr4.Runtime;
// using Antlr4.Runtime.Atn;
// using Antlr4.Runtime.Tree;

// namespace AntlrC3
// {
//     /**
//      * This class computes the set of tokens which can follow a given parser
//      * context (position) in the input stream.
//      *
//      * Ported from Java version of Mike Lischke's ANTLR-c3.
//      */
//     public class CodeCompletionCore
//     {
//         public static readonly int RULE_REF = -1;
//         public static readonly int TOKEN_REF = -2;

//         private readonly Parser _parser;
//         private readonly ATN _atn;

//         private readonly Dictionary<int, HashSet<int>> _preferredRules = new();
//         private readonly Dictionary<int, HashSet<int>> _ignoredTokens = new();
//         private readonly Dictionary<int, HashSet<int>> _ignoredRules = new();
//         private readonly Dictionary<int, HashSet<int>> _followSets = new();

//         private readonly Dictionary<int, string> _ruleNames = new();
//         private readonly Dictionary<int, string> _tokenNames = new();
//         private readonly Dictionary<int, IntervalSet> _tokensPerDecision = new();
//         private readonly HashSet<int> _callStack = new();

//         private readonly List<CandidateRule> _candidatesRules = new();
//         private readonly List<CandidateToken> _candidateTokens = new();

//         public class CandidateRule
//         {
//             public int RuleIndex { get; set; }
//             public List<ContextDescriptor> RuleList { get; set; } = new();

//             public override string ToString()
//             {
//                 return $"Rule({RuleIndex}) -> [{string.Join(", ", RuleList)}]";
//             }
//         }

//         public class CandidateToken
//         {
//             public int TokenId { get; set; }
//             public string TokenName { get; set; }
//             public List<ContextDescriptor> RuleList { get; set; } = new();

//             public override string ToString()
//             {
//                 return $"Token({TokenId}:{TokenName}) -> [{string.Join(", ", RuleList)}]";
//             }
//         }

//         public class ContextDescriptor
//         {
//             public int RuleIndex { get; set; }
//             public string RuleName { get; set; }

//             public override string ToString() => $"{RuleName}({RuleIndex})";
//         }

//         public CodeCompletionCore(Parser parser)
//         {
//             _parser = parser;
//             _atn = parser.Atn;
//         }

//         public void AddPreferredRule(int ruleIndex, int tokenType)
//         {
//             if (!_preferredRules.ContainsKey(ruleIndex))
//                 _preferredRules[ruleIndex] = new HashSet<int>();

//             _preferredRules[ruleIndex].Add(tokenType);
//         }

//         public void AddIgnoredToken(int ruleIndex, int tokenType)
//         {
//             if (!_ignoredTokens.ContainsKey(ruleIndex))
//                 _ignoredTokens[ruleIndex] = new HashSet<int>();
//             _ignoredTokens[ruleIndex].Add(tokenType);
//         }

//         public void AddIgnoredRule(int ruleIndex, int ignoreRule)
//         {
//             if (!_ignoredRules.ContainsKey(ruleIndex))
//                 _ignoredRules[ruleIndex] = new HashSet<int>();
//             _ignoredRules[ruleIndex].Add(ignoreRule);
//         }

//         public List<CandidateToken> CandidateTokens => _candidateTokens;
//         public List<CandidateRule> CandidateRules => _candidatesRules;

//         public void CollectCandidates(ParserRuleContext context, int caretTokenIndex)
//         {
//             _candidateTokens.Clear();
//             _candidatesRules.Clear();

//             var startState = _atn.states[context.s];
//             CollectFollowSets(startState, caretTokenIndex);
//         }

//         private void CollectFollowSets(ATNState state, int caretTokenIndex)
//         {
//             if (_callStack.Contains(state.stateNumber))
//                 return;

//             _callStack.Add(state.stateNumber);

//             foreach (var transition in state.GetTransitions())
//             {
//                 switch (transition.TransitionType)
//                 {
//                     case TransitionType.Rule:
//                         {
//                             var ruleTransition = (RuleTransition)transition;
//                             if (_ignoredRules.TryGetValue(state.stateNumber, out var ignored) && ignored.Contains(ruleTransition.ruleIndex))
//                                 continue;

//                             CollectFollowSets(ruleTransition.target, caretTokenIndex);
//                             break;
//                         }
//                     case TransitionType.Predicate:
//                         {
//                             var predicateTransition = (PredicateTransition)transition;
//                             if (predicateTransition.IsEpsilon)
//                                 CollectFollowSets(predicateTransition.target, caretTokenIndex);
//                             break;
//                         }
//                     case TransitionType.Atom:
//                         {
//                             var label = transition.Label;
//                             if (label != null)
//                             {
//                                 foreach (int tokenType in label.ToList())
//                                 {
//                                     AddTokenCandidate(tokenType);
//                                 }
//                             }
//                             CollectFollowSets(transition.target, caretTokenIndex);
//                             break;
//                         }
//                     default:
//                         CollectFollowSets(transition.target, caretTokenIndex);
//                         break;
//                 }
//             }

//             _callStack.Remove(state.stateNumber);
//         }

//         private void AddTokenCandidate(int tokenType)
//         {
//             var tokenName = _parser.Vocabulary.GetDisplayName(tokenType);
//             _candidateTokens.Add(new CandidateToken
//             {
//                 TokenId = tokenType,
//                 TokenName = tokenName
//             });
//         }
