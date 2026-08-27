using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4C3.Demo.Grammar;
using Xunit;

namespace Antlr4C3.Tests
{
    public class SQLiteTest
    {
        private static SQLiteParser CreateParser(string input)
        {
            var lexer = new SQLiteLexer(new AntlrInputStream(input));
            lexer.RemoveErrorListeners();
            var parser = new SQLiteParser(new CommonTokenStream(lexer));
            parser.RemoveErrorListeners();
            return parser;
        }

        [Fact]
        public void ReturnsSqlStatementsAtInputStart()
        {
            var parser = CreateParser("");
            var context = parser.parse();
            var core = new CodeCompletionCore(parser);

            var candidates = core.CollectCandidates(0, context);

            Assert.Equal(25, candidates.Tokens.Count);
            Assert.Empty(candidates.Rules);
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.SELECT_));
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.CREATE_));
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.INSERT_));
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.UPDATE_));
        }

        [Fact]
        public void ReturnsClausesAfterFromAndIgnoresIdentifiers()
        {
            var parser = CreateParser("SELECT * FROM ");
            var context = parser.parse();
            var core = new CodeCompletionCore(parser)
            {
                ignoredTokens = new HashSet<int> { SQLiteLexer.STAR, SQLiteLexer.IDENTIFIER },
            };

            var candidates = core.CollectCandidates(3, context);

            Assert.Equal(12, candidates.Tokens.Count);
            Assert.Empty(candidates.Rules);
            Assert.False(candidates.Tokens.ContainsKey(SQLiteLexer.IDENTIFIER));
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.WHERE_));
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.ORDER_));
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.LIMIT_));
        }

        [Fact]
        public void ReturnsExpressionCandidatesAfterWhereAndIgnoresIdentifiers()
        {
            var parser = CreateParser("SELECT * FROM users WHERE ");
            var context = parser.parse();
            var core = new CodeCompletionCore(parser)
            {
                ignoredTokens = new HashSet<int> { SQLiteLexer.STAR, SQLiteLexer.IDENTIFIER },
            };

            var candidates = core.CollectCandidates(5, context);

            Assert.Equal(97, candidates.Tokens.Count);
            Assert.Empty(candidates.Rules);
            Assert.False(candidates.Tokens.ContainsKey(SQLiteLexer.IDENTIFIER));
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.STRING_LITERAL));
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.OPEN_PAR));
        }

        [Fact]
        public void SuggestsSchemaAndTableRulesAfterFrom()
        {
            var parser = CreateParser("SELECT * FROM ");
            var context = parser.parse();
            var core = new CodeCompletionCore(parser)
            {
                preferredRules = new HashSet<int>
                {
                    SQLiteParser.RULE_schema_name,
                    SQLiteParser.RULE_table_name,
                },
            };

            var candidates = core.CollectCandidates(6, context);

            Assert.Equal(98, candidates.Tokens.Count);
            Assert.Equal(2, candidates.Rules.Count);
            Assert.True(candidates.Rules.ContainsKey(SQLiteParser.RULE_schema_name));
            Assert.True(candidates.Rules.ContainsKey(SQLiteParser.RULE_table_name));
            Assert.Equal(6, candidates.Rules[SQLiteParser.RULE_schema_name].StartTokenIndex);
            Assert.Equal(6, candidates.Rules[SQLiteParser.RULE_table_name].StartTokenIndex);
        }

        [Fact]
        public void SuggestsColumnAliasRuleAfterAs()
        {
            var parser = CreateParser("SELECT name AS ");
            var context = parser.parse();
            var core = new CodeCompletionCore(parser)
            {
                preferredRules = new HashSet<int> { SQLiteParser.RULE_column_alias },
            };

            var candidates = core.CollectCandidates(6, context);

            Assert.Empty(candidates.Tokens);
            Assert.Single(candidates.Rules);
            Assert.True(candidates.Rules.ContainsKey(SQLiteParser.RULE_column_alias));
            Assert.Equal(6, candidates.Rules[SQLiteParser.RULE_column_alias].StartTokenIndex);
        }

        [Fact]
        public void SuggestsNextClauseAfterCompletedSelect()
        {
            var parser = CreateParser("SELECT name FROM users ");
            var context = parser.parse();
            var core = new CodeCompletionCore(parser)
            {
                ignoredTokens = new HashSet<int> { SQLiteLexer.IDENTIFIER },
            };

            var candidates = core.CollectCandidates(4, context);

            Assert.Equal(137, candidates.Tokens.Count);
            Assert.Empty(candidates.Rules);
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.WHERE_));
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.GROUP_));
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.ORDER_));
            Assert.True(candidates.Tokens.ContainsKey(SQLiteLexer.LIMIT_));
        }

        [Fact]
        public void SuggestsRulesInComplexCteJoinQuery()
        {
            var input = "WITH recent AS (SELECT u.id, u.name, COUNT(o.id) AS order_count FROM main.users AS u "
                + "LEFT JOIN orders AS o ON o.user_id = u.id WHERE o.created_at >= '2025-01-01' GROUP BY u.id, u.name "
                + "HAVING COUNT(o.id) > 2) SELECT r.name, r.order_count FROM recent AS r JOIN audit AS a "
                + "ON a.user_id = r.id ORDER BY ";
            var parser = CreateParser(input);
            var context = parser.parse();
            var core = new CodeCompletionCore(parser)
            {
                preferredRules = new HashSet<int>
                {
                    SQLiteParser.RULE_schema_name,
                    SQLiteParser.RULE_table_name,
                    SQLiteParser.RULE_column_name,
                    SQLiteParser.RULE_column_alias,
                    SQLiteParser.RULE_function_name,
                },
            };

            var candidates = core.CollectCandidates(142, context);

            Assert.Equal(108, candidates.Tokens.Count);
            Assert.Equal(3, candidates.Rules.Count);
            foreach (var rule in new[]
            {
                SQLiteParser.RULE_function_name,
                SQLiteParser.RULE_schema_name,
                SQLiteParser.RULE_table_name,
            })
            {
                Assert.True(candidates.Rules.ContainsKey(rule));
                Assert.Equal(142, candidates.Rules[rule].StartTokenIndex);
            }
            Assert.False(candidates.Rules.ContainsKey(SQLiteParser.RULE_column_name));
            Assert.False(candidates.Rules.ContainsKey(SQLiteParser.RULE_column_alias));
        }

        [Fact]
        public void SuggestsRulesInComplexCteSubquery()
        {
            var input = "WITH totals AS (SELECT customer_id, SUM(amount) AS total FROM payments WHERE status = 'paid' "
                + "GROUP BY customer_id) SELECT c.name AS customer_name, t.total FROM customers AS c JOIN totals AS t "
                + "ON t.customer_id = c.id WHERE c.active = 1 GROUP BY c.name, t.total HAVING t.total > "
                + "(SELECT AVG(total) FROM totals) ORDER BY customer_name DESC LIMIT ";
            var parser = CreateParser(input);
            var context = parser.parse();
            var core = new CodeCompletionCore(parser)
            {
                preferredRules = new HashSet<int>
                {
                    SQLiteParser.RULE_schema_name,
                    SQLiteParser.RULE_table_name,
                    SQLiteParser.RULE_column_name,
                    SQLiteParser.RULE_column_alias,
                    SQLiteParser.RULE_function_name,
                },
            };

            var candidates = core.CollectCandidates(137, context);

            Assert.Equal(108, candidates.Tokens.Count);
            Assert.Equal(3, candidates.Rules.Count);
            foreach (var rule in new[]
            {
                SQLiteParser.RULE_function_name,
                SQLiteParser.RULE_schema_name,
                SQLiteParser.RULE_table_name,
            })
            {
                Assert.True(candidates.Rules.ContainsKey(rule));
                Assert.Equal(137, candidates.Rules[rule].StartTokenIndex);
            }
            Assert.False(candidates.Rules.ContainsKey(SQLiteParser.RULE_column_name));
            Assert.False(candidates.Rules.ContainsKey(SQLiteParser.RULE_column_alias));
        }
    }
}