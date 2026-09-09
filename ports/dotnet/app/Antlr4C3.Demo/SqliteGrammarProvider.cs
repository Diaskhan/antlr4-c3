using System.Collections.Generic;
using Antlr4.Runtime;
using SqlGrammar = Antlr4C3.Grammars;

namespace Antlr4C3.Demo
{
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
