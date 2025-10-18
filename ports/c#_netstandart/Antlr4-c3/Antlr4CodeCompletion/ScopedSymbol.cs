/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Antlr4CodeCompletion;

/// <summary>
/// Defines a symbol that can contain other symbols.
/// </summary>
public interface IScopedSymbol : BaseSymbol
{
    /// <summary>
    /// Gets all direct child symbols with a scope (e.g., classes in a module).
    /// </summary>
    Task<IList<IScopedSymbol>> DirectScopesAsync { get; }

    IList<BaseSymbol> Children { get; }
    BaseSymbol? FirstChild { get; }
    BaseSymbol? LastChild { get; }

    void Clear();

    /// <summary>
    /// Adds the given symbol to this scope. If it already belongs to a different scope,
    /// it is removed from that scope before being added to this one.
    /// </summary>
    /// <param name="symbol">The symbol to add as a child.</param>
    void AddSymbol(BaseSymbol symbol);

    /// <summary>
    /// Removes the given symbol from this scope, if it exists.
    /// </summary>
    /// <param name="symbol">The symbol to remove.</param>
    void RemoveSymbol(BaseSymbol symbol);

    /// <summary>
    /// Asynchronously retrieves all nested child symbols of a given type from this symbol.
    /// </summary>
    /// <typeparam name="T">The type of the symbols to return.</typeparam>
    /// <returns>A task that resolves to a list of all nested children of the given type.</returns>
    Task<IList<T>> GetNestedSymbolsOfTypeAsync<T>() where T : BaseSymbol;

    /// <summary>
    /// Synchronously retrieves all nested child symbols of a given type from this symbol.
    /// </summary>
    /// <typeparam name="T">The type of the symbols to return.</typeparam>
    /// <returns>A list of all nested children of the given type.</returns>
    IList<T> GetNestedSymbolsOfType<T>() where T : BaseSymbol;

    /// <summary>
    /// Asynchronously retrieves symbols from this and all nested scopes, optionally filtered by name.
    /// </summary>
    /// <param name="name">If provided, only returns symbols with that name.</param>
    /// <returns>A task that resolves to a list of symbols in definition order.</returns>
    Task<IList<BaseSymbol>> GetAllNestedSymbolsAsync(string? name = null);

    /// <summary>
    /// Synchronously retrieves symbols from this and all nested scopes, optionally filtered by name.
    /// </summary>
    /// <param name="name">If provided, only returns symbols with that name.</param>
    /// <returns>A list of all symbols in definition order.</returns>
    IList<BaseSymbol> GetAllNestedSymbols(string? name = null);

    /// <summary>
    /// Asynchronously retrieves direct child symbols of a given type.
    /// </summary>
    /// <typeparam name="T">The type of the symbols to return.</typeparam>
    /// <returns>A task that resolves to a list of direct children of the given type.</returns>
    Task<IList<T>> GetSymbolsOfTypeAsync<T>() where T : BaseSymbol;

    /// <summary>
    /// Asynchronously retrieves all symbols of a given type accessible from this scope.
    /// </summary>
    /// <typeparam name="T">The type of symbols to return.</typeparam>
    /// <param name="localOnly">If true, only child symbols are returned; otherwise, symbols from parent scopes are also included.</param>
    /// <returns>A task resolving to all accessible symbols of the specified type.</returns>
    Task<IList<T>> GetAllSymbolsAsync<T>(bool localOnly = false) where T : BaseSymbol;

    /// <summary>
    /// Synchronously retrieves all symbols of a given type accessible from this scope.
    /// </summary>
    /// <typeparam name="T">The type of symbols to return.</typeparam>
    /// <param name="localOnly">If true, only child symbols are returned; otherwise, symbols from parent scopes are also included.</param>
    /// <returns>A list of all accessible symbols of the specified type.</returns>
    IList<T> GetAllSymbols<T>(bool localOnly = false) where T : BaseSymbol;

    /// <summary>
    /// Asynchronously resolves a name to the first symbol found in this scope or any parent scope.
    /// </summary>
    /// <param name="name">The name of the symbol to resolve.</param>
    /// <param name="localOnly">If true, only child symbols are considered.</param>
    /// <returns>A task resolving to the found symbol or null.</returns>
    Task<BaseSymbol?> ResolveAsync(string name, bool localOnly = false);

