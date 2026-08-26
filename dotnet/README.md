# antlr4-c3 — .NET port

A .NET port of the Java `CodeCompletionCore` implementation (antlr4-c3).

- Core: [`src/Antlr4C3/CodeCompletionCore.cs`](src/Antlr4C3/CodeCompletionCore.cs)
- Target framework: `netstandard2.0`
- ANTLR runtime: **`Antlr4.Runtime.Standard` 4.7.2** (the ANTLR 4.7 line)

This is an independent translation of the Java source
`ports/java/src/main/java/com/vmware/antlr4c3/CodeCompletionCore.java`,
preserving the same algorithm and structure (including `RuleWithStartToken`,
`translateRulesTopDown`, and the `isExhaustive` follow-sets logic).

## Structure

```
dotnet/
├── Antlr4C3.slnx
├── src/Antlr4C3/            — core (CodeCompletionCore port)
├── test/Antlr4C3.Tests/     — xUnit tests (ExprTest port)
│   └── Grammar/             — Expr.g4 + the generated ANTLR parser
└── tools/                   — antlr-4.7.2-complete.jar (for parser regeneration)
```

## Build

```powershell
dotnet build dotnet -c Release
```

## Tests

```powershell
dotnet test dotnet
```

The tests are a port of the Java `ExprTest` class. Important detail: the
follow-sets cache (`followSetsByATN`) is static in the original source, and
`GetFollowingTokens` filters `ignoredTokens`. As a result `TypicalExpressionTest`
depends on `MostSimpleSetup` running first (in Java this is guaranteed by
`@Order`). In .NET the ordering is enforced via `TestPriorityOrderer` +
`[TestOrder(n)]`.

### Regenerating the parser from the grammar

Java is required. The parser was generated with the official ANTLR 4.7.2:

```powershell
java -jar dotnet/tools/antlr-4.7.2-complete.jar -Dlanguage=CSharp `
  -package Antlr4C3.Tests.Grammar `
  -o dotnet/test/Antlr4C3.Tests/Grammar `
  dotnet/test/Antlr4C3.Tests/Grammar/Expr.g4
```

## Usage

```csharp
var core = new Antlr4C3.CodeCompletionCore(parser)
{
	preferredRules = new HashSet<int> { MyParser.RULE_expression },
	ignoredTokens = new HashSet<int> { MyParser.WS },
};

var candidates = core.CollectCandidates(caretTokenIndex, context: null);
// candidates.Tokens, candidates.Rules
```

## API mapping notes (Java → .NET Standard runtime)

- `getSerializationType()` → `TransitionType` (enum, ALL_CAPS values: `RULE`, `ATOM`, …)
- `getStateType()` → `StateType` (enum, PascalCase: `RuleStop`, …)
- `state.getTransitions()` → `state.TransitionsArray`
- `RuleStartState.isLeftRecursiveRule` → `RuleStartState.isPrecedenceRule`
- `Token.EOF` → `TokenConstants.EOF`
- `CharStreams.fromString(...)` → `new AntlrInputStream(...)`
