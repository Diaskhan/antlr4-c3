using System.Collections.Generic;
using Antlr4.Runtime;
using CppGrammar = Antlr4C3.Grammars;

namespace Antlr4C3.Demo
{
    // C++ (CPP14) grammar reused from the test project.
    public sealed class Cpp14GrammarProvider : IGrammarProvider
    {
        public string DisplayName => "C++ (CPP14)";

        public string SampleCode => "class A {\r\npublic:\r\n  void test() {\r\n    \r\n  }\r\n};\r\n";

        public ISet<int> IgnoredTokens { get; } = new HashSet<int>
        {
            CppGrammar.CPP14Lexer.Identifier,
            CppGrammar.CPP14Lexer.LeftParen, CppGrammar.CPP14Lexer.RightParen,
            CppGrammar.CPP14Lexer.Operator, CppGrammar.CPP14Lexer.Star, CppGrammar.CPP14Lexer.And, CppGrammar.CPP14Lexer.AndAnd,
            CppGrammar.CPP14Lexer.LeftBracket,
            CppGrammar.CPP14Lexer.Ellipsis,
            CppGrammar.CPP14Lexer.Doublecolon, CppGrammar.CPP14Lexer.Semi,
        };

        public ISet<int> PreferredRules { get; } = new HashSet<int>
        {
            CppGrammar.CPP14Parser.RULE_classname, CppGrammar.CPP14Parser.RULE_namespacename, CppGrammar.CPP14Parser.RULE_idexpression,
        };

        public (Parser Parser, CommonTokenStream Tokens) Parse(string code)
        {
            var inputStream = new AntlrInputStream(code);
            var lexer = new CppGrammar.CPP14Lexer(inputStream);
            lexer.RemoveErrorListeners();
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new CppGrammar.CPP14Parser(tokenStream);
            parser.RemoveErrorListeners();

            parser.translationunit();

            return (parser, tokenStream);
        }

        public override string ToString() => DisplayName;
    }
}
