/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using Antlr4.Runtime.Tree;
using System.Text;

namespace Antlr4CodeCompletion;

/// <summary>
/// The root of the symbol table class hierarchy. A symbol can be any manageable entity
/// (like a block), not just things like variables or classes.
/// </summary>
/// <remarks>
/// We are using a class hierarchy here, instead of an enum or similar, to allow for easy extension.
/// Certain symbols can provide additional APIs for simpler access to their sub-elements, if needed.
/// </remarks>
public class BaseSymbol
{
    private IScopedSymbol? _parent;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseSymbol"/> class.
    /// </summary>
    /// <param name="name">The name of the symbol. Can be empty for anonymous symbols.</param>
    public BaseSymbol(string name = "")
    {
        Name = name;
    }

    /// <summary>
    /// Gets or sets the name of the symbol. Can be empty if anonymous.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the reference to the parse tree which contains this symbol.
    /// </summary>
    public IParseTree? Context { get; set; }

    /// <summary>
    /// Gets the set of modifiers for this symbol.
    /// </summary>
    public ISet<Modifier> Modifiers { get; } = new HashSet<Modifier>();

    /// <summary>
    /// Gets or sets the visibility of this symbol.
    /// </summary>
    public MemberVisibility Visibility { get; set; } = MemberVisibility.Unknown;

    /// <summary>
    /// Gets the parent scope of this symbol.
    /// </summary>
    public IScopedSymbol? Parent => _parent;

    /// <summary>
    /// Gets the first sibling of this symbol in its scope.
    /// </summary>
    public BaseSymbol? FirstSibling => _parent?.FirstChild;

    /// <summary>
    /// Gets the symbol before this symbol in its scope.
    /// </summary>
    public BaseSymbol? PreviousSibling => _parent?.PreviousSiblingOf(this);

    /// <summary>
    /// Gets the symbol following this symbol in its scope.
    /// </summary>
    public BaseSymbol? NextSibling => _parent?.NextSiblingOf(this);

    /// <summary>
    /// Gets the last sibling of this symbol in its scope.
    /// </summary>
    public BaseSymbol? LastSibling => _parent?.LastChild;

    /// <summary>
    /// Gets the next symbol in definition order, regardless of the scope.
    /// </summary>
    public BaseSymbol? Next => _parent?.NextOf(this);

    /// <summary>
    /// Gets the outermost entity (below the symbol table) that holds this symbol.
    /// </summary>
    public BaseSymbol? Root
    {
        get
        {
            IScopedSymbol? run = _parent;
            while (run != null)
            {
                if (run.Parent == null || run.Parent is ISymbolTable)
                {
                    return run;
                }
                run = run.Parent;
            }

            return run;
        }
    }

    /// <summary>
    /// Gets the symbol table this symbol belongs to, if any.
    /// </summary>
    public ISymbolTable? SymbolTable
    {
        get
        {
            if (this is ISymbolTable table)
            {
                return table;
            }

            IScopedSymbol? run = _parent;
            while (run != null)
            {
                if (run is ISymbolTable symbolTable)
                {
                    return symbolTable;
                }
                run = run.Parent;
            }

            return null;
        }
    }

    /// <summary>
    /// Gets the list of symbols from this one up to the root.
    /// </summary>
    public IReadOnlyList<BaseSymbol> SymbolPath
    {
        get
        {
            var result = new List<BaseSymbol>();
            BaseSymbol? run = this;
            while (run != null)
            {
                result.Add(run);
                run = run.Parent;
            }
            return result;
        }
    }

    /// <summary>
    /// Sets the parent of this symbol.
    /// This is an internal method and should be used with caution.
    /// </summary>
    /// <param name="parent">The new parent to use.</param>
    public void SetParent(IScopedSymbol? parent)
    {
        _parent = parent;
    }

    /// <summary>
    /// Removes this symbol from its parent scope.
    /// </summary>
    public void RemoveFromParent()
    {
        _parent?.RemoveSymbol(this);
        _parent = null;
    }

    /// <summary>
    /// Asynchronously looks up a symbol with a given name in a bottom-up manner.
    /// </summary>
    /// <param name="name">The name of the symbol to find.</param>
    /// <param name="localOnly">If true, only direct child symbols are considered.</param>
    /// <returns>A task that resolves to the first symbol found, or null if no symbol is found.</returns>
    public Task<BaseSymbol?> ResolveAsync(string name, bool localOnly = false)
    {
        return _parent?.ResolveAsync(name, localOnly) ?? Task.FromResult<BaseSymbol?>(null);
    }

    /// <summary>
    /// Synchronously looks up a symbol with a given name in a bottom-up manner.
    /// </summary>
    /// <param name="name">The name of the symbol to find.</param>
    /// <param name="localOnly">If true, only direct child symbols are considered.</param>
    /// <returns>The first symbol found, or null if no symbol is found.</returns>
    public BaseSymbol? ResolveSync(string name, bool localOnly = false)
    {
        return _parent?.ResolveSync(name, localOnly);
    }

    /// <summary>
    /// Gets the next enclosing parent of a given type.
    /// </summary>
    /// <typeparam name="T">The type of the parent to find.</typeparam>
    /// <returns>The parent of the specified type, or null if no such parent exists.</returns>
    public T? GetParentOfType<T>() where T : BaseSymbol
    {
        IScopedSymbol? run = _parent;
        while (run != null)
        {
            if (run is T typedParent)
            {
                return typedParent;
            }
            run = run.Parent;
        }

        return null;
    }

    /// <summary>
    /// Creates a qualified identifier from this symbol and its parents.
    /// </summary>
    /// <param name="separator">The string to use between name parts.</param>
    /// <param name="full">True to create a fully qualified name, false for a name limited to the parent scope.</param>
    /// <param name="includeAnonymous">True to use a placeholder for anonymous scopes.</param>
    /// <returns>The constructed qualified identifier.</returns>
    public string GetQualifiedName(string separator = ".", bool full = false, bool includeAnonymous = false)
    {
        if (!includeAnonymous && string.IsNullOrEmpty(Name))
        {
            return string.Empty;
        }

        var result = new StringBuilder(string.IsNullOrEmpty(Name) ? "<anonymous>" : Name);
        IScopedSymbol? run = _parent;
        while (run != null)
        {
            if (includeAnonymous || !string.IsNullOrEmpty(run.Name))
            {
                result.Insert(0, (string.IsNullOrEmpty(run.Name) ? "<anonymous>" : run.Name) + separator);
            }

            if (!full || run.Parent == null)
            {
                break;
            }
            run = run.Parent;
        }

        return result.ToString();
    }
}