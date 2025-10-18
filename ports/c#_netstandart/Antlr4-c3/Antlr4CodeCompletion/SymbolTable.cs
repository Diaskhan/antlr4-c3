using Antlr4CodeCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Antlr4CodeCompletion // Replace with your namespace
{
    
    public interface ISymbolTable : IScopedSymbol
    {
        ISymbolTableOptions Options { get; }
        (int dependencyCount, int symbolCount) Info { get; }
        void Clear();
        void AddDependencies(params ISymbolTable[] tables);
        void RemoveDependency(ISymbolTable table);
        T AddNewSymbolOfType<T, Args>(SymbolConstructor<T, Args> t, IScopedSymbol parent, params Args[] args) where T : IBaseSymbol;
        Task<INamespaceSymbol> AddNewNamespaceFromPath(IScopedSymbol parent, string path, string delimiter = ".");
        INamespaceSymbol AddNewNamespaceFromPathSync(IScopedSymbol parent, string path, string delimiter = ".");
        Task<List<T>> GetAllSymbols<T, Args>(SymbolConstructor<T, Args> t, bool localOnly = false) where T : IBaseSymbol;
        List<T> GetAllSymbolsSync<T, Args>(SymbolConstructor<T, Args> t, bool localOnly = false) where T : IBaseSymbol;
        Task<IBaseSymbol> SymbolWithContext(ParseTree context);
        IBaseSymbol SymbolWithContextSync(ParseTree context);
        new Task<IBaseSymbol> Resolve(string name, bool localOnly = false);
        new IBaseSymbol ResolveSync(string name, bool localOnly = false);
    }

    public class SymbolTable : ScopedSymbol, ISymbolTable
    {
        private readonly HashSet<ISymbolTable> _dependencies = new HashSet<ISymbolTable>();

        public SymbolTable(string name, ISymbolTableOptions options) : base(name)
        {
            Options = options;
        }

        public ISymbolTableOptions Options { get; }

        public (int dependencyCount, int symbolCount) Info => (_dependencies.Count, Children.Length);

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
            var result = t(args);
            var targetParent = parent ?? this;
            targetParent.AddSymbol(result);
            return result;
        }

        public async Task<INamespaceSymbol> AddNewNamespaceFromPath(IScopedSymbol parent, string path, string delimiter = ".")
        {
            var parts = path.Split(delimiter);
            int i = 0;
            var currentParent = parent ?? this;
            while (i < parts.Length - 1)
            {
                var namespaceSymbol = await currentParent.Resolve(parts[i], true) as INamespaceSymbol;
                if (namespaceSymbol == null)
                    namespaceSymbol = AddNewSymbolOfType<INamespaceSymbol, object>(x => (INamespaceSymbol)x[0], currentParent, parts[i]);
                currentParent = namespaceSymbol;
                i++;
            }
            return AddNewSymbolOfType<INamespaceSymbol, object>(x => (INamespaceSymbol)x[0], currentParent, parts[parts.Length - 1]);
        }

        public INamespaceSymbol AddNewNamespaceFromPathSync(IScopedSymbol parent, string path, string delimiter = ".")
        {
            var parts = path.Split(delimiter);
            int i = 0;
            var currentParent = parent ?? this;
            while (i < parts.Length - 1)
            {
                var namespaceSymbol = currentParent.ResolveSync(parts[i], true) as INamespaceSymbol;
                if (namespaceSymbol == null)
                    namespaceSymbol = AddNewSymbolOfType<INamespaceSymbol, object>(x => (INamespaceSymbol)x[0], currentParent, parts[i]);
                currentParent = namespaceSymbol;
                i++;
            }
            return AddNewSymbolOfType<INamespaceSymbol, object>(x => (INamespaceSymbol)x[0], currentParent, parts[parts.Length - 1]);
        }

        public async Task<List<T>> GetAllSymbols<T, Args>(SymbolConstructor<T, Args> t, bool localOnly = false) where T : IBaseSymbol
        {
            var result = await base.GetAllSymbols<T, Args>(t, localOnly);
            if (!localOnly)
            {
                var dependencyResults = await Task.WhenAll(_dependencies.Select(d => d.GetAllSymbols<T, Args>(t, false)));
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

        public async Task<IBaseSymbol> SymbolWithContext(ParseTree context)
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

            var symbols = await GetAllSymbols<IBaseSymbol, object>(x => (IBaseSymbol)x[0]);
            foreach (var symbol in symbols)
            {
                var result = FindRecursive(symbol);
                if (result != null)
                    return result;
            }

            foreach (var dependency in _dependencies)
            {
                symbols = await dependency.GetAllSymbols<IBaseSymbol, object>(x => (IBaseSymbol)x[0]);
                foreach (var symbol in symbols)
                {
                    var result = FindRecursive(symbol);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

        public IBaseSymbol SymbolWithContextSync(ParseTree context)
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

            var symbols = GetAllSymbolsSync<IBaseSymbol, object>(x => (IBaseSymbol)x[0]);
            foreach (var symbol in symbols)
            {
                var result = FindRecursive(symbol);
                if (result != null)
                    return result;
            }

            foreach (var dependency in _dependencies)
            {
                symbols = dependency.GetAllSymbolsSync<IBaseSymbol, object>(x => (IBaseSymbol)x[0]);
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
    }
}