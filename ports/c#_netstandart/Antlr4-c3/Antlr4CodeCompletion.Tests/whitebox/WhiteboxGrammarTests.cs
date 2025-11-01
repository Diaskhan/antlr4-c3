using Antlr4.Runtime;
using AntlrC3;
using static WhiteboxLexer; // Чтобы можно было писать VAR, LET и т.п. напрямую

namespace C3.Tests
{
    public class WhiteboxGrammar
    {
        public class Lexer : WhiteboxLexer { public Lexer(ICharStream input) : base(input) { } }
        public class Parser : WhiteboxParser { public Parser(ITokenStream input) : base(input) { } }
    }

    [TestFixture]
    public class WhiteboxGrammarTests
    {
        [Test]
        public void CaretAtTransitionToRuleWithNonExhaustiveFollowSet()
        {
            var pipeline = new AntlrPipeline<WhiteboxLexer, WhiteboxParser>("LOREM ");
            var ctx = pipeline.Parser.test1();
            Assert.AreEqual(1, pipeline.Listener.ErrorCount);

            var completion = new CodeCompletionCore(pipeline.Parser);
            var candidates = completion.CollectCandidates(1, new Parameters { Context = ctx });

            CollectionAssert.AreEquivalent(
                new[] {
            WhiteboxLexer.IPSUM,
            WhiteboxLexer.DOLOR,
            WhiteboxLexer.SIT,
            WhiteboxLexer.AMET,
            WhiteboxLexer.CONSECTETUR
                },
                candidates.Tokens.Keys
            );
        }


        [Test]
        public void CaretAtTransitionToRuleWithEmptyFollowSet()
        {
            var pipeline = new AntlrPipeline<WhiteboxLexer, WhiteboxParser>("LOREM ");
            var ctx = pipeline.Parser.test2();
            Assert.AreEqual(1, pipeline.Listener.ErrorCount);

            var completion = new CodeCompletionCore(pipeline.Parser);
            var candidates = completion.CollectCandidates(1, new Parameters { Context = ctx });

            CollectionAssert.AreEquivalent(
                new[] { IPSUM, DOLOR, SIT, AMET, CONSECTETUR },
                candidates.Tokens.Keys
            );
        }

        [Test]
        public void CaretAtOneOfMultiplePossibleStates()
        {
            foreach (var index in new[] { 4, 5, 6, 7 })
            {
                var pipeline = new AntlrPipeline<WhiteboxLexer, WhiteboxParser>("LOREM IPSUM ");

                ParserRuleContext ctx = index switch
                {
                    4 => pipeline.Parser.test4(),
                    5 => pipeline.Parser.test5(),
                    6 => pipeline.Parser.test6(),
                    7 => pipeline.Parser.test7(),
                    _ => throw new System.ArgumentOutOfRangeException()
                };

                var completion = new CodeCompletionCore(pipeline.Parser);
                var candidates = completion.CollectCandidates(2, new Parameters { Context = ctx });

                CollectionAssert.AreEquivalent(
                    new[] { DOLOR },
                    candidates.Tokens.Keys
                );
                CollectionAssert.IsEmpty(candidates.Tokens[DOLOR]);
            }
        }

        [Test]
        public void CaretAtOneOfMultiplePossibleStatesWithCommonFollowList()
        {
            var pipeline = new AntlrPipeline<WhiteboxLexer, WhiteboxParser>("LOREM IPSUM ");
            var ctx = pipeline.Parser.test8();

            var completion = new CodeCompletionCore(pipeline.Parser);
            var candidates = completion.CollectCandidates(2, new Parameters { Context = ctx });

            CollectionAssert.AreEquivalent(
                new[] { DOLOR },
                candidates.Tokens.Keys
            );

            CollectionAssert.AreEqual(
                new[] { SIT },
                candidates.Tokens[DOLOR]
            );
        }
    }
}
