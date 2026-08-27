using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using Antlr4C3.Grammars;
using Xunit;

namespace Antlr4C3.Tests
{
    public class Cpp14Test
    {
        private static readonly HashSet<int> IgnoredTokens = new HashSet<int>
        {
            CPP14Lexer.Identifier,
            CPP14Lexer.LeftParen, CPP14Lexer.RightParen,
            CPP14Lexer.Operator, CPP14Lexer.Star, CPP14Lexer.And, CPP14Lexer.AndAnd,
            CPP14Lexer.LeftBracket,
            CPP14Lexer.Ellipsis,
            CPP14Lexer.Doublecolon, CPP14Lexer.Semi,
        };

        private static readonly HashSet<int> PreferredRules = new HashSet<int>
        {
            CPP14Parser.RULE_classname, CPP14Parser.RULE_namespacename, CPP14Parser.RULE_idexpression,
        };

        private static CPP14Parser CreateParser(string source, out CountingErrorListener errorListener)
        {
            var lexer = new CPP14Lexer(new AntlrInputStream(source));
            var tokens = new CommonTokenStream(lexer);
            var parser = new CPP14Parser(tokens);
            parser.RemoveErrorListeners();
            errorListener = new CountingErrorListener();
            parser.AddErrorListener(errorListener);
            return parser;
        }

