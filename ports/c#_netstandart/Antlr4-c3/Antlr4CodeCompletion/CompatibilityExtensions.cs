using Antlr4.Runtime.Tree;

namespace Antlr4CodeCompletion
{
    public static class CompatibilityExtensions
    {
        // Provide AddNewSymbolOfType<T>(parent, params args) overload using Activator to create instances.
        public static T AddNewSymbolOfType<T>(this SymbolTable table, IScopedSymbol parent, params object[] args) where T : IBaseSymbol
        {
            var instance = (T)Activator.CreateInstance(typeof(T), args ?? Array.Empty<object>());
            var targetParent = parent ?? (IScopedSymbol)table;
            targetParent.AddSymbol(instance);
            return instance;
        }

        // Async wrappers for Resolve
        public static Task<IBaseSymbol> ResolveAsync(this IBaseSymbol symbol, string name, bool localOnly = false)
        {
            return symbol.Resolve(name, localOnly);
        }

        public static Task<IBaseSymbol> ResolveAsync(this ISymbolTable table, string name, bool localOnly = false)
        {
            return table.Resolve(name, localOnly);
        }

        // GetAllSymbolsAsync for symbol tables without needing SymbolConstructor param in tests
        public static async Task<T[]> GetAllSymbolsAsync<T>(this ISymbolTable table) where T : IBaseSymbol
        {
            var list = await table.GetAllSymbols<T, object>((args) => (T)args[0], false);
            return list.ToArray();
        }

        public static async Task<T[]> GetAllSymbolsAsync<T>(this IScopedSymbol scope) where T : IBaseSymbol
        {
            var list = await scope.GetAllSymbols<T, object>((args) => (T)args[0], false);
            return list.ToArray();
        }

        // GetAllNestedSymbolsAsync
        public static async Task<IBaseSymbol[]> GetAllNestedSymbolsAsync(this IScopedSymbol scope)
        {
            var list = await scope.GetAllNestedSymbols();
            return list.ToArray();
        }

        // GetNestedSymbolsOfTypeAsync without constructor argument
        public static async Task<T[]> GetNestedSymbolsOfTypeAsync<T>(this IScopedSymbol scope) where T : IBaseSymbol
        {
            var list = await scope.GetNestedSymbolsOfType<T, object>((args) => (T)args[0]);
            return list.ToArray();
        }

        // Sync variant
        public static T[] GetNestedSymbolsOfTypeSync<T>(this IScopedSymbol scope) where T : IBaseSymbol
        {
            var list = scope.GetNestedSymbolsOfTypeSync<T, object>((args) => (T)args[0]);
            return list.ToArray();
        }

        // GetNestedSymbolsOfTypeAsync on SymbolTable too (IScopedSymbol covers it but include for convenience)
        public static Task<T[]> GetNestedSymbolsOfTypeAsync<T>(this SymbolTable table) where T : IBaseSymbol
        {
            return GetNestedSymbolsOfTypeAsync<T>((IScopedSymbol)table);
        }

        // SymbolWithContextAsync wrappers
        public static Task<IBaseSymbol> SymbolWithContextAsync(this ISymbolTable table, IParseTree context)
        {
            return table.SymbolWithContext(context);
        }

        public static Task<IBaseSymbol> SymbolWithContextAsync(this IScopedSymbol scope, IParseTree context)
        {
            // scoped symbol doesn't have symbolWithContext; delegate to its symbol table.
            return scope.SymbolTable?.SymbolWithContext(context) ?? Task.FromResult<IBaseSymbol>(null);
        }
    }
}
