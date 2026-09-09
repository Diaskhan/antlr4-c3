using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Antlr4.Runtime;

namespace Antlr4C3.Demo
{
    public class MainForm : Form
    {
        private readonly TextBox _codeBox;
        private readonly TextBox _resultBox;
        private readonly Label _statusLabel;
        private readonly Label _codeLabel;
        private readonly ListBox _grammarList;

        // Grammars the user can pick from in the list box.
        private static readonly IGrammarProvider[] Grammars =
        {
            new Cpp14GrammarProvider(),
            new TSqlGrammarProvider(),
        };

        private IGrammarProvider _grammar;

        public MainForm()
        {
            _grammar = Grammars[0];

            Text = "Autocomplete Demo (antlr4-c3)";
            Width = 1000;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(8),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var grammarLabel = new Label { Text = "Grammar", AutoSize = true };
            _grammarList = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                SelectionMode = SelectionMode.One,
            };
            _grammarList.Items.AddRange(Grammars);
            _grammarList.SelectedIndex = 0;
            _grammarList.SelectedIndexChanged += (s, e) => OnGrammarChanged();

            var grammarPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            grammarPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
            grammarPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            grammarPanel.Controls.Add(grammarLabel, 0, 0);
            grammarPanel.Controls.Add(_grammarList, 0, 1);
            layout.Controls.Add(grammarPanel, 0, 0);
            layout.SetColumnSpan(grammarPanel, 2);

            _codeLabel = new Label { Text = "Code (move the caret to see completions)", AutoSize = true };
            var resultLabel = new Label { Text = "Autocomplete results at caret", AutoSize = true };
            layout.Controls.Add(_codeLabel, 0, 1);
            layout.Controls.Add(resultLabel, 1, 1);

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

            layout.Controls.Add(_codeBox, 0, 2);
            layout.Controls.Add(_resultBox, 1, 2);

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
            };

            Controls.Add(layout);
            Controls.Add(_statusLabel);

            _codeBox.Text = _grammar.SampleCode;

            _codeBox.KeyUp += (s, e) => UpdateCompletions();
            _codeBox.MouseUp += (s, e) => UpdateCompletions();
            _codeBox.TextChanged += (s, e) => UpdateCompletions();

            Load += (s, e) => UpdateCompletions();
        }

        private void OnGrammarChanged()
        {
            if (_grammarList.SelectedItem is IGrammarProvider grammar)
            {
                _grammar = grammar;
                _codeLabel.Text = _grammar.DisplayName + " code (move the caret to see completions)";
                _codeBox.Text = _grammar.SampleCode;
                UpdateCompletions();
            }
        }

        private void UpdateCompletions()
        {
            try
            {
                var code = _codeBox.Text;
                int caret = _codeBox.SelectionStart;

                var (parser, tokenStream) = _grammar.Parse(code);

                int caretTokenIndex = FindCaretTokenIndex(tokenStream, caret);

                var core = new CodeCompletionCore(parser)
                {
                    ignoredTokens = new HashSet<int>(_grammar.IgnoredTokens),
                    preferredRules = new HashSet<int>(_grammar.PreferredRules),
                };

                var candidates = core.CollectCandidates(caretTokenIndex, null);

                _resultBox.Text = FormatCandidates(candidates, parser);
                _statusLabel.Text =
                    $"Grammar: {_grammar.DisplayName}  |  Caret char: {caret}  |  Token index: {caretTokenIndex}  |  " +
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

        private static string FormatCandidates(CodeCompletionCore.CandidatesCollection candidates, Parser parser)
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
