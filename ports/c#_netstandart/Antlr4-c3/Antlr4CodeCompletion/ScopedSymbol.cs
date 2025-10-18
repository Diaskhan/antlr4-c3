using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Antlr4.Runtime.Tree;

namespace Antlr4CodeCompletion // Replace with your namespace
{

    public interface IScopedSymbol : IBaseSymbol
    {
        Task<List<IScopedSymbol>> DirectScopes { get; }
        IBaseSymbol[] Children { get; }
        IBaseSymbol FirstChild { get; }
        IBaseSymbol LastChild { get; }

        void Clear();
        void AddSymbol(IBaseSymbol symbol);
        void RemoveSymbol(IBaseSymbol symbol);
        Task<List<T>> GetNestedSymbolsOfType<T, Args>(SymbolConstructor<T, Args> t) where T : IBaseSymbol;
        List<T> GetNestedSymbolsOfTypeSync<T, Args>(SymbolConstructor<T, Args> t) where T : IBaseSymbol;
        Task<List<IBaseSymbol>> GetAllNestedSymbols(string name = null);
        List<IBaseSymbol> GetAllNestedSymbolsSync(string name = null);
        Task<List<T>> GetSymbolsOfType<T, Args>(SymbolConstructor<T, Args> t) where T : IBaseSymbol;
        Task<List<T>> GetAllSymbols<T, Args>(SymbolConstructor<T, Args> t, bool localOnly = false) where T : IBaseSymbol;
        List<T> GetAllSymbolsSync<T, Args>(SymbolConstructor<T, Args> t, bool localOnly = false) where T : IBaseSymbol;
        new Task<IBaseSymbol> Resolve(string name, bool localOnly = false);
        new IBaseSymbol ResolveSync(string name, bool localOnly = false);
        IBaseSymbol SymbolFromPath(string path, string separator = ".");
        int IndexOfChild(IBaseSymbol child);
        IBaseSymbol NextSiblingOf(IBaseSymbol child);
        IBaseSymbol PreviousSiblingOf(IBaseSymbol child);
        IBaseSymbol NextOf(IBaseSymbol child);
    }

    public class ScopedSymbol : BaseSymbol, IScopedSymbol
    {
        private readonly List<IBaseSymbol> _children = new List<IBaseSymbol>();
        private readonly Dictionary<string, int> _names = new Dictionary<string, int>();

        public ScopedSymbol(string name = "") : base(name) { }

        public Task<List<IScopedSymbol>> DirectScopes => GetSymbolsOfType<IScopedSymbol, object>(x => (IScopedSymbol)x[0]);

        public IBaseSymbol[] Children => _children.ToArray();

        public IBaseSymbol FirstChild => _children.Count > 0 ? _children[0] : null;

        public IBaseSymbol LastChild => _children.Count > 0 ? _children[_children.Count - 1] : null;

        public void Clear()
        {
            _children.Clear();
            _names.Clear();
        }

        public void AddSymbol(IBaseSymbol symbol)
        {
            symbol.RemoveFromParent();
            var symbolTable = SymbolTable;
            var count = _names.ContainsKey(symbol.Name) ? _names[symbol.Name] : 0;
            if (symbolTable?.Options?.AllowDuplicateSymbols != true)
            {
                if (count != 0)
                    throw new DuplicateSymbolError($"Attempt to add duplicate symbol '{(symbol.Name ?? "<anonymous>")}'");
                if (_children.Contains(symbol))
                    throw new DuplicateSymbolError($"Attempt to add duplicate symbol '{(symbol.Name ?? "<anonymous>")}'");
                _names[symbol.Name] = 1;
            }
            else
            {
                _names[symbol.Name] = count + 1;
            }
            _children.Add(symbol);
            symbol.SetParent(this);
        }

        public void RemoveSymbol(IBaseSymbol symbol)
        {
            int index = _children.IndexOf(symbol);
            if (index > -1)
            {
                _children.RemoveAt(index);
                symbol.SetParent(null);
                if (_names.TryGetValue(symbol.Name, out int count))
                {
                    if (count == 1)
                        _names.Remove(symbol.Name);
                    else
                        _names[symbol.Name] = count - 1;
                }
            }
        }

        public async Task<List<T>> GetNestedSymbolsOfType<T, Args>(SymbolConstructor<T, Args> t) where T : IBaseSymbol
        {
            var result = new List<T>();
            var childTasks = new List<Task<List<T>>>();
            foreach (var child in _children)
            {
                if (child.GetType() == typeof(T))
                    result.Add((T)child);
                if (child is IScopedSymbol scoped)
                    childTasks.Add(scoped.GetNestedSymbolsOfType<T, Args>(t));
            }
            var childSymbols = await Task.WhenAll(childTasks);
            foreach (var entry in childSymbols)
                result.AddRange(entry);
            return result;
        }

        public List<T> GetNestedSymbolsOfTypeSync<T, Args>(SymbolConstructor<T, Args> t) where T : IBaseSymbol
        {
            var result = new List<T>();
            foreach (var child in _children)
            {
                if (child.GetType() == typeof(T))
                    result.Add((T)child);
                if (child is IScopedSymbol scoped)
                    result.AddRange(scoped.GetNestedSymbolsOfTypeSync<T, Args>(t));
            }
            return result;
        }

