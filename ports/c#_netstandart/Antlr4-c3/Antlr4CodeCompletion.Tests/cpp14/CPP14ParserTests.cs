using Antlr4.Runtime;
using AntlrC3;

namespace C3.Tests
{
    [TestFixture]
    public class CPP14ParserTests
    {
        private static CPP14Parser CreateParser(string source)
        {
            var input = new AntlrInputStream(source);
            var lexer = new CPP14Lexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new CPP14Parser(tokens);
            return parser;
        }

        [Test]
        public void SimpleExample()
        {
            // Исходный код, как в тесте
            string source = @"
class A {
public:
  void test() {
  }
};
";

            var parser = CreateParser(source);
            parser.translationunit(); // построим дерево

            // Проверяем, что парсер не выдал ошибок
            Assert.Zero(parser.NumberOfSyntaxErrors, "Parser reported syntax errors.");

            var completion = new CodeCompletionCore(parser);

            // Игнорируем операторы и ID
            completion.IgnoredTokens.UnionWith(new[]
            {
                CPP14Lexer.Identifier,
                CPP14Lexer.LeftParen,
                CPP14Lexer.RightParen,
                CPP14Lexer.Operator,
                CPP14Lexer.Star,
                CPP14Lexer.And,
                CPP14Lexer.AndAnd,
                CPP14Lexer.LeftBracket,
                CPP14Lexer.Ellipsis,
                CPP14Lexer.Doublecolon,
                CPP14Lexer.Semi,
            });

            // Правила, которые нас интересуют
            completion.PreferredRules.UnionWith(new[]
            {
                CPP14Parser.RULE_classname,
                CPP14Parser.RULE_namespacename,
                CPP14Parser.RULE_idexpression
            });

            // 1) Кандидаты в начале файла
            var candidates = completion.CollectCandidates(0);

            Assert.That(candidates.Tokens, Is.Not.Empty);
            Assert.That(candidates.Rules, Is.Not.Empty);

            var vocab = new CPP14Lexer(new AntlrInputStream("")).Vocabulary;
            Console.WriteLine("Candidates:");
            foreach (var kvp in candidates.Tokens)
            {
                var name = vocab.GetLiteralName(kvp.Key)
                           ?? vocab.GetSymbolicName(kvp.Key)
                           ?? kvp.Key.ToString();
                Console.WriteLine($"[{name}]");
            }

            // Проверим, что среди кандидатов есть ключевые слова, как в оригинальном тесте
            var tokenTypes = candidates.Tokens.Keys.ToHashSet();
            Assert.That(tokenTypes, Does.Contain(CPP14Lexer.Class));
            Assert.That(tokenTypes, Does.Contain(CPP14Lexer.Namespace));
            Assert.That(tokenTypes, Does.Contain(CPP14Lexer.Auto));
        }

        [Test]
        public void SimpleCppExampleWithErrors()
        {
            string source = @"
class A {
public:
  void test() {
    if ()
  }
};
";
            var parser = CreateParser(source);
            parser.translationunit();

            // Парсер должен выдать ошибки
            Assert.That(parser.NumberOfSyntaxErrors, Is.GreaterThan(0));

            var completion = new CodeCompletionCore(parser);

            completion.IgnoredTokens.UnionWith(new[]
            {
                CPP14Lexer.Identifier,
                CPP14Lexer.Operator,
                CPP14Lexer.Star,
                CPP14Lexer.And,
                CPP14Lexer.AndAnd,
                CPP14Lexer.LeftBracket,
                CPP14Lexer.Ellipsis,
                CPP14Lexer.Doublecolon,
                CPP14Lexer.Semi,
            });

            completion.PreferredRules.UnionWith(new[]
            {
                CPP14Parser.RULE_classname,
                CPP14Parser.RULE_namespacename,
                CPP14Parser.RULE_idexpression
            });

            // В позиции 11 ожидается токен '('
            var candidates = completion.CollectCandidates(11);
            Assert.That(candidates.Tokens.Keys, Does.Contain(CPP14Lexer.LeftParen));

            // В позиции 12 — выражения (this, new, throw)
            candidates = completion.CollectCandidates(12);
            var tokenSet = candidates.Tokens.Keys.ToHashSet();
            Assert.That(tokenSet, Does.Contain(CPP14Lexer.This));
            Assert.That(tokenSet, Does.Contain(CPP14Lexer.New));
            Assert.That(tokenSet, Does.Contain(CPP14Lexer.Throw));

            // После ошибки — пусто
            candidates = completion.CollectCandidates(13);
            Assert.That(candidates.Tokens, Is.Empty);
            Assert.That(candidates.Rules, Is.Empty);
        }

        [Test]
        public void RealCppFile()
        {
            // Пример чтения реального файла Parser.cpp (как в C++)
            var path = Path.Combine("..", "..", "..", "..", "tests", "Parser.cpp");
            Assert.That(File.Exists(path), $"File not found: {path}");

            var source = File.ReadAllText(path);
            var parser = CreateParser(source);
            parser.translationunit();

            Assert.Zero(parser.NumberOfSyntaxErrors);

            var completion = new CodeCompletionCore(parser);
            completion.IgnoredTokens.UnionWith(new[]
            {
                CPP14Lexer.Identifier,
                CPP14Lexer.LeftParen,
                CPP14Lexer.RightParen,
                CPP14Lexer.Operator,
                CPP14Lexer.Star,
                CPP14Lexer.And,
                CPP14Lexer.AndAnd,
                CPP14Lexer.LeftBracket,
                CPP14Lexer.Ellipsis,
                CPP14Lexer.Doublecolon,
                CPP14Lexer.Semi,
            });
            completion.PreferredRules.UnionWith(new[]
            {
                CPP14Parser.RULE_classname,
                CPP14Parser.RULE_namespacename,
                CPP14Parser.RULE_idexpression
            });

            var candidates = completion.CollectCandidates(3469);
            Assert.That(candidates.Tokens, Is.Not.Empty);
        }
    }
}
