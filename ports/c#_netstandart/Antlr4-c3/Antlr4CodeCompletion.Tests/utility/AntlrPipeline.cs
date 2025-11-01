using Antlr4.Runtime;

public class CountingErrorListener : BaseErrorListener
{
    public int ErrorCount { get; private set; }

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
    }
}

public class AntlrPipeline<TLexer, TParser>
    where TLexer : Lexer
    where TParser : Parser
{
    public readonly CountingErrorListener Listener = new();
    public readonly TLexer Lexer;
    public readonly CommonTokenStream Tokens;
    public readonly TParser Parser;

    public AntlrPipeline(string text)
    {
        var input = new AntlrInputStream(text);
        Lexer = (TLexer)Activator.CreateInstance(typeof(TLexer), input);
        Tokens = new CommonTokenStream(Lexer);
        Parser = (TParser)Activator.CreateInstance(typeof(TParser), Tokens);

        Parser.RemoveErrorListeners();
        Parser.AddErrorListener(Listener);
    }
}
