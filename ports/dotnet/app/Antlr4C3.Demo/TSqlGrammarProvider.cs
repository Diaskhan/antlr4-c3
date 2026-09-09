using System.Collections.Generic;
using Antlr4.Runtime;
using SqlGrammar = Antlr4C3.Grammars;

namespace Antlr4C3.Demo
{
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
}
