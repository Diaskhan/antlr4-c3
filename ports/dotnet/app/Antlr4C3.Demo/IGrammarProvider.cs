using System.Collections.Generic;
using Antlr4.Runtime;

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
}
