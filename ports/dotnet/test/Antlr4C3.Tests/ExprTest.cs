using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4C3.Grammars;
using Xunit;

namespace Antlr4C3.Tests
{
    [TestCaseOrderer("Antlr4C3.Tests.TestPriorityOrderer", "Antlr4C3.Tests")]
    public class ExprTest
    {
        private static ExprParser CreateParser(string expression, out CountingErrorListener errorListener)
        {
            var lexer = new ExprLexer(new AntlrInputStream(expression));
            var tokens = new CommonTokenStream(lexer);
            var parser = new ExprParser(tokens);

            lexer.RemoveErrorListeners();
            parser.RemoveErrorListeners();
            errorListener = new CountingErrorListener();
            parser.AddErrorListener(errorListener);
            return parser;
        }

        [Fact]
        [TestOrder(1)]
        public void MostSimpleSetup()
        {
            var parser = CreateParser("var c = a + b()", out var errorListener);

            // Specify our entry point
            parser.expression();

            Assert.Equal(0, errorListener.ErrorCount);

            var core = new CodeCompletionCore(parser);

            // 1) At the input start.
            var candidates = core.CollectCandidates(0, null);

            Assert.Equal(3, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.VAR));
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.LET));
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.ID));

            Assert.Equal(new List<int> { ExprLexer.ID, ExprLexer.EQUAL }, candidates.Tokens[ExprLexer.VAR]);
            Assert.Equal(new List<int> { ExprLexer.ID, ExprLexer.EQUAL }, candidates.Tokens[ExprLexer.LET]);
            Assert.Equal(new List<int>(), candidates.Tokens[ExprLexer.ID]);

            // 2) On the first whitespace.
            candidates = core.CollectCandidates(1, null);
            Assert.Equal(1, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.ID));

            // 3) On the variable name ('c').
            candidates = core.CollectCandidates(2, null);
            Assert.Equal(1, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.ID));

            // 4) On the equal sign.
            candidates = core.CollectCandidates(4, null);
            Assert.Equal(1, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.EQUAL));

            // 5) On the variable reference 'a'.
            candidates = core.CollectCandidates(6, null);
            Assert.Equal(1, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.ID));

            // 6) On the '+' operator.
            candidates = core.CollectCandidates(8, null);
            Assert.Equal(5, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.PLUS));
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.MINUS));
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.MULTIPLY));
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.DIVIDE));
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.OPEN_PAR));
        }

        [Fact]
        [TestOrder(2)]
        public void TypicalExpressionTest()
        {
            var parser = CreateParser("var c = a + b", out var errorListener);
            parser.Interpreter.PredictionMode = PredictionMode.LL_EXACT_AMBIG_DETECTION;

            // Specify our entry point
            parser.expression();

            Assert.Equal(0, errorListener.ErrorCount);

            var preferredRules = new HashSet<int> { ExprParser.RULE_functionRef, ExprParser.RULE_variableRef };
            var ignoredTokens = new HashSet<int>
            {
                ExprLexer.ID, ExprLexer.PLUS, ExprLexer.MINUS,
                ExprLexer.MULTIPLY, ExprLexer.DIVIDE, ExprLexer.EQUAL,
            };

            var core = new CodeCompletionCore(parser)
            {
                preferredRules = preferredRules,
                ignoredTokens = ignoredTokens,
            };

            // 1) At the input start.
            var candidates = core.CollectCandidates(0, null);

            Assert.Equal(2, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.VAR));
            Assert.True(candidates.Tokens.ContainsKey(ExprLexer.LET));

            Assert.Equal(new List<int> { ExprLexer.ID, ExprLexer.EQUAL }, candidates.Tokens[ExprLexer.VAR]);
            Assert.Equal(new List<int> { ExprLexer.ID, ExprLexer.EQUAL }, candidates.Tokens[ExprLexer.LET]);

            // 2) On the variable name ('c').
            candidates = core.CollectCandidates(2, null);
            Assert.Empty(candidates.Tokens);

            // 4) On the equal sign.
            candidates = core.CollectCandidates(4, null);
            Assert.Empty(candidates.Tokens);

            // 5) On the variable reference 'a'.
            candidates = core.CollectCandidates(6, null);
            Assert.Empty(candidates.Tokens);
            Assert.Equal(2, candidates.Rules.Count);

            var found = 0;
            foreach (var candidate in candidates.Rules)
            {
                if (candidate.Key == ExprParser.RULE_functionRef || candidate.Key == ExprParser.RULE_variableRef)
                {
                    found++;
                }
                else
                {
                    Assert.True(false);
                }
            }
            Assert.Equal(2, found);

            // 6) On the whitespace after the 'a'.
            candidates = core.CollectCandidates(7, null);
            Assert.Empty(candidates.Tokens);
            Assert.Equal(1, candidates.Rules.Count);

            found = 0;
            foreach (var candidate in candidates.Rules)
            {
                if (candidate.Key == ExprParser.RULE_functionRef || candidate.Key == ExprParser.RULE_variableRef)
                {
                    found++;
                }
                else
                {
                    Assert.True(false);
                }
            }
            Assert.Equal(1, found);
        }
    }
}