        [Fact]
        public void SimpleCppExample()
        {
            var source = "class A {\n" +
                "public:\n" +
                "  void test() {\n" +
                "  }\n" +
                "};\n";

            var parser = CreateParser(source, out var errorListener);
            parser.translationunit();
            Assert.Equal(0, errorListener.ErrorCount);

            var core = new CodeCompletionCore(parser)
            {
                ignoredTokens = new HashSet<int>(IgnoredTokens),
                preferredRules = new HashSet<int>(PreferredRules),
            };

            // 1) At the input start.
            var candidates = core.CollectCandidates(0, null);

            Assert.Equal(40, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Extern));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Mutable));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Register));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Static));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Thread_local));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Decltype));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Char));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Char16));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Char32));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Wchar));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Bool));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Short));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Int));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Long));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Signed));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Unsigned));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Float));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Double));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Void));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Auto));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Class));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Struct));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Union));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Enum));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Typename));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Const));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Volatile));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Explicit));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Inline));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Virtual));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Friend));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Typedef));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Constexpr));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Alignas));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Asm));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Namespace));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Using));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Static_assert));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Template));
            Assert.True(candidates.Tokens.ContainsKey(TokenConstants.EOF));

            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Identifier));

            Assert.Equal(3, candidates.Rules.Count);
            Assert.Equal(new List<int>
            {
                CPP14Parser.RULE_translationunit,
                CPP14Parser.RULE_declarationseq,
                CPP14Parser.RULE_declaration,
                CPP14Parser.RULE_functiondefinition,
                CPP14Parser.RULE_declarator,
                CPP14Parser.RULE_ptrdeclarator,
                CPP14Parser.RULE_ptroperator,
                CPP14Parser.RULE_nestednamespecifier,
            }, candidates.Rules[CPP14Parser.RULE_namespacename].RuleList);
            Assert.Equal(new List<int>
            {
                CPP14Parser.RULE_translationunit,
                CPP14Parser.RULE_declarationseq,
                CPP14Parser.RULE_declaration,
                CPP14Parser.RULE_functiondefinition,
                CPP14Parser.RULE_declarator,
                CPP14Parser.RULE_ptrdeclarator,
                CPP14Parser.RULE_ptroperator,
                CPP14Parser.RULE_nestednamespecifier,
                CPP14Parser.RULE_typename,
            }, candidates.Rules[CPP14Parser.RULE_classname].RuleList);

            // 2) Within the method body.
            candidates = core.CollectCandidates(10, null);

            var idexpressionStack = new List<int>
            {
                CPP14Parser.RULE_translationunit,
                CPP14Parser.RULE_declarationseq,
                CPP14Parser.RULE_declaration,
                CPP14Parser.RULE_functiondefinition,
                CPP14Parser.RULE_declspecifierseq,
                CPP14Parser.RULE_declspecifier,
                CPP14Parser.RULE_typespecifier,
                CPP14Parser.RULE_classspecifier,
                CPP14Parser.RULE_memberspecification,
                CPP14Parser.RULE_memberspecification,
                CPP14Parser.RULE_memberdeclaration,

                CPP14Parser.RULE_memberdeclaratorlist,
                CPP14Parser.RULE_memberdeclarator,
                CPP14Parser.RULE_braceorequalinitializer,
                CPP14Parser.RULE_bracedinitlist,
                CPP14Parser.RULE_initializerlist,
                CPP14Parser.RULE_initializerclause,

                CPP14Parser.RULE_assignmentexpression,
                CPP14Parser.RULE_logicalorexpression,
                CPP14Parser.RULE_logicalandexpression,
                CPP14Parser.RULE_inclusiveorexpression,
                CPP14Parser.RULE_exclusiveorexpression,
                CPP14Parser.RULE_andexpression,
                CPP14Parser.RULE_equalityexpression,
                CPP14Parser.RULE_relationalexpression,
                CPP14Parser.RULE_shiftexpression,
                CPP14Parser.RULE_additiveexpression,
                CPP14Parser.RULE_multiplicativeexpression,
                CPP14Parser.RULE_pmexpression,
                CPP14Parser.RULE_castexpression,
                CPP14Parser.RULE_unaryexpression,
                CPP14Parser.RULE_postfixexpression,
                CPP14Parser.RULE_primaryexpression,
            };

            Assert.Equal(3, candidates.Rules.Count);
            Assert.Equal(idexpressionStack, candidates.Rules[CPP14Parser.RULE_idexpression].RuleList);

            var classnameStack = new List<int>(idexpressionStack.GetRange(0, idexpressionStack.Count - 1))
            {
                CPP14Parser.RULE_simpletypespecifier,
                CPP14Parser.RULE_nestednamespecifier,
                CPP14Parser.RULE_typename,
            };
            Assert.Equal(classnameStack, candidates.Rules[CPP14Parser.RULE_classname].RuleList);

            var namespacenameStack = new List<int>(idexpressionStack.GetRange(0, idexpressionStack.Count - 1))
            {
                CPP14Parser.RULE_simpletypespecifier,
                CPP14Parser.RULE_nestednamespecifier,
            };
            Assert.Equal(namespacenameStack, candidates.Rules[CPP14Parser.RULE_namespacename].RuleList);

            // We should receive more specific rules when translating top down.
            core.translateRulesTopDown = true;
            candidates = core.CollectCandidates(10, null);

            Assert.Equal(3, candidates.Rules.Count);
            Assert.Equal(idexpressionStack, candidates.Rules[CPP14Parser.RULE_idexpression].RuleList);
            Assert.Equal(classnameStack, candidates.Rules[CPP14Parser.RULE_classname].RuleList);
            Assert.Equal(namespacenameStack, candidates.Rules[CPP14Parser.RULE_namespacename].RuleList);

            // We are starting a primary expression in a function body, so everything related to expressions and
            // control flow is allowed here. We only check for a few possible keywords.
            Assert.Equal(82, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.If));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.This));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.New));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Case));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.While));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Throw));

            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Override));
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Export));
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Private));
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Protected));

            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Decltype));
        }

        [Fact]
        public void SimpleCppExampleWithErrorsInInput()
        {
            var source = "class A {\n" +
                "public:\n" +
                "  void test() {\n" +
                "    if ()" +
                "  }\n" +
                "};\n";

            var parser = CreateParser(source, out var errorListener);
            parser.translationunit();
            Assert.Equal(3, errorListener.ErrorCount);

            var core = new CodeCompletionCore(parser)
            {
                // Let parentheses show up in this test.
                ignoredTokens = new HashSet<int>
                {
                    CPP14Lexer.Identifier,
                    CPP14Lexer.Operator, CPP14Lexer.Star, CPP14Lexer.And, CPP14Lexer.AndAnd,
                    CPP14Lexer.LeftBracket,
                    CPP14Lexer.Ellipsis,
                    CPP14Lexer.Doublecolon, CPP14Lexer.Semi,
                },
                preferredRules = new HashSet<int>(PreferredRules),
            };

            var candidates = core.CollectCandidates(11, null); // At the opening parenthesis.

            Assert.Equal(1, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.LeftParen));

            // At the closing parenthesis -> again everything in an expression allowed
            // (no control flow this time, though).
            candidates = core.CollectCandidates(12, null);

            Assert.Equal(65, candidates.Tokens.Count);
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.If));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.This));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.New));
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Case));
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.While));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Throw));

            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Override));
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Export));
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Private));
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Protected));

            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Decltype));

            candidates = core.CollectCandidates(13, null); // After the error position -> no suggestions.
            Assert.Empty(candidates.Tokens);
            Assert.Empty(candidates.Rules);
        }

        [Fact]
        public void RealCppFile()
        {
            var source = File.ReadAllText("Parser.cpp");
            var parser = CreateParser(source, out var errorListener);
            parser.translationunit();
            Assert.Equal(0, errorListener.ErrorCount);

            var core = new CodeCompletionCore(parser)
            {
                ignoredTokens = new HashSet<int>(IgnoredTokens),
                preferredRules = new HashSet<int>(PreferredRules),
            };

            var candidates = core.CollectCandidates(3469, null);

            var idexpressionStack = new List<int>
            {
                CPP14Parser.RULE_translationunit,
                CPP14Parser.RULE_declarationseq,
                CPP14Parser.RULE_declaration,
                CPP14Parser.RULE_functiondefinition,
                CPP14Parser.RULE_functionbody,
                CPP14Parser.RULE_compoundstatement,
                CPP14Parser.RULE_statementseq,
                CPP14Parser.RULE_statement,
                CPP14Parser.RULE_declarationstatement,
                CPP14Parser.RULE_blockdeclaration,
                CPP14Parser.RULE_simpledeclaration,
                CPP14Parser.RULE_initdeclaratorlist,
                CPP14Parser.RULE_initdeclarator,
                CPP14Parser.RULE_declarator,
                CPP14Parser.RULE_noptrdeclarator,
                CPP14Parser.RULE_declaratorid,
            };

            Assert.Equal(3, candidates.Rules.Count);
            Assert.Equal(idexpressionStack, candidates.Rules[CPP14Parser.RULE_idexpression].RuleList);

            // We should receive more specific rules when translating top down.
            core.translateRulesTopDown = true;
            candidates = core.CollectCandidates(3469, null);

            Assert.Equal(3, candidates.Rules.Count);
            Assert.Equal(idexpressionStack, candidates.Rules[CPP14Parser.RULE_idexpression].RuleList);

            var classnameStack = new List<int>(idexpressionStack)
            {
                CPP14Parser.RULE_idexpression,
                CPP14Parser.RULE_qualifiedid,
                CPP14Parser.RULE_nestednamespecifier,
                CPP14Parser.RULE_typename,
            };
            Assert.Equal(classnameStack, candidates.Rules[CPP14Parser.RULE_classname].RuleList);

            var namespacenameStack = new List<int>(idexpressionStack)
            {
                CPP14Parser.RULE_idexpression,
                CPP14Parser.RULE_qualifiedid,
                CPP14Parser.RULE_nestednamespecifier,
            };
            Assert.Equal(namespacenameStack, candidates.Rules[CPP14Parser.RULE_namespacename].RuleList);

            // We are starting a primary expression in a function body, so everything related to expressions and
            // control flow is allowed here. We only check for a few possible keywords.
            Assert.Equal(82, candidates.Tokens.Count);
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.If));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.This));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.New));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Case));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.While));
            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Throw));

            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Override));
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Export));
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Private));
            Assert.False(candidates.Tokens.ContainsKey(CPP14Lexer.Protected));

            Assert.True(candidates.Tokens.ContainsKey(CPP14Lexer.Decltype));
        }
    }
}
