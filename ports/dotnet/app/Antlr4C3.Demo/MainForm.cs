using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Antlr4.Runtime;
using Antlr4C3.Tests.Grammar;

namespace Antlr4C3.Demo
{
    public class MainForm : Form
    {
        private readonly TextBox _codeBox;
        private readonly TextBox _resultBox;
        private readonly Label _statusLabel;

        // Tokens/rules ignored or preferred, mirroring the C++ autocomplete test setup.
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

        public MainForm()
        {
            Text = "C++ Autocomplete Demo (antlr4-c3)";
            Width = 1000;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(8),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var codeLabel = new Label { Text = "C++ code (move the caret to see completions)", AutoSize = true };
            var resultLabel = new Label { Text = "Autocomplete results at caret", AutoSize = true };
            layout.Controls.Add(codeLabel, 0, 0);
            layout.Controls.Add(resultLabel, 1, 0);

            _codeBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                AcceptsTab = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new System.Drawing.Font("Consolas", 11f),
                HideSelection = false,
            };

            _resultBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new System.Drawing.Font("Consolas", 10f),
                BackColor = System.Drawing.Color.FromArgb(245, 245, 245),
            };

            layout.Controls.Add(_codeBox, 0, 1);
            layout.Controls.Add(_resultBox, 1, 1);

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
            };

            Controls.Add(layout);
            Controls.Add(_statusLabel);

            _codeBox.Text = "class A {\r\npublic:\r\n  void test() {\r\n    \r\n  }\r\n};\r\n";

            _codeBox.KeyUp += (s, e) => UpdateCompletions();
            _codeBox.MouseUp += (s, e) => UpdateCompletions();
            _codeBox.TextChanged += (s, e) => UpdateCompletions();

            Load += (s, e) => UpdateCompletions();
        }

        private void UpdateCompletions()
        {
            try
            {
                var code = _codeBox.Text;
                int caret = _codeBox.SelectionStart;

                var inputStream = new AntlrInputStream(code);
                var lexer = new CPP14Lexer(inputStream);
                lexer.RemoveErrorListeners();
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new CPP14Parser(tokenStream);
                parser.RemoveErrorListeners();

                parser.translationunit();

                int caretTokenIndex = FindCaretTokenIndex(tokenStream, caret);

                var core = new CodeCompletionCore(parser)
                {
                    ignoredTokens = new HashSet<int>(IgnoredTokens),
                    preferredRules = new HashSet<int>(PreferredRules),
                };

                var candidates = core.CollectCandidates(caretTokenIndex, null);

                _resultBox.Text = FormatCandidates(candidates, parser);
                _statusLabel.Text =
                    $"Caret char: {caret}  |  Token index: {caretTokenIndex}  |  " +
                    $"Tokens: {candidates.Tokens.Count}  |  Rules: {candidates.Rules.Count}";
            }
            catch (Exception ex)
            {
                _resultBox.Text = "Error: " + ex.Message;
                _statusLabel.Text = "Error while computing completions.";
            }
        }

        // Maps a character caret offset to the parser token index expected by antlr4-c3.
        private static int FindCaretTokenIndex(CommonTokenStream tokenStream, int caret)
        {
            tokenStream.Fill();
            var tokens = tokenStream.GetTokens();

            foreach (var token in tokens)
            {
                if (token.Type == TokenConstants.EOF)
                {
                    return token.TokenIndex;
                }

                // The first token that starts at or after the caret is the caret token.
                if (token.StartIndex >= caret)
                {
                    return token.TokenIndex;
                }

                // Caret located inside this token -> the token itself is the caret token.
                if (caret >= token.StartIndex && caret <= token.StopIndex + 1)
                {
                    return token.TokenIndex;
                }
            }

            return Math.Max(0, tokens.Count - 1);
        }

        private static string FormatCandidates(CodeCompletionCore.CandidatesCollection candidates, CPP14Parser parser)
        {
            var vocabulary = parser.Vocabulary;
            var ruleNames = parser.RuleNames;
            var sb = new StringBuilder();

            sb.AppendLine("=== Suggested tokens (" + candidates.Tokens.Count + ") ===");
            foreach (var kvp in candidates.Tokens.OrderBy(k => DisplayTokenName(vocabulary, k.Key)))
            {
                string name = DisplayTokenName(vocabulary, kvp.Key);
                if (kvp.Value != null && kvp.Value.Count > 0)
                {
                    string following = string.Join(" ", kvp.Value.Select(t => DisplayTokenName(vocabulary, t)));
                    sb.AppendLine($"  {name}  (followed by: {following})");
                }
                else
                {
                    sb.AppendLine($"  {name}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== Suggested rules (" + candidates.Rules.Count + ") ===");
            foreach (var kvp in candidates.Rules.OrderBy(k => RuleName(ruleNames, k.Key)))
            {
                sb.AppendLine($"  {RuleName(ruleNames, kvp.Key)}");
                if (kvp.Value?.RuleList != null && kvp.Value.RuleList.Count > 0)
                {
                    string path = string.Join(" > ", kvp.Value.RuleList.Select(r => RuleName(ruleNames, r)));
                    sb.AppendLine($"      path: {path}");
                }
            }

            return sb.ToString();
        }

        private static string DisplayTokenName(IVocabulary vocabulary, int type)
        {
            if (type == TokenConstants.EOF)
            {
                return "<EOF>";
            }

            string symbolic = vocabulary.GetSymbolicName(type);
            if (!string.IsNullOrEmpty(symbolic))
            {
                return symbolic;
            }

            string literal = vocabulary.GetLiteralName(type);
            return !string.IsNullOrEmpty(literal) ? literal : type.ToString();
        }

        private static string RuleName(string[] ruleNames, int index)
        {
            return index >= 0 && index < ruleNames.Length ? ruleNames[index] : index.ToString();
        }
    }
}