    /// <summary>
    /// Synchronously resolves a name to the first symbol found in this scope or any parent scope.
    /// </summary>
    /// <param name="name">The name of the symbol to resolve.</param>
    /// <param name="localOnly">If true, only child symbols are considered.</param>
    /// <returns>The found symbol or null.</returns>
    BaseSymbol? Resolve(string name, bool localOnly = false);

    /// <summary>
    /// Finds a symbol by a path of symbol names.
    /// </summary>
    /// <param name="path">The path consisting of symbol names.</param>
    /// <param name="separator">The character separating path segments.</param>
    /// <returns>The symbol at the given path, or null if not found.</returns>
    BaseSymbol? SymbolFromPath(string path, string separator = ".");

    /// <summary>
    /// Gets the index of a direct child symbol.
    /// </summary>
    /// <param name="child">The child symbol to find.</param>
    /// <returns>The zero-based index of the child, or -1 if not found.</returns>
    int IndexOfChild(BaseSymbol child);

    BaseSymbol? NextSiblingOf(BaseSymbol child);
    BaseSymbol? PreviousSiblingOf(BaseSymbol child);
    BaseSymbol? NextOf(BaseSymbol child);
}

/// <summary>
/// A symbol that can contain other symbols (a scope).
/// </summary>
public class ScopedSymbol : BaseSymbol, IScopedSymbol
{
    private readonly List<BaseSymbol> _children = new();
    private readonly Dictionary<string, int> _names = new();

    public ScopedSymbol(string name = "") : base(name)
    {
    }

    public Task<IList<IScopedSymbol>> DirectScopesAsync => await GetSymbolsOfTypeAsync<IScopedSymbol>();

    public IList<BaseSymbol> Children => _children.AsReadOnly();
    public BaseSymbol? FirstChild => _children.Count > 0 ? _children[0] : null;
    public BaseSymbol? LastChild => _children.Count > 0 ? _children[^1] : null;

    public virtual void Clear()
    {
        _children.Clear();
        _names.Clear();
    }

    public void AddSymbol(BaseSymbol symbol)
    {
        symbol.RemoveFromParent();

        var symbolTable = SymbolTable;
        if (symbolTable != null && !symbolTable.Options.AllowDuplicateSymbols)
        {
            if (_names.ContainsKey(symbol.Name))
            {
                throw new InvalidOperationException($"Attempt to add duplicate symbol '{symbol.Name}'");
            }
        }

        if (_children.Contains(symbol))
        {
            throw new InvalidOperationException($"Attempt to add duplicate symbol instance '{symbol.Name}'");
        }

        _names[symbol.Name] = _names.TryGetValue(symbol.Name, out var count) ? count + 1 : 1;
        _children.Add(symbol);
        symbol.Parent = this;
    }

    public void RemoveSymbol(BaseSymbol symbol)
    {
        if (_children.Remove(symbol))
        {
            symbol.Parent = null;
            if (_names.TryGetValue(symbol.Name, out var count))
            {
                if (count == 1)
                {
                    _names.Remove(symbol.Name);
                }
                else
                {
                    _names[symbol.Name] = count - 1;
                }
            }
        }
    }

    public async Task<IList<T>> GetNestedSymbolsOfTypeAsync<T>() where T : BaseSymbol
    {
        var result = new List<T>();
        var childTasks = new List<Task<IList<T>>>();

        foreach (var child in _children)
        {
            if (child is T typedChild)
            {
                result.Add(typedChild);
            }
            if (child is IScopedSymbol scopedChild)
            {
                childTasks.Add(scopedChild.GetNestedSymbolsOfTypeAsync<T>());
            }
        }

        var nestedResults = await Task.WhenAll(childTasks);
        result.AddRange(nestedResults.SelectMany(x => x));

        return result;
    }

    public IList<T> GetNestedSymbolsOfType<T>() where T : BaseSymbol
    {
        var result = new List<T>();
        foreach (var child in _children)
        {
            if (child is T typedChild)
            {
                result.Add(typedChild);
            }
            if (child is IScopedSymbol scopedChild)
            {
                result.AddRange(scopedChild.GetNestedSymbolsOfType<T>());
            }
        }
        return result;
    }

    public async Task<IList<BaseSymbol>> GetAllNestedSymbolsAsync(string? name = null)
    {
        var result = new List<BaseSymbol>();
        var childTasks = new List<Task<IList<BaseSymbol>>>();

        foreach (var child in _children)
        {
            if (name == null || child.Name == name)
            {
                result.Add(child);
            }
            if (child is IScopedSymbol scopedChild)
            {
                childTasks.Add(scopedChild.GetAllNestedSymbolsAsync(name));
            }
        }

        var nestedResults = await Task.WhenAll(childTasks);
        result.AddRange(nestedResults.SelectMany(x => x));

        return result;
    }

