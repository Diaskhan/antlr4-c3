using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4C3.Tests.Grammar;
using Xunit;

namespace Antlr4C3.Tests
{
    public class WhiteboxTest
    {
        private static WhiteboxParser CreateParser(string input, out CountingErrorListener errorListener)
        {
            var lexer = new WhiteboxLexer(new AntlrInputStream(input));
            var tokens = new CommonTokenStream(lexer);
            var parser = new WhiteboxParser(tokens);

            parser.RemoveErrorListeners();
            errorListener = new CountingErrorListener();
            parser.AddErrorListener(errorListener);
            return parser;
        }

        [Fact]
        public void CaretAtTransitionToRuleWithNonExhaustiveFollowSet()
        {
            var parser = CreateParser("LOREM ", out var errorListener);
            var ctx = parser.test1();
            Assert.Equal(1, errorListener.ErrorCount);

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(1, ctx); // caret on EOF

            Assert.Equal(5, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.IPSUM));
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.DOLOR));
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.SIT));
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.AMET));
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.CONSECTETUR));
        }

        [Fact]
        public void CaretAtTransitionToRuleWithEmptyFollowSet()
        {
            var parser = CreateParser("LOREM ", out var errorListener);
            var ctx = parser.test2();
            Assert.Equal(1, errorListener.ErrorCount);

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(1, ctx); // caret on EOF

            Assert.Equal(5, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.IPSUM));
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.DOLOR));
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.SIT));
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.AMET));
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.CONSECTETUR));
        }

        [Fact]
        public void CaretAtOptionalToken()
        {
            var parser = CreateParser("LOREM ", out var errorListener);
            var ctx = parser.test3();
            Assert.Equal(1, errorListener.ErrorCount);

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(1, ctx); // caret on EOF

            Assert.Equal(4, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.IPSUM));
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.DOLOR));
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.SIT));
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.AMET));
        }

        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public void CaretAtOneOfMultiplePossibleStates(int index)
        {
            var parser = CreateParser("LOREM IPSUM ", out _);
            ParserRuleContext ctx = index switch
            {
                4 => parser.test4(),
                5 => parser.test5(),
                6 => parser.test6(),
                7 => parser.test7(),
                _ => throw new System.InvalidOperationException(),
            };

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(2, ctx); // caret on EOF

            Assert.Equal(1, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.DOLOR));
            Assert.Equal(new List<int>(), candidates.Tokens[WhiteboxLexer.DOLOR]);
        }

        [Fact]
        public void CaretAtOneOfMultiplePossibleStatesWithCommonFollowList()
        {
            var parser = CreateParser("LOREM IPSUM ", out _);
            var ctx = parser.test8();

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(2, ctx); // caret on EOF

            Assert.Equal(1, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(WhiteboxLexer.DOLOR));
            Assert.Equal(new List<int> { WhiteboxLexer.SIT }, candidates.Tokens[WhiteboxLexer.DOLOR]);
        }
    }
}