        public async Task<List<IBaseSymbol>> GetAllNestedSymbols(string name = null)
        {
            var result = new List<IBaseSymbol>();
            var childTasks = new List<Task<List<IBaseSymbol>>>();
            foreach (var child in _children)
            {
                if (string.IsNullOrEmpty(name) || child.Name == name)
                    result.Add(child);
                if (child is IScopedSymbol scoped)
                    childTasks.Add(scoped.GetAllNestedSymbols(name));
            }
            var childSymbols = await Task.WhenAll(childTasks);
            foreach (var entry in childSymbols)
                result.AddRange(entry);
            return result;
        }

        public List<IBaseSymbol> GetAllNestedSymbolsSync(string name = null)
        {
            var result = new List<IBaseSymbol>();
            foreach (var child in _children)
            {
                if (string.IsNullOrEmpty(name) || child.Name == name)
                    result.Add(child);
                if (child is IScopedSymbol scoped)
                    result.AddRange(scoped.GetAllNestedSymbolsSync(name));
            }
            return result;
        }

        public Task<List<T>> GetSymbolsOfType<T, Args>(SymbolConstructor<T, Args> t) where T : IBaseSymbol
        {
            var result = new List<T>();
            foreach (var child in _children)
                if (child.GetType() == typeof(T))
                    result.Add((T)child);
            return Task.FromResult(result);
        }

        public async Task<List<T>> GetAllSymbols<T, Args>(SymbolConstructor<T, Args> t, bool localOnly = false) where T : IBaseSymbol
        {
            var result = new List<T>();
            foreach (var child in _children)
            {
                if (child.GetType() == typeof(T))
                    result.Add((T)child);
                if (IsNamespace(child))
                {
                    var childSymbols = await ((IScopedSymbol)child).GetAllSymbols<T, Args>(t, true);
                    result.AddRange(childSymbols);
                }
            }
            if (!localOnly && Parent != null)
            {
                var parentSymbols = await Parent.GetAllSymbols<T, Args>(t, true);
                result.AddRange(parentSymbols);
            }
            return result;
        }

        public List<T> GetAllSymbolsSync<T, Args>(SymbolConstructor<T, Args> t, bool localOnly = false) where T : IBaseSymbol
        {
            var result = new List<T>();
            foreach (var child in _children)
            {
                if (child.GetType() == typeof(T))
                    result.Add((T)child);
                if (IsNamespace(child))
                {
                    var childSymbols = ((IScopedSymbol)child).GetAllSymbolsSync<T, Args>(t, true);
                    result.AddRange(childSymbols);
                }
            }
            if (!localOnly && Parent != null)
            {
                var parentSymbols = Parent.GetAllSymbolsSync<T, Args>(t, true);
                result.AddRange(parentSymbols);
            }
            return result;
        }

        public override async Task<IBaseSymbol> Resolve(string name, bool localOnly = false)
        {
            foreach (var child in _children)
                if (child.Name == name)
                    return child;
            if (!localOnly && Parent != null)
                return await Parent.Resolve(name, false);
            return null;
        }

        public override IBaseSymbol ResolveSync(string name, bool localOnly = false)
        {
            foreach (var child in _children)
                if (child.Name == name)
                    return child;
            if (!localOnly && Parent != null)
                return Parent.ResolveSync(name, false);
            return null;
        }

        public IBaseSymbol SymbolFromPath(string path, string separator = ".")
        {
            var elements = path.Split(separator);
            int index = elements[0] == Name || string.IsNullOrEmpty(elements[0]) ? 1 : 0;
            IBaseSymbol result = this;
            while (index < elements.Length)
            {
                if (result is not IScopedSymbol scoped)
                    return null;
                result = scoped.Children.FirstOrDefault(c => c.Name == elements[index]);
                if (result == null)
                    return null;
                index++;
            }
            return result;
        }

        public int IndexOfChild(IBaseSymbol child)
        {
            return _children.IndexOf(child);
        }

        public IBaseSymbol NextSiblingOf(IBaseSymbol child)
        {
            int index = IndexOfChild(child);
            return index == -1 || index >= _children.Count - 1 ? null : _children[index + 1];
        }

        public IBaseSymbol PreviousSiblingOf(IBaseSymbol child)
        {
            int index = IndexOfChild(child);
            return index < 1 ? null : _children[index - 1];
        }

        public IBaseSymbol NextOf(IBaseSymbol child)
        {
            if (child.Parent != this)
                return child.Parent?.NextOf(child);
            if (child is IScopedSymbol scoped && scoped.Children.Length > 0)
                return scoped.Children[0];
            var sibling = NextSiblingOf(child);
            if (sibling != null)
                return sibling;
            return Parent?.NextOf(this);
        }

        private bool IsNamespace(object candidate)
        {
            return candidate is INamespaceSymbol ns && ns.IsInline != null && ns.Attributes != null;
        }

        // Convenience async wrappers used by tests
        public async Task<IBaseSymbol[]> GetAllNestedSymbolsAsync()
        {
            var list = await GetAllNestedSymbols();
            return list.ToArray();
        }

        public async Task<T[]> GetNestedSymbolsOfTypeAsync<T>() where T : IBaseSymbol
        {
            var list = await GetNestedSymbolsOfType<T, object>((args) => (T)args[0]);
            return list.ToArray();
        }

        // Non-suffixed variant used by some tests
        public Task<T[]> GetNestedSymbolsOfType<T>() where T : IBaseSymbol
        {
            return GetNestedSymbolsOfTypeAsync<T>();
        }

        public async Task<T[]> GetSymbolsOfTypeAsync<T>() where T : IBaseSymbol
        {
            var list = await GetSymbolsOfType<T, object>((args) => (T)args[0]);
            return list.ToArray();
        }
    }
}