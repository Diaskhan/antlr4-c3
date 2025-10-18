namespace Antlr4CodeCompletion // Replace with your namespace
{
    public interface ParseTree { }

    public interface IBaseSymbol
    {
        string Name { get; }
        ParseTree Context { get; set; }
        HashSet<Modifier> Modifiers { get; }
        MemberVisibility Visibility { get; set; }
        IScopedSymbol Parent { get; }
        IBaseSymbol FirstSibling { get; }
        IBaseSymbol PreviousSibling { get; }
        IBaseSymbol NextSibling { get; }
        IBaseSymbol LastSibling { get; }
        IBaseSymbol Next { get; }
        IBaseSymbol Root { get; }
        ISymbolTable SymbolTable { get; }
        IBaseSymbol[] SymbolPath { get; }

        void SetParent(IScopedSymbol parent);
        void RemoveFromParent();
        Task<IBaseSymbol> Resolve(string name, bool localOnly = false);
        IBaseSymbol ResolveSync(string name, bool localOnly = false);
        T GetParentOfType<T>(Type t) where T : IBaseSymbol;
        string QualifiedName(string separator = ".", bool full = false, bool includeAnonymous = false);
    }

    public class BaseSymbol : IBaseSymbol
    {
        public string Name { get; }
        public ParseTree Context { get; set; }
        public HashSet<Modifier> Modifiers { get; } = new HashSet<Modifier>();
        public MemberVisibility Visibility { get; set; } = MemberVisibility.Unknown;
        private IScopedSymbol _parent;

        public BaseSymbol(string name = "")
        {
            Name = name;
        }

        public IScopedSymbol Parent => _parent;

        public IBaseSymbol FirstSibling => _parent?.FirstChild;

        public IBaseSymbol PreviousSibling => _parent?.PreviousSiblingOf(this);

        public IBaseSymbol NextSibling => _parent?.NextSiblingOf(this);

        public IBaseSymbol LastSibling => _parent?.LastChild;

        public IBaseSymbol Next => _parent?.NextOf(this);

        public IBaseSymbol Root
        {
            get
            {
                var run = _parent;
                while (run != null)
                {
                    if (run.Parent == null || IsSymbolTable(run.Parent))
                        return run;
                    run = run.Parent;
                }
                return null;
            }
        }

        public ISymbolTable SymbolTable
        {
            get
            {
                if (IsSymbolTable(this))
                    return (ISymbolTable)this;
                var run = _parent;
                while (run != null)
                {
                    if (IsSymbolTable(run))
                        return (ISymbolTable)run;
                    run = run.Parent;
                }
                return null;
            }
        }

        public IBaseSymbol[] SymbolPath
        {
            get
            {
                var result = new List<IBaseSymbol>();
                var run = (IBaseSymbol)this;
                while (run != null)
                {
                    result.Add(run);
                    run = run.Parent;
                }
                return result.ToArray();
            }
        }

        public void SetParent(IScopedSymbol parent)
        {
            _parent = parent;
        }

        public void RemoveFromParent()
        {
            _parent?.RemoveSymbol(this);
            _parent = null;
        }

        public virtual Task<IBaseSymbol> Resolve(string name, bool localOnly = false)
        {
            return _parent?.Resolve(name, localOnly) ?? Task.FromResult<IBaseSymbol>(null);
        }

        public virtual IBaseSymbol ResolveSync(string name, bool localOnly = false)
        {
            return _parent?.ResolveSync(name, localOnly);
        }

        public T GetParentOfType<T>(Type t) where T : IBaseSymbol
        {
            var run = _parent;
            while (run != null)
            {
                if (run.GetType() == t)
                    return (T)run;
                run = run.Parent;
            }
            return default;
        }

        public string QualifiedName(string separator = ".", bool full = false, bool includeAnonymous = false)
        {
            string result = string.IsNullOrEmpty(Name) ? (includeAnonymous ? "<anonymous>" : "") : Name;
            var run = _parent;
            while (run != null)
            {
                string runName = string.IsNullOrEmpty(run.Name) ? (includeAnonymous ? "<anonymous>" : "") : run.Name;
                if (includeAnonymous || !string.IsNullOrEmpty(run.Name))
                    result = runName + separator + result;
                if (!full || run.Parent == null)
                    break;
                run = run.Parent;
            }
            return result;
        }

        private bool IsSymbolTable(object candidate)
        {
            return candidate is ISymbolTable table && table.Options != null;
        }
    }

    public delegate T SymbolConstructor<T, Args>(params Args[] args) where T : IBaseSymbol;
}