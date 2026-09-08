using System.Collections.Generic;
using Antlr4.Runtime;
using CppGrammar = Antlr4C3.Grammars;
using SqlGrammar = Antlr4C3.Grammars;

namespace Antlr4C3.Demo
{
    // Encapsulates everything the demo needs to run autocomplete for a single grammar:
    // how to build the parser, which entry rule to invoke, and the ignored tokens /
    // preferred rules used to tune the antlr4-c3 candidate collection.
    public interface IGrammarProvider
    {
        string DisplayName { get; }

        string SampleCode { get; }

        ISet<int> IgnoredTokens { get; }

        ISet<int> PreferredRules { get; }

        // Builds a parser, runs the entry rule and returns the parser together with its token stream.
        (Parser Parser, CommonTokenStream Tokens) Parse(string code);
    }

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

    // Microsoft SQL Server (T-SQL) grammar generated into this project.
    public sealed class TSqlGrammarProvider : IGrammarProvider
    {
        public string DisplayName => "MS SQL (T-SQL)";

        public string SampleCode => "SELECT * FROM dbo.Customers WHERE ;\r\n";

        // Mirrors the gravity-ui websql-autocomplete PostgreSQL strategy: ignore every
        // operator token (except STAR, still wanted for "SELECT *"), every built-in
        // function-name token, whitespace/comments and EOF.
        public ISet<int> IgnoredTokens { get; } = BuildIgnoredTokens();

        public ISet<int> PreferredRules { get; } = new HashSet<int>
        {
            // Object-name rules we DO want to collect completions for.
            SqlGrammar.TSqlParser.RULE_table_name,
            SqlGrammar.TSqlParser.RULE_full_table_name,
            SqlGrammar.TSqlParser.RULE_simple_name,
            SqlGrammar.TSqlParser.RULE_full_column_name,
            SqlGrammar.TSqlParser.RULE_column_name_list,
            SqlGrammar.TSqlParser.RULE_insert_column_id,
            SqlGrammar.TSqlParser.RULE_cursor_name,
            SqlGrammar.TSqlParser.RULE_scalar_function_name,
            SqlGrammar.TSqlParser.RULE_func_proc_name_schema,
            SqlGrammar.TSqlParser.RULE_func_proc_name_database_schema,
            SqlGrammar.TSqlParser.RULE_func_proc_name_server_database_schema,
            SqlGrammar.TSqlParser.RULE_as_column_alias,
            SqlGrammar.TSqlParser.RULE_column_alias,

            // Generic identifier / keyword rules: visited so their tokens resolve
            // through these rules instead of being offered as raw keyword candidates.
            SqlGrammar.TSqlParser.RULE_id_,
            SqlGrammar.TSqlParser.RULE_simple_id,
            SqlGrammar.TSqlParser.RULE_id_or_string,
            SqlGrammar.TSqlParser.RULE_keyword,
        };

        private static HashSet<int> BuildIgnoredTokens()
        {
            var tokens = new HashSet<int>
            {
                TokenConstants.EOF,
                SqlGrammar.TSqlLexer.SPACE,
                SqlGrammar.TSqlLexer.COMMENT,
                SqlGrammar.TSqlLexer.LINE_COMMENT,
            };

            // Operators: EQUAL .. PLACEHOLDER form a contiguous block in the lexer.
            // STAR is intentionally kept so "SELECT *" is still suggested.
            for (int token = SqlGrammar.TSqlLexer.EQUAL; token <= SqlGrammar.TSqlLexer.PLACEHOLDER; token++)
            {
                if (token != SqlGrammar.TSqlLexer.STAR)
                {
                    tokens.Add(token);
                }
            }

            // Built-in functions: ABS .. SP_EXECUTESQL form a contiguous block.
            for (int token = SqlGrammar.TSqlLexer.ABS; token <= SqlGrammar.TSqlLexer.SP_EXECUTESQL; token++)
            {
                tokens.Add(token);
            }

            return tokens;
        }

        public (Parser Parser, CommonTokenStream Tokens) Parse(string code)
        {
            var inputStream = new AntlrInputStream(code);
            var lexer = new SqlGrammar.TSqlLexer(inputStream);
            lexer.RemoveErrorListeners();
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new SqlGrammar.TSqlParser(tokenStream);
            parser.RemoveErrorListeners();

            parser.tsql_file();

            return (parser, tokenStream);
        }

        public override string ToString() => DisplayName;
    }

    // SQLite grammar (grammars-v4) generated into this project.
    public sealed class SqliteGrammarProvider : IGrammarProvider
    {
        public string DisplayName => "SQLite";

        public string SampleCode => "SELECT * FROM main.users WHERE ;\r\n";

        public ISet<int> IgnoredTokens { get; } = new HashSet<int>
        {
            SqlGrammar.SQLiteLexer.SPACES,
            SqlGrammar.SQLiteLexer.COMMA, SqlGrammar.SQLiteLexer.DOT, SqlGrammar.SQLiteLexer.SCOL,
            SqlGrammar.SQLiteLexer.STAR,
            SqlGrammar.SQLiteLexer.OPEN_PAR, SqlGrammar.SQLiteLexer.CLOSE_PAR,
            SqlGrammar.SQLiteLexer.IDENTIFIER, SqlGrammar.SQLiteLexer.NUMERIC_LITERAL, SqlGrammar.SQLiteLexer.STRING_LITERAL,
        };

        public ISet<int> PreferredRules { get; } = new HashSet<int>
        {
            SqlGrammar.SQLiteParser.RULE_schema_name,
            SqlGrammar.SQLiteParser.RULE_table_name,
            SqlGrammar.SQLiteParser.RULE_column_name,
            SqlGrammar.SQLiteParser.RULE_column_alias,
        };

        public (Parser Parser, CommonTokenStream Tokens) Parse(string code)
        {
            var inputStream = new AntlrInputStream(code);
            var lexer = new SqlGrammar.SQLiteLexer(inputStream);
            lexer.RemoveErrorListeners();
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new SqlGrammar.SQLiteParser(tokenStream);
            parser.RemoveErrorListeners();

            parser.parse();

            return (parser, tokenStream);
        }

        public override string ToString() => DisplayName;
    }
}
