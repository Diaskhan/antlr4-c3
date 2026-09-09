using Antlr4.Runtime;

namespace Antlr4C3.Tests
{
    public class CountingErrorListener : BaseErrorListener
    {
        public int ErrorCount = 0;

        public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            ErrorCount++;
        }
    }
}