    public IList<BaseSymbol> GetAllNestedSymbols(string? name = null)
    {
        var result = new List<BaseSymbol>();
        foreach (var child in _children)
        {
            if (name == null || child.Name == name)
            {
                result.Add(child);
            }
            if (child is IScopedSymbol scopedChild)
            {
                result.AddRange(scopedChild.GetAllNestedSymbols(name));
            }
        }
        return result;
    }

    public Task<IList<T>> GetSymbolsOfTypeAsync<T>() where T : BaseSymbol
    {
        return Task.FromResult<IList<T>>(_children.OfType<T>().ToList());
    }

    public virtual async Task<IList<T>> GetAllSymbolsAsync<T>(bool localOnly = false) where T : BaseSymbol
    {
        var result = new List<T>();

        foreach (var child in _children)
        {
            if (child is T typedChild)
            {
                result.Add(typedChild);
            }

            if (child is INamespaceSymbol namespaceSymbol)
            {
                result.AddRange(await namespaceSymbol.GetAllSymbolsAsync<T>(true));
            }
        }

        if (!localOnly && Parent is IScopedSymbol parentScope)
        {
            result.AddRange(await parentScope.GetAllSymbolsAsync<T>(false));
        }

        return result;
    }

    public virtual IList<T> GetAllSymbols<T>(bool localOnly = false) where T : BaseSymbol
    {
        var result = new List<T>();
        
        foreach (var child in _children)
        {
            if (child is T typedChild)
            {
                result.Add(typedChild);
            }
            if (child is INamespaceSymbol namespaceSymbol)
            {
                result.AddRange(namespaceSymbol.GetAllSymbols<T>(true));
            }
        }

        if (!localOnly && Parent is IScopedSymbol parentScope)
        {
            result.AddRange(parentScope.GetAllSymbols<T>(false));
        }

        return result;
    }

    public override async Task<BaseSymbol?> ResolveAsync(string name, bool localOnly = false)
    {
        var child = _children.FirstOrDefault(c => c.Name == name);
        if (child != null)
        {
            return child;
        }

        if (!localOnly && Parent is IScopedSymbol parentScope)
        {
            return await parentScope.ResolveAsync(name, false);
        }

        return null;
    }

    public override BaseSymbol? Resolve(string name, bool localOnly = false)
    {
        var child = _children.FirstOrDefault(c => c.Name == name);
        if (child != null)
        {
            return child;
        }

        if (!localOnly && Parent is IScopedSymbol parentScope)
        {
            return parentScope.Resolve(name, false);
        }

        return null;
    }

    public BaseSymbol? SymbolFromPath(string path, string separator = ".")
    {
        var elements = path.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
        var index = 0;
        if (elements.Length > 0 && elements[0] == Name)
        {
            index++;
        }

        BaseSymbol? result = this;
        while (index < elements.Length)
        {
            if (result is not IScopedSymbol scopedResult)
            {
                return null;
            }

            var child = scopedResult.Children.FirstOrDefault(c => c.Name == elements[index]);
            if (child == null)
            {
                return null;
            }
            result = child;
            index++;
        }
        return result;
    }

    public int IndexOfChild(BaseSymbol child) => _children.IndexOf(child);

    public BaseSymbol? NextSiblingOf(BaseSymbol child)
    {
        var index = IndexOfChild(child);
        return (index == -1 || index >= _children.Count - 1) ? null : _children[index + 1];
    }

    public BaseSymbol? PreviousSiblingOf(BaseSymbol child)
    {
        var index = IndexOfChild(child);
        return (index < 1) ? null : _children[index - 1];
    }

    public BaseSymbol? NextOf(BaseSymbol child)
    {
        if (child.Parent == null) return null;

        if (child.Parent != this)
        {
            return (child.Parent as IScopedSymbol)?.NextOf(child);
        }

        if (child is IScopedSymbol scopedChild && scopedChild.Children.Any())
        {
            return scopedChild.FirstChild;
        }

        var sibling = NextSiblingOf(child);
        if (sibling != null)
        {
            return sibling;
        }

        return (Parent as IScopedSymbol)?.NextOf(this);
    }
}