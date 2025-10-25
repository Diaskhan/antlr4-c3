using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Antlr4CodeCompletion;

namespace Antlr4CodeCompletion.Tests
{
    // NOTE:
    // This file is a line-by-line translation of the TypeScript `SymbolTable.spec.ts` tests to NUnit.
    // It assumes the C# port exposes the same class names and APIs as the TypeScript sources:
    // `SymbolTable`, `ClassSymbol`, `InterfaceSymbol`, `MethodSymbol`, `BlockSymbol`, `VariableSymbol`,
    // `FieldSymbol`, `LiteralSymbol`, `NamespaceSymbol`, `RoutineSymbol`, `TypeAlias`, `DuplicateSymbolError`,
    // `FundamentalType`, `ScopedSymbol`, `BaseSymbol` and similar members (async methods returning Task or synchronous
    // counterparts). If method names or signatures differ in your C# port you will need to adapt the calls below.
    //
    // The tests use Antlr4.Runtime's TerminalNodeImpl as the dummy parse-tree context (similar to the TS tests).
    // If your project uses a different parse-tree type adjust `dummyNode` accordingly.

    [TestFixture]
    public class SymbolTableTests
    {
        private static readonly IParseTree DummyNode =
            new TerminalNodeImpl(new CommonToken(0, "dummy"));

        private async Task<SymbolTable> CreateClassSymbolTable(string name, int[] counts, string[] namespaces = null)
        {
            // This follows the TypeScript helper semantics: create a symbol table,
            // add classes/interfaces, methods, blocks and variables, plus some top-level variables and literals.
            // Adapt construction of SymbolTable and AddNewSymbolOfType calls to your C# signatures if needed.

            var symbolTable = new SymbolTable(name, new SymbolTableOptions { AllowDuplicateSymbols = false });

            NamespaceSymbol[] nsSymbols = { null };
            int nsIndex = 0;
            int nsCount = 1;

            if (namespaces != null && namespaces.Length > 0)
            {
                nsCount = namespaces.Length;
                nsSymbols = new NamespaceSymbol[namespaces.Length];
                for (int i = 0; i < nsCount; ++i)
                {
                    // Assuming an async AddNewNamespaceFromPath exists. If a sync variant exists use it.
                    nsSymbols[i] = (NamespaceSymbol)await symbolTable.AddNewNamespaceFromPath(null, namespaces[i]);
                }
            }

            for (int i = 0; i < counts[0]; ++i)
            {
                var classSymbol = symbolTable.AddNewSymbolOfType<ClassSymbol>(nsSymbols[nsIndex], $"class{i}", Array.Empty<ClassSymbol>(), Array.Empty<object>());
                var interfaceSymbol = symbolTable.AddNewSymbolOfType<InterfaceSymbol>(null, $"interface{i}", Array.Empty<object>());

                for (int j = 0; j < counts[2]; ++j)
                {
                    symbolTable.AddNewSymbolOfType<FieldSymbol>(classSymbol, $"field{j}", null);
                    symbolTable.AddNewSymbolOfType<FieldSymbol>(interfaceSymbol, $"field{j}", null);
                }

                for (int j = 0; j < counts[1]; ++j)
                {
                    var method = symbolTable.AddNewSymbolOfType<MethodSymbol>(classSymbol, $"method{j}");
                    symbolTable.AddNewSymbolOfType<MethodSymbol>(interfaceSymbol, $"method{j}");

                    // Blocks are created at top level first, then moved into the method.
                    var block1 = symbolTable.AddNewSymbolOfType<BlockSymbol>(null, "block1");
                    symbolTable.AddNewSymbolOfType<VariableSymbol>(block1, "var1", 17, FundamentalType.IntegerType);
                    var block2 = symbolTable.AddNewSymbolOfType<BlockSymbol>(null, "block2");
                    var symbol = symbolTable.AddNewSymbolOfType<VariableSymbol>(block2, "var1", 3.142, FundamentalType.FloatType);
                    if (j == counts[1] - 1)
                    {
                        symbol.Context = DummyNode;
                    }

                    method.AddSymbol(block1);
                    method.AddSymbol(block2);
                }

                ++nsIndex;
                if (nsIndex == nsCount) nsIndex = 0;
            }

            for (int i = 0; i < counts[3]; ++i)
            {
                symbolTable.AddNewSymbolOfType<VariableSymbol>(null, $"globalVar{i}", 42, FundamentalType.IntegerType);
            }

            for (int i = 0; i < counts[4]; ++i)
            {
                symbolTable.AddNewSymbolOfType<LiteralSymbol>(null, $"globalConst{i}", "string constant", FundamentalType.StringType);
            }

            return symbolTable;
        }

        [Test]
        public async Task SingleTableBaseTests()
        {
            var symbolTable = await CreateClassSymbolTable("main", new[] { 3, 3, 4, 5, 5 });
            var info = symbolTable.Info;
            Assert.AreEqual(0, info.DependencyCount);
            Assert.AreEqual(16, info.SymbolCount); // 5 + 5 top level symbols + 3 classes + 3 interfaces.

            // Attempt to add duplicate top-level variable -> DuplicateSymbolError
            Assert.Throws<DuplicateSymbolError>(() =>
            {
                symbolTable.AddNewSymbolOfType<VariableSymbol>(null, "globalVar3", null);
            }, "Attempt to add duplicate symbol 'globalVar3'");

            var class1 = await symbolTable.ResolveAsync("class1");
            Assert.IsInstanceOf<ClassSymbol>(class1);

            var method2 = await ((ClassSymbol)class1).ResolveAsync("method2");
            Assert.IsInstanceOf<MethodSymbol>(method2);

            var scopes = await ((MethodSymbol)method2).DirectScopes;
            Assert.AreEqual(2, scopes.Count);
            Assert.IsInstanceOf<ScopedSymbol>(scopes[0]);

            var block1 = scopes[0];
            Assert.Throws<DuplicateSymbolError>(() =>
            {
                var duplicateMethod = symbolTable.AddNewSymbolOfType<MethodSymbol>(null, "method2");
                ((ClassSymbol)class1).AddSymbol(duplicateMethod); // must throw
            }, "Attempt to add duplicate symbol 'method2'");

            // Resolve global var from block scope
            var variable = await scopes[0].ResolveAsync("globalVar3");
            Assert.IsInstanceOf<VariableSymbol>(variable);
            Assert.AreEqual(symbolTable, variable.Root);

            variable = await scopes[0].ResolveAsync("globalVar3", localOnly: true);
            Assert.IsNull(variable);

            variable = await scopes[0].ResolveAsync("var1");
            Assert.AreEqual(class1, variable.Root);
            Assert.AreEqual(method2, variable.GetParentOfType<MethodSymbol>());

            var methods = await ((ClassSymbol)class1).GetSymbolsOfTypeAsync<MethodSymbol>();
            Assert.AreEqual(3, methods.Length);

            var symbols = await ((MethodSymbol)method2).GetSymbolsOfTypeAsync<ScopedSymbol>();
            Assert.AreEqual(2, symbols.Length);

            Assert.AreEqual(class1, await block1.ResolveAsync("class1", localOnly: false));

            var symbolPath = variable.SymbolPath;
            Assert.AreEqual(5, symbolPath.Length);
            Assert.AreEqual("var1", symbolPath[0].Name);
            Assert.AreEqual("block1", symbolPath[1].Name);
            Assert.AreEqual("method2", symbolPath[2].Name);
            Assert.AreEqual("class1", symbolPath[3].Name);
            Assert.AreEqual("main", symbolPath[4].Name);

            Assert.AreEqual("class1.method2", method2.QualifiedName());
            Assert.AreEqual("main-class1-method2", method2.QualifiedName("-", true));
            Assert.AreEqual("block1.var1", variable.QualifiedName());
            Assert.AreEqual("block1#var1", variable.QualifiedName("#"));
            Assert.AreEqual("block1.var1", variable.QualifiedName(".", full: false, includeAnonymous: true));
            Assert.AreEqual("main.class1.method2.block1.var1", variable.QualifiedName(".", true, false));
            Assert.AreEqual("main.class1.method2.block1.var1", variable.QualifiedName(".", true, true));

            var allSymbols = await symbolTable.GetAllNestedSymbolsAsync();
            Assert.AreEqual(94, allSymbols.Length);

            var symbolPathStr = allSymbols[59].QualifiedName(".", true);
            Assert.AreEqual("main.class1.method2.block1.var1", symbolPathStr);

            var foundSymbol = symbolTable.SymbolFromPath("main.class2.method0.block2.var1");
            Assert.AreEqual(allSymbols[78], foundSymbol);

            Assert.AreEqual(symbolTable, symbolTable.SymbolTable);
        }

        [Test]
        public async Task SingleTableTypeChecks()
        {
            var symbolTable = await CreateClassSymbolTable("main", new[] { 1, 1, 1, 1, 1 });

            symbolTable.AddNewSymbolOfType<TypeAlias>(null, "newBool", FundamentalType.BoolType);
            symbolTable.AddNewSymbolOfType<RoutineSymbol>(null, "routine1", FundamentalType.IntegerType);
            symbolTable.AddNewSymbolOfType<FieldSymbol>(null, "field1", FundamentalType.FloatType);

            // TODO: finish the test details — preserved from original TS file.
            Assert.Pass("Type checks skeleton executed.");
        }

        [Test]
        public async Task SingleTableStressTest()
        {
            var symbolTable = await CreateClassSymbolTable("table", new[] { 300, 30, 20, 1000, 1000 });

            var symbols = await symbolTable.GetAllNestedSymbolsAsync();
            Assert.AreEqual(68600, symbols.Length);

            symbols = await symbolTable.GetNestedSymbolsOfTypeAsync<ClassSymbol>();
            Assert.AreEqual(300, symbols.Length);

            symbols = await symbolTable.GetNestedSymbolsOfTypeAsync<MethodSymbol>();
            Assert.AreEqual(18000, symbols.Length);

            symbols = await symbolTable.GetNestedSymbolsOfTypeAsync<ScopedSymbol>();
            Assert.AreEqual(36600, symbols.Length);

            symbols = await symbolTable.GetNestedSymbolsOfTypeAsync<VariableSymbol>();
            Assert.AreEqual(31000, symbols.Length);

            symbols = await symbolTable.GetNestedSymbolsOfTypeAsync<FieldSymbol>();
            Assert.AreEqual(12000, symbols.Length);

            symbols = await symbolTable.GetNestedSymbolsOfTypeAsync<LiteralSymbol>();
            Assert.AreEqual(1000, symbols.Length);
        }

        [Test]
        public async Task SingleTableNamespaceTests()
        {
            var symbolTable = await CreateClassSymbolTable("main", new[] { 30, 10, 10, 100, 100 },
                new[] { "ns1", "ns2", "ns1.ns3.ns5", "ns1.ns4.ns6.ns8" });

            var namespaces = await symbolTable.GetNestedSymbolsOfTypeAsync<NamespaceSymbol>();
            Assert.AreEqual(7, namespaces.Length);

            var methods = await symbolTable.GetNestedSymbolsOfTypeAsync<MethodSymbol>();
            Assert.AreEqual(600, methods.Length);
            Assert.AreEqual("main.ns1.ns3.ns5.class2.method2", methods[2].QualifiedName(".", true));
            Assert.AreEqual("main.ns2.class29.method9", methods[299].QualifiedName(".", true));
        }

        [Test]
        public async Task MultiTableTests()
        {
            var main = await CreateClassSymbolTable("main", new[] { 30, 10, 10, 100, 100 });

            var systemFunctions = new SymbolTable("system functions", new SymbolTableOptions { AllowDuplicateSymbols = false });
            var ns1 = systemFunctions.AddNewSymbolOfType<NamespaceSymbol>(null, "ns1");
            for (int i = 0; i < 333; ++i) systemFunctions.AddNewSymbolOfType<RoutineSymbol>(ns1, $"func{i}");
            main.AddDependencies(systemFunctions);

            var libFunctions = new SymbolTable("library functions", new SymbolTableOptions { AllowDuplicateSymbols = false });
            var ns2 = libFunctions.AddNewSymbolOfType<NamespaceSymbol>(null, "ns2");
            for (int i = 0; i < 444; ++i) libFunctions.AddNewSymbolOfType<RoutineSymbol>(ns2, $"func{i}");

            var libVariables = new SymbolTable("library variables", new SymbolTableOptions { AllowDuplicateSymbols = false });
            var ns3 = libVariables.AddNewSymbolOfType<NamespaceSymbol>(null, "ns1");
            for (int i = 0; i < 555; ++i) libVariables.AddNewSymbolOfType<VariableSymbol>(ns3, $"var{i}", null);

            var libFunctions2 = new SymbolTable("library functions 2", new SymbolTableOptions { AllowDuplicateSymbols = false });
            var ns4 = libFunctions2.AddNewSymbolOfType<NamespaceSymbol>(null, "ns1");
            for (int i = 0; i < 666; ++i) libFunctions2.AddNewSymbolOfType<RoutineSymbol>(ns4, $"func{i}");

            libVariables.AddDependencies(libFunctions, libFunctions2);
            main.AddDependencies(systemFunctions, libVariables);

            var allSymbols = await main.GetAllSymbolsAsync<BaseSymbol>();
            Assert.AreEqual(2262, allSymbols.Length);

            allSymbols = await main.GetAllSymbolsAsync<RoutineSymbol>();
            Assert.AreEqual(1443, allSymbols.Length);

            Assert.AreEqual(334, (await systemFunctions.GetAllSymbolsAsync<BaseSymbol>()).Length);
            Assert.AreEqual(445, (await libFunctions.GetAllSymbolsAsync<BaseSymbol>()).Length);
            Assert.AreEqual(1668, (await libVariables.GetAllSymbolsAsync<BaseSymbol>()).Length);
            Assert.AreEqual(666, (await libFunctions2.GetAllSymbolsAsync<RoutineSymbol>()).Length);
        }

