using Antlr4.Runtime;
using AntlrC3;

public class TSqlAutocomplete
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
            Console.WriteLine($"Syntax error at line {line}, char {charPositionInLine}: {msg}");
        }
    }

    public static int GetCaretTokenIndex(CommonTokenStream tokenStream, int caretCharIndex)
    {
        var tokens = tokenStream.GetTokens();
        if (tokens == null || tokens.Count == 0) return 0;

        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Type == TokenConstants.EOF)
                return t.TokenIndex;

            if (t.StartIndex <= caretCharIndex && caretCharIndex <= t.StopIndex)
                return t.TokenIndex;

            if (caretCharIndex < t.StartIndex)
                return t.TokenIndex;
        }

        return tokens.Last().TokenIndex;
    }

    public static IList<string> GetSuggestionsForCaret(string text, int caretCharIndex)
    {
        var input = new AntlrInputStream(text);
        var lexer = new TSqlLexer(input);
        var tokenStream = new CommonTokenStream(lexer);
        tokenStream.Fill();

        var parser = new TSqlParser(tokenStream);

        // 🧩 Вместо выброса исключений просто логируем ошибки
        parser.RemoveErrorListeners();
        parser.ErrorHandler = new BailErrorStrategy(); // заменим чуть ниже на мягкую версию

        // 🚑 Включаем «мягкий» обработчик ошибок (чтобы не падал)
        parser.ErrorHandler = new DefaultErrorStrategy();

        try
        {
            parser.BuildParseTree = true;
            parser.tsql_file(); // можно не использовать результат
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Parser skipped due to invalid input: {ex.Message}]");
        }

        var core = new CodeCompletionCore(parser);

        int caretTokenIndex = GetCaretTokenIndex(tokenStream, caretCharIndex);
        string partialText = "";
        if (caretCharIndex > 0 && caretCharIndex <= text.Length)
        {
            int start = Math.Max(0, caretCharIndex - 1);
            partialText = text.Substring(start, caretCharIndex - start)
                .Split(' ', '\n', '\t').LastOrDefault() ?? "";
        }

        Console.WriteLine($"Partial text: '{partialText}'");

        // ✨ Получаем кандидатов
        var candidates = core.CollectCandidates(caretTokenIndex);

        // 🔍 Фильтрация + ограничение
        var suggestions = candidates.Tokens.Keys
            .Select(k => parser.Vocabulary.GetDisplayName(k))
            //.Where(name => name.StartsWith(partialText, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();

        return suggestions;
    }


    public static void Main()
    {
        string[] inputs =
        {
            "insert ",            // неполное слово
            "SELECT ",          // курсор после SELECT
            "SELECT name FROM ;",
            "SELECT arg1 ",        // курсор перед ;
            "SELECT arg1,arg2 F ",        // курсор перед ;
        };

        //foreach (var input in inputs)
        //{
        //    // вычисляем позицию курсора
        //    int caretCharIndex = input.Contains('|') ? input.IndexOf('|') : input.Length;
        //    string cleanInput = input.Replace("|", ""); // удаляем визуальный курсор

        //    Console.WriteLine(new string('-', 40));
        //    Console.WriteLine($"Input: \"{cleanInput}\"");
        //    Console.WriteLine($"Caret at: {caretCharIndex}");
        //    Console.WriteLine($"Text visualization: {cleanInput.Insert(caretCharIndex, "|")}");

        //    var suggestions = GetSuggestionsForCaret(cleanInput, caretCharIndex);
        //    Console.WriteLine("Result → " + string.Join(", ", suggestions));
        //    Console.WriteLine();
        //}

        foreach (string input in inputs)
        {
            var inputStream = CharStreams.fromString(input);
            var lexer = new TSqlLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            tokenStream.Fill();

            var parser = new TSqlParser(tokenStream);
            var errorListener = new TestErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            parser.BuildParseTree = true;
            parser.tsql_file(); // можно не использовать результат

            var core = new CodeCompletionCore(parser);
            var candidates = core.CollectCandidates(3);

            Console.WriteLine($"Input: {candidates.Tokens.Count}");
        }
    }
}
