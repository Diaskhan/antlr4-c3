using Antlr4.Runtime;
using AntlrC3;

namespace CodeCompletionTests
{
    public class TestErrorListener : BaseErrorListener
    {
        public int ErrorCount { get; private set; } = 0;

        public override void SyntaxError(
            TextWriter output,
            IRecognizer recognizer,
            IToken offendingSymbol,
            int line,
            int charPositionInLine,
            string msg,
            RecognitionException e)
        {
            ErrorCount++;
        }
    }

    [TestFixture]
    public class WhiteboxGrammarTests
    {
        [Test]
        public void CaretAtTransitionWithOptionalTokens()
        {
            var inputStream = CharStreams.fromString("LOREM ");
            var lexer = new WhiteboxLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);

            var parser = new WhiteboxParser(tokenStream);
            var errorListener = new TestErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            var ctx = parser.test1();
            Assert.That(errorListener.ErrorCount, Is.EqualTo(1));

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(1);

            Assert.That(candidates.Tokens.Count, Is.EqualTo(5));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.IPSUM));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.DOLOR));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.SIT));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.AMET));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.CONSECTETUR));
        }

        [Test]
        public void CaretAtTransitionWithEmptyFollowSet()
        {
            var inputStream = CharStreams.fromString("LOREM ");
            var lexer = new WhiteboxLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);

            var parser = new WhiteboxParser(tokenStream);
            var errorListener = new TestErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            var ctx = parser.test2();
            Assert.That(errorListener.ErrorCount, Is.EqualTo(1));

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(1);

            Assert.That(candidates.Tokens.Count, Is.EqualTo(5));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.IPSUM));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.DOLOR));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.SIT));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.AMET));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.CONSECTETUR));
        }

        [Test]
        public void CaretAtOptionalToken()
        {
            var inputStream = CharStreams.fromString("LOREM ");
            var lexer = new WhiteboxLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);

            var parser = new WhiteboxParser(tokenStream);
            var errorListener = new TestErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            var ctx = parser.test3();
            Assert.That(errorListener.ErrorCount, Is.EqualTo(1));

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(1);

            Assert.That(candidates.Tokens.Count, Is.EqualTo(4));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.IPSUM));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.DOLOR));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.SIT));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.AMET));
        }

        [TestCase("test4")]
        [TestCase("test5")]
        [TestCase("test6")]
        [TestCase("test7")]
        public void CaretAtOneOfMultiplePossibleStates(string testRule)
        {
            var inputStream = CharStreams.fromString("LOREM IPSUM ");
            var lexer = new WhiteboxLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);

            var parser = new WhiteboxParser(tokenStream);
            var errorListener = new TestErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            ParserRuleContext ctx = testRule switch
            {
                "test4" => parser.test4(),
                "test5" => parser.test5(),
                "test6" => parser.test6(),
                "test7" => parser.test7(),
                _ => throw new ArgumentException()
            };

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(2);

            Assert.That(candidates.Tokens.Count, Is.EqualTo(1));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.DOLOR));
            Assert.That(candidates.Tokens[WhiteboxLexer.DOLOR].Count, Is.EqualTo(0));
        }

        [Test]
        public void CaretAtMultipleStatesWithCommonFollowList()
        {
            var inputStream = CharStreams.fromString("LOREM IPSUM ");
            var lexer = new WhiteboxLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);

            var parser = new WhiteboxParser(tokenStream);
            var errorListener = new TestErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            var ctx = parser.test8();

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(2);

            Assert.That(candidates.Tokens.Count, Is.EqualTo(1));
            Assert.IsTrue(candidates.Tokens.ContainsKey(WhiteboxLexer.DOLOR));
            CollectionAssert.AreEqual(new List<int> { WhiteboxLexer.SIT }, candidates.Tokens[WhiteboxLexer.DOLOR]);
        }
    }

    [TestFixture]
    public class CPP14ParserTests
    {
        [Test]
        public void SimpleCPPExample()
        {
            var inputStream = CharStreams.fromString(
                "class A {\npublic:\n  void test() {\n  }\n};\n"
            );
            var lexer = new CPP14Lexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);

            var parser = new CPP14Parser(tokenStream);
            parser.RemoveErrorListeners();
            var errorListener = new TestErrorListener();
            parser.AddErrorListener(errorListener);
            parser.translationunit();
            Assert.That(errorListener.ErrorCount, Is.EqualTo(0));

            var core = new CodeCompletionCore(parser);

            //core.IgnoredTokens = new HashSet<int> {
            //    CPP14Lexer.Identifier,
            //    CPP14Lexer.LeftParen, CPP14Lexer.RightParen,
            //    CPP14Lexer.Operator, CPP14Lexer.Star, CPP14Lexer.And, CPP14Lexer.AndAnd,
            //    CPP14Lexer.LeftBracket,
            //    CPP14Lexer.Ellipsis,
            //    CPP14Lexer.Doublecolon, CPP14Lexer.Semi
            //};

            //core.PreferredRules = new HashSet<int> {
            //    CPP14Parser.RULE_classname, CPP14Parser.RULE_namespacename, CPP14Parser.RULE_idexpression
            //};

            var candidates = core.CollectCandidates(0);
            Assert.IsTrue(candidates.Tokens.ContainsKey(CPP14Lexer.Class));
            //Assert.That(candidates.Rules.Count, Is.EqualTo(3));
        }

        [Test]
        public void SimpleCPPWithErrors()
        {
            var inputStream = CharStreams.fromString(
                "class A {\npublic:\n  void test() {\n    if ()  }\n};\n"
            );
            var lexer = new CPP14Lexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);

            var parser = new CPP14Parser(tokenStream);
            parser.RemoveErrorListeners();
            var errorListener = new TestErrorListener();
            parser.AddErrorListener(errorListener);
            parser.translationunit();
            Assert.That(errorListener.ErrorCount, Is.EqualTo(3));

            var core = new CodeCompletionCore(parser);
            //core.IgnoredTokens = new HashSet<int> {
            //    CPP14Lexer.Identifier,
            //    CPP14Lexer.Operator, CPP14Lexer.Star, CPP14Lexer.And, CPP14Lexer.AndAnd,
            //    CPP14Lexer.LeftBracket,
            //    CPP14Lexer.Ellipsis,
            //    CPP14Lexer.Doublecolon, CPP14Lexer.Semi
            //};
            //core.PreferredRules = new HashSet<int> {
            //    CPP14Parser.RULE_classname, CPP14Parser.RULE_namespacename, CPP14Parser.RULE_idexpression
            //};

            var candidates = core.CollectCandidates(11);
            Assert.That(candidates.Tokens.Count, Is.EqualTo(1));
            Assert.IsTrue(candidates.Tokens.ContainsKey(CPP14Lexer.LeftParen));
        }
    }

    [TestFixture]
    public class ExprParserTests
    {
        [Test]
        public void MostSimpleSetup()
        {
            var inputStream = CharStreams.fromString("var c = a + b()");
            var lexer = new ExprLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);

            var parser = new ExprParser(tokenStream);
            var errorListener = new TestErrorListener();
            parser.AddErrorListener(errorListener);
            parser.expression();
            Assert.That(errorListener.ErrorCount, Is.EqualTo(0));

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(0);

            Assert.That(candidates.Tokens.Count, Is.EqualTo(3));
            Assert.IsTrue(candidates.Tokens.ContainsKey(ExprLexer.VAR));
        }
    }
}