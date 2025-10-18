using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Antlr4.Runtime.Tree;

namespace Antlr4CodeCompletion // Replace with your namespace
{
    public interface ISymbolTable : IScopedSymbol
    {
        ISymbolTableOptions Options { get; }
        SymbolTableInfo Info { get; }
        void Clear();
        void AddDependencies(params ISymbolTable[] tables);
        void RemoveDependency(ISymbolTable table);
        T AddNewSymbolOfType<T, Args>(SymbolConstructor<T, Args> t, IScopedSymbol parent, params Args[] args) where T : IBaseSymbol;
        Task<INamespaceSymbol> AddNewNamespaceFromPath(IScopedSymbol parent, string path, string delimiter = ".");
        INamespaceSymbol AddNewNamespaceFromPathSync(IScopedSymbol parent, string path, string delimiter = ".");
        Task<List<T>> GetAllSymbols<T, Args>(SymbolConstructor<T, Args> t, bool localOnly = false) where T : IBaseSymbol;
        List<T> GetAllSymbolsSync<T, Args>(SymbolConstructor<T, Args> t, bool localOnly = false) where T : IBaseSymbol;
        Task<IBaseSymbol> SymbolWithContext(IParseTree context);
        IBaseSymbol SymbolWithContextSync(IParseTree context);
        new Task<IBaseSymbol> Resolve(string name, bool localOnly = false);
        new IBaseSymbol ResolveSync(string name, bool localOnly = false);
    }

    public struct SymbolTableInfo
    {
        public int DependencyCount { get; set; }
        public int SymbolCount { get; set; }
    }

    public class SymbolTable : ScopedSymbol, ISymbolTable
    {
        private readonly HashSet<ISymbolTable> _dependencies = new HashSet<ISymbolTable>();

        public SymbolTable(string name, ISymbolTableOptions options) : base(name)
        {
            Options = options;
        }

        public ISymbolTableOptions Options { get; }

        public SymbolTableInfo Info => new SymbolTableInfo { DependencyCount = _dependencies.Count, SymbolCount = Children.Length };

        public void Clear()
        {
            base.Clear();
            _dependencies.Clear();
        }

        public void AddDependencies(params ISymbolTable[] tables)
        {
            foreach (var table in tables)
                _dependencies.Add(table);
        }

        public void RemoveDependency(ISymbolTable table)
        {
            _dependencies.Remove(table);
        }

        public T AddNewSymbolOfType<T, Args>(SymbolConstructor<T, Args> t, IScopedSymbol parent, params Args[] args) where T : IBaseSymbol
        {
            // Use the provided constructor delegate to create the instance
            var result = t(args);
            var targetParent = parent ?? (IScopedSymbol)this;
            targetParent.AddSymbol(result);
            return result;
        }

        // Convenience overload matching original tests: caller provides only T and args, constructor created via Activator
        public T AddNewSymbolOfType<T>(IScopedSymbol parent, params object[] args) where T : IBaseSymbol
        {
            var instance = (T)Activator.CreateInstance(typeof(T), args ?? Array.Empty<object>());
            var targetParent = parent ?? (IScopedSymbol)this;
            targetParent.AddSymbol(instance);
            return instance;
        }

        public async Task<INamespaceSymbol> AddNewNamespaceFromPath(IScopedSymbol parent, string path, string delimiter = ".")
        {
            var parts = path.Split(delimiter);
            int i = 0;
            IScopedSymbol currentParent = parent ?? this;
            while (i < parts.Length - 1)
            {
                var namespaceSymbol = await currentParent.Resolve(parts[i], true) as INamespaceSymbol;
                if (namespaceSymbol == null)
                    namespaceSymbol = AddNewSymbolOfType<INamespaceSymbol, string>((args) => (INamespaceSymbol)new NamespaceSymbol(args.Length > 0 ? args[0] as string : ""), currentParent, parts[i]);
                currentParent = namespaceSymbol;
                i++;
            }
            return AddNewSymbolOfType<INamespaceSymbol, string>((args) => (INamespaceSymbol)new NamespaceSymbol(args.Length > 0 ? args[0] as string : ""), currentParent, parts[parts.Length - 1]);
        }