        [Test]
        public async Task SymbolNavigation()
        {
            var symbolTable = await CreateClassSymbolTable("main", new[] { 10, 10, 10, 20, 34 }, null);

            var namespaces = await symbolTable.GetNestedSymbolsOfType<NamespaceSymbol>();
            Assert.AreEqual(0, namespaces.Length);

            var variables = await symbolTable.GetNestedSymbolsOfType<VariableSymbol>();
            Assert.AreEqual(420, variables.Length);

            var field7 = variables[211];
            Assert.IsNotNull(field7);
            Assert.AreEqual(variables[210], field7.FirstSibling);
            Assert.AreEqual("method9", field7.LastSibling.Name);
            Assert.AreEqual(variables[210], field7.PreviousSibling);
            Assert.AreEqual(variables[212], field7.NextSibling);

            Assert.AreEqual(field7.FirstSibling, field7.FirstSibling.FirstSibling.FirstSibling.FirstSibling);
            Assert.AreEqual(field7.LastSibling, field7.LastSibling.LastSibling.LastSibling.LastSibling);
            Assert.AreEqual(field7.FirstSibling, field7.FirstSibling.LastSibling.FirstSibling.FirstSibling);
            Assert.AreEqual(field7.LastSibling, field7.LastSibling.FirstSibling.FirstSibling.LastSibling);

            Assert.IsInstanceOf<InterfaceSymbol>(field7.Parent);

            var parent7 = (InterfaceSymbol)field7.Parent;
            Assert.AreEqual(1, parent7.IndexOfChild(field7));
            Assert.AreEqual(parent7.FirstChild, field7.FirstSibling);
            Assert.AreEqual(parent7.LastChild, field7.LastSibling);

            var var1 = variables[286];
            Assert.IsNotNull(var1);
            Assert.AreEqual(var1, var1.FirstSibling);
            Assert.AreEqual("var1", var1.LastSibling.Name);
            Assert.IsNull(var1.PreviousSibling);
            Assert.IsNull(var1.NextSibling);

            Assert.AreEqual(var1.FirstSibling, var1.FirstSibling.FirstSibling.FirstSibling.FirstSibling);
            Assert.AreEqual(var1.LastSibling, var1.LastSibling.LastSibling.LastSibling.LastSibling);
            Assert.AreEqual(var1.FirstSibling, var1.FirstSibling.LastSibling.FirstSibling.FirstSibling);
            Assert.AreEqual(var1.LastSibling, var1.LastSibling.FirstSibling.FirstSibling.LastSibling);

            var block1 = var1.Parent;
            Assert.AreEqual(-1, block1.IndexOfChild(field7));
            Assert.AreEqual(0, block1.IndexOfChild(var1));
            Assert.AreEqual(block1.FirstChild, var1.FirstSibling);
            Assert.AreEqual(block1.LastChild, var1.LastSibling);

            var var15 = variables[19];
            Assert.IsNotNull(var15);
            Assert.AreEqual(symbolTable.FirstChild, var15.FirstSibling);
            Assert.AreEqual("globalConst33", var15.LastSibling.Name);
            Assert.AreEqual(variables[18], var15.PreviousSibling);
            Assert.AreEqual("globalConst0", var15.NextSibling?.Name);

            Assert.IsInstanceOf<SymbolTable>(var15.Parent);

            var st1 = (ScopedSymbol)var15.Parent;
            Assert.AreEqual(39, st1.IndexOfChild(var15));
            Assert.AreEqual(st1.FirstChild, var15.FirstSibling);
            Assert.AreEqual(st1.LastChild, var15.LastSibling);

            var next = variables[284].Next;
            Assert.IsNotNull(next);
            Assert.AreEqual("main.class6.method7.block1.var1", next.QualifiedName(".", true));

            var symbol = await symbolTable.SymbolWithContext(DummyNode);
            Assert.IsNotNull(symbol);
            Assert.AreEqual("main.class0.method9.block2.var1", symbol.QualifiedName(".", true));
        }

        [Test]
        public void SearchContextInLargeSingleFieldList()
        {
            // Very large number of fields: test synchronous context search.
            var symbolTableTask = CreateClassSymbolTable("main", new[] { 1, 1, 1000 }, null);
            //symbolTableTask.Wait();
            var symbolTable = symbolTableTask.Result;

            var symbol = symbolTable.SymbolWithContextSync(DummyNode);
            Assert.IsNotNull(symbol);
            Assert.AreEqual("main.class0.method0.block2.var1", symbol.QualifiedName(".", true));
        }
    }
}