using Antlr4.Runtime;
using AntlrC3;

namespace C3.Tests
{
    public static class ExprGrammar
    {
        public static ExprLexer CreateLexer(string source) =>
            new ExprLexer(new AntlrInputStream(source));

        public static ExprParser CreateParser(string source)
        {
            var lexer = CreateLexer(source);
            var tokens = new CommonTokenStream(lexer);
            return new ExprParser(tokens);
        }
    }

    [TestFixture]
    public class SimpleExpressionParserTests
    {
        [Test]
        public void MostSimpleSetup()
        {
            var input = new AntlrInputStream("var c = a + b()");
            var lexer = new ExprLexer(input);
            var tokenStream = new CommonTokenStream(lexer);
            tokenStream.Fill(); // <-- аналог pipeline.tokens.fill()

            var parser = new ExprParser(tokenStream);
            parser.expression();

            var completion = new CodeCompletionCore(parser);

            {
                // 1) В начале ввода
                var candidates = completion.CollectCandidates(0);
                CollectionAssert.AreEquivalent(
                    new[] { ExprLexer.VAR, ExprLexer.LET, ExprLexer.ID },
                    candidates.Tokens.Keys
                );

                CollectionAssert.AreEqual(
                    new[] { ExprLexer.ID, ExprLexer.EQUAL },
                    candidates.Tokens[ExprLexer.VAR]
                );
                CollectionAssert.AreEqual(
                    new[] { ExprLexer.ID, ExprLexer.EQUAL },
                    candidates.Tokens[ExprLexer.LET]
                );
                Assert.IsEmpty(candidates.Tokens[ExprLexer.ID]);
            }

            {
                // 2) После первого пробела
                var candidates = completion.CollectCandidates(1);
                CollectionAssert.AreEquivalent(new[] { ExprLexer.ID }, candidates.Tokens.Keys);
            }

            {
                // 3) На имени переменной
                var candidates = completion.CollectCandidates(2);
                CollectionAssert.AreEquivalent(new[] { ExprLexer.ID }, candidates.Tokens.Keys);
            }

            {
                // 4) На знаке '='
                var candidates = completion.CollectCandidates(4);
                CollectionAssert.AreEquivalent(new[] { ExprLexer.EQUAL }, candidates.Tokens.Keys);
            }

            {
                // 5) На 'a'
                var candidates = completion.CollectCandidates(6);
                CollectionAssert.AreEquivalent(new[] { ExprLexer.ID }, candidates.Tokens.Keys);
            }

            {
                // 6) На '+'
                var candidates = completion.CollectCandidates(8);
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        ExprLexer.PLUS,
                        ExprLexer.MINUS,
                        ExprLexer.MULTIPLY,
                        ExprLexer.DIVIDE,
                        ExprLexer.OPEN_PAR
                    },
                    candidates.Tokens.Keys
                );
            }
        }

        [Test]
        public void TypicalSetup()
        {
            var parser = ExprGrammar.CreateParser("var c = a + b()");
            parser.expression();

            var completion = new CodeCompletionCore(parser);
            completion.IgnoredTokens.UnionWith(new[]
            {
                ExprLexer.ID,
                ExprLexer.PLUS,
                ExprLexer.MINUS,
                ExprLexer.MULTIPLY,
                ExprLexer.DIVIDE,
                ExprLexer.EQUAL
            });
            completion.PreferredRules.UnionWith(new[]
            {
                ExprParser.RULE_functionRef,
                ExprParser.RULE_variableRef
            });

            {
                var candidates = completion.CollectCandidates(0);
                CollectionAssert.AreEquivalent(
                    new[] { ExprLexer.VAR, ExprLexer.LET },
                    candidates.Tokens.Keys
                );
                Assert.IsEmpty(candidates.Tokens[ExprLexer.VAR]);
                Assert.IsEmpty(candidates.Tokens[ExprLexer.LET]);
            }

            {
                var candidates = completion.CollectCandidates(2);
                Assert.IsEmpty(candidates.Tokens);
            }

            {
                var candidates = completion.CollectCandidates(4);
                Assert.IsEmpty(candidates.Tokens);
            }

            {
                var candidates = completion.CollectCandidates(6);
                Assert.IsEmpty(candidates.Tokens);

                CollectionAssert.AreEquivalent(
                    new[] { ExprParser.RULE_functionRef, ExprParser.RULE_variableRef },
                    candidates.Rules.Keys
                );

                Assert.AreEqual(6, candidates.Rules[ExprParser.RULE_functionRef].StartTokenIndex);
                Assert.AreEqual(6, candidates.Rules[ExprParser.RULE_variableRef].StartTokenIndex);
            }

            {
                var candidates = completion.CollectCandidates(7);
                Assert.IsEmpty(candidates.Tokens);

                CollectionAssert.AreEquivalent(
                    new[] { ExprParser.RULE_functionRef },
                    candidates.Rules.Keys
                );
                Assert.AreEqual(6, candidates.Rules[ExprParser.RULE_functionRef].StartTokenIndex);
            }
        }

        [Test]
        public void RecursivePreferredRule()
        {
            var parser = ExprGrammar.CreateParser("var c = a + b");
            parser.expression();

            var completion = new CodeCompletionCore(parser);
            completion.PreferredRules.UnionWith(new[]
            {
                ExprParser.RULE_simpleExpression
            });

            {
                var candidates = completion.CollectCandidates(6);
                CollectionAssert.AreEquivalent(
                    new[] { ExprParser.RULE_simpleExpression },
                    candidates.Rules.Keys
                );
                Assert.AreEqual(6, candidates.Rules[ExprParser.RULE_simpleExpression].StartTokenIndex);
            }

            {
                completion.TranslateRulesTopDown = false;
                var candidates = completion.CollectCandidates(10);
                CollectionAssert.AreEquivalent(
                    new[] { ExprParser.RULE_simpleExpression },
                    candidates.Rules.Keys
                );
                Assert.AreEqual(6, candidates.Rules[ExprParser.RULE_simpleExpression].StartTokenIndex);
            }

            {
                completion.TranslateRulesTopDown = true;
                var candidates = completion.CollectCandidates(10);
                CollectionAssert.AreEquivalent(
                    new[] { ExprParser.RULE_simpleExpression },
                    candidates.Rules.Keys
                );
                Assert.AreEqual(10, candidates.Rules[ExprParser.RULE_simpleExpression].StartTokenIndex);
            }
        }

        [Test]
        public void CandidateRulesWithDifferentStartTokens()
        {
            var parser = ExprGrammar.CreateParser("var c = a + b");
            parser.expression();

            var completion = new CodeCompletionCore(parser);
            completion.PreferredRules.UnionWith(new[]
            {
                ExprParser.RULE_assignment,
                ExprParser.RULE_variableRef
            });
            completion.TranslateRulesTopDown = true;

            {
                var candidates = completion.CollectCandidates(0);
                CollectionAssert.AreEquivalent(
                    new[] { ExprParser.RULE_assignment, ExprParser.RULE_variableRef },
                    candidates.Rules.Keys
                );
                Assert.AreEqual(0, candidates.Rules[ExprParser.RULE_assignment].StartTokenIndex);
                Assert.AreEqual(0, candidates.Rules[ExprParser.RULE_variableRef].StartTokenIndex);
            }

            {
                var candidates = completion.CollectCandidates(6);
                CollectionAssert.AreEquivalent(
                    new[] { ExprParser.RULE_assignment, ExprParser.RULE_variableRef },
                    candidates.Rules.Keys
                );
                Assert.AreEqual(0, candidates.Rules[ExprParser.RULE_assignment].StartTokenIndex);
                Assert.AreEqual(6, candidates.Rules[ExprParser.RULE_variableRef].StartTokenIndex);
            }
        }

        [Test]
        public void OutOfBoundsCaret()
        {
            var input = new AntlrInputStream("var c = a + b");
            var lexer = new ExprLexer(input);
            var tokenStream = new CommonTokenStream(lexer);
            tokenStream.Fill(); // <-- аналог pipeline.tokens.fill()

            var parser = new ExprParser(tokenStream);

            // Создаём движок автодополнения
            var completion = new CodeCompletionCore(parser);

            var last = completion.CollectCandidates(7);
            Assert.That(last, Is.EqualTo(completion.CollectCandidates(8)));
            Assert.That(last, Is.EqualTo(completion.CollectCandidates(16)));
            Assert.That(last, Is.EqualTo(completion.CollectCandidates(32)));
            Assert.That(last, Is.EqualTo(completion.CollectCandidates(128)));
        }

        [Test]
        public void ConcurrencySmoke()
        {
            const int concurrency = 8;
            const int rounds = 32;
            const int maxTokenIndex = 8;

            // Параллельно запускаем 8 потоков
            Parallel.For(0, concurrency, _ =>
            {
                for (int j = 0; j < rounds; j++)
                {
                    // Создаём lexer и parser
                    var input = new AntlrInputStream("var c = a + b");
                    var lexer = new ExprLexer(input);
                    var tokenStream = new CommonTokenStream(lexer);
                    tokenStream.Fill(); // <-- аналог pipeline.tokens.fill()

                    var parser = new ExprParser(tokenStream);

                    // Создаём движок автодополнения
                    var completion = new CodeCompletionCore(parser);

                    // Эмулируем позицию курсора
                    for (int k = 0; k <= maxTokenIndex; k++)
                    {
                        completion.CollectCandidates(k);
                    }
                }
            });
        }

    }
}