        public INamespaceSymbol AddNewNamespaceFromPathSync(IScopedSymbol parent, string path, string delimiter = ".")
        {
            var parts = path.Split(delimiter);
            int i = 0;
            IScopedSymbol currentParent = parent ?? this;
            while (i < parts.Length - 1)
            {
                var namespaceSymbol = currentParent.ResolveSync(parts[i], true) as INamespaceSymbol;
                if (namespaceSymbol == null)
                    namespaceSymbol = AddNewSymbolOfType<INamespaceSymbol, string>((args) => (INamespaceSymbol)new NamespaceSymbol(args.Length > 0 ? args[0] as string : ""), currentParent, parts[i]);
                currentParent = namespaceSymbol;
                i++;
            }
            return AddNewSymbolOfType<INamespaceSymbol, string>((args) => (INamespaceSymbol)new NamespaceSymbol(args.Length > 0 ? args[0] as string : ""), currentParent, parts[parts.Length - 1]);
        }

        public async Task<List<T>> GetAllSymbols<T, Args>(SymbolConstructor<T, Args> t, bool localOnly = false) where T : IBaseSymbol
        {
            var result = await base.GetAllSymbols<T, Args>(t, localOnly);
            if (!localOnly)
            {
                var dependencyTasks = _dependencies.Select(d => d.GetAllSymbols<T, Args>(t, false));
                var dependencyResults = await Task.WhenAll(dependencyTasks);
                foreach (var value in dependencyResults)
                    result.AddRange(value);
            }
            return result;
        }

        public List<T> GetAllSymbolsSync<T, Args>(SymbolConstructor<T, Args> t, bool localOnly = false) where T : IBaseSymbol
        {
            var result = base.GetAllSymbolsSync<T, Args>(t, localOnly);
            if (!localOnly)
            {
                foreach (var dependency in _dependencies)
                    result.AddRange(dependency.GetAllSymbolsSync<T, Args>(t, false));
            }
            return result;
        }

        // Convenience async method used by tests
        public async Task<T[]> GetAllSymbolsAsync<T>() where T : IBaseSymbol
        {
            var list = await GetAllSymbols<T, object>((args) => (T)args[0], false);
            return list.ToArray();
        }

        public async Task<IBaseSymbol> SymbolWithContext(IParseTree context)
        {
            IBaseSymbol FindRecursive(IBaseSymbol symbol)
            {
                if (symbol.Context == context)
                    return symbol;
                if (symbol is IScopedSymbol scoped)
                {
                    foreach (var child in scoped.Children)
                    {
                        var result = FindRecursive(child);
                        if (result != null)
                            return result;
                    }
                }
                return null;
            }

            var symbols = await GetAllSymbols<IBaseSymbol, object>((args) => (IBaseSymbol)args[0]);
            foreach (var symbol in symbols)
            {
                var result = FindRecursive(symbol);
                if (result != null)
                    return result;
            }

            foreach (var dependency in _dependencies)
            {
                symbols = await dependency.GetAllSymbols<IBaseSymbol, object>((args) => (IBaseSymbol)args[0]);
                foreach (var symbol in symbols)
                {
                    var result = FindRecursive(symbol);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

        public IBaseSymbol SymbolWithContextSync(IParseTree context)
        {
            IBaseSymbol FindRecursive(IBaseSymbol symbol)
            {
                if (symbol.Context == context)
                    return symbol;
                if (symbol is IScopedSymbol scoped)
                {
                    foreach (var child in scoped.Children)
                    {
                        var result = FindRecursive(child);
                        if (result != null)
                            return result;
                    }
                }
                return null;
            }

            var symbols = GetAllSymbolsSync<IBaseSymbol, object>((args) => (IBaseSymbol)args[0]);
            foreach (var symbol in symbols)
            {
                var result = FindRecursive(symbol);
                if (result != null)
                    return result;
            }

            foreach (var dependency in _dependencies)
            {
                symbols = dependency.GetAllSymbolsSync<IBaseSymbol, object>((args) => (IBaseSymbol)args[0]);
                foreach (var symbol in symbols)
                {
                    var result = FindRecursive(symbol);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

        public override async Task<IBaseSymbol> Resolve(string name, bool localOnly = false)
        {
            var result = await base.Resolve(name, localOnly);
            if (result == null && !localOnly)
            {
                foreach (var dependency in _dependencies)
                {
                    result = await dependency.Resolve(name, false);
                    if (result != null)
                        return result;
                }
            }
            return result;
        }

        public override IBaseSymbol ResolveSync(string name, bool localOnly = false)
        {
            var result = base.ResolveSync(name, localOnly);
            if (result == null && !localOnly)
            {
                foreach (var dependency in _dependencies)
                {
                    result = dependency.ResolveSync(name, false);
                    if (result != null)
                        return result;
                }
            }
            return result;
        }

        // Async convenience
        public Task<IBaseSymbol> ResolveAsync(string name, bool localOnly = false)
        {
            return Resolve(name, localOnly);
        }
    }
}