/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Antlr4CodeCompletion;

public interface ISymbolTable : IScopedSymbol
{
    ISymbolTableOptions Options { get; }

    /// <summary>
    /// Gets instance information, mostly relevant for unit testing.
    /// </summary>
    (int DependencyCount, int SymbolCount) Info { get; }

    void Clear();
    void AddDependencies(params SymbolTable[] tables);
    void RemoveDependency(SymbolTable table);
    T AddNewSymbolOfType<T>(ScopedSymbol? parent, params object[] args) where T : BaseSymbol;

    /// <summary>
    /// Asynchronously adds a new namespace to the symbol table or the given parent. The path parameter specifies a
    /// single namespace name or a chain of namespaces (e.g., "outer.intermittent.inner.final").
    /// If any of the parent namespaces are missing, they are created implicitly. The final part must not exist
    /// to avoid a duplicate symbol error.
    /// </summary>
    /// <param name="parent">The parent to add the namespace to.</param>
    /// <param name="path">The namespace path.</param>
    /// <param name="delimiter">The delimiter used in the path.</param>
    /// <returns>A task resolving to the new symbol.</returns>
    Task<NamespaceSymbol> AddNewNamespaceFromPathAsync(ScopedSymbol? parent, string path, string delimiter = ".");

    /// <summary>
    /// Synchronously adds a new namespace to the symbol table or the given parent. The path parameter specifies a
    /// single namespace name or a chain of namespaces (e.g., "outer.intermittent.inner.final").
    /// If any of the parent namespaces are missing, they are created implicitly. The final part must not exist
    /// to avoid a duplicate symbol error.
    /// </summary>
    /// <param name="parent">The parent to add the namespace to.</param>
    /// <param name="path">The namespace path.</param>
    /// <param name="delimiter">The delimiter used in the path.</param>
    /// <returns>The new symbol.</returns>
    NamespaceSymbol AddNewNamespaceFromPath(ScopedSymbol? parent, string path, string delimiter = ".");

    /// <summary>
    /// Asynchronously returns all symbols from this scope (and optionally those from dependencies) of a specific type.
    /// </summary>
    /// <typeparam name="T">The type of the symbols to return.</typeparam>
    /// <param name="localOnly">If true, do not search dependencies.</param>
    /// <returns>A promise which resolves when all symbols are collected.</returns>
    Task<IList<T>> GetAllSymbolsAsync<T>(bool localOnly = false) where T : BaseSymbol;

    /// <summary>
    /// Synchronously returns all symbols from this scope (and optionally those from dependencies) of a specific type.
    /// </summary>
    /// <typeparam name="T">The type of the symbols to return.</typeparam>
    /// <param name="localOnly">If true, do not search dependencies.</param>
    /// <returns>A list with all symbols.</returns>
    IList<T> GetAllSymbols<T>(bool localOnly = false) where T : BaseSymbol;

    /// <summary>
    /// Asynchronously looks for a symbol which is connected with a given parse tree context.
    /// </summary>
    /// <param name="context">The context to search for.</param>
    /// <returns>A task resolving to the found symbol or null.</returns>
    Task<BaseSymbol?> SymbolWithContextAsync(IParseTree context);

    /// <summary>
    /// Synchronously looks for a symbol which is connected with a given parse tree context.
    /// </summary>
    /// <param name="context">The context to search for.</param>
    /// <returns>The found symbol or null.</returns>
    BaseSymbol? SymbolWithContext(IParseTree context);

    /// <summary>
    /// Asynchronously resolves a name to a symbol.
    /// </summary>
    /// <param name="name">The name of the symbol to find.</param>
    /// <param name="localOnly">A flag indicating if only this symbol table should be used or also its dependencies.</param>
    /// <returns>A promise resolving to the found symbol or null.</returns>
    Task<BaseSymbol?> ResolveAsync(string name, bool localOnly = false);

    /// <summary>
    /// Synchronously resolves a name to a symbol.
    /// </summary>
    /// <param name="name">The name of the symbol to find.</param>
    /// <param name="localOnly">A flag indicating if only this symbol table should be used or also its dependencies.</param>
    /// <returns>The found symbol or null.</returns>
    BaseSymbol? Resolve(string name, bool localOnly = false);
}

/// <summary>
/// The main class managing all the symbols for a top-level entity like a file, library, or similar.
/// </summary>
public class SymbolTable : ScopedSymbol, ISymbolTable
{
    /// <summary>
    /// Other symbol information available to this instance.
    /// </summary>
    protected readonly ISet<SymbolTable> dependencies = new HashSet<SymbolTable>();

    public SymbolTable(string name, ISymbolTableOptions options) : base(name)
    {
        Options = options;
    }

    public ISymbolTableOptions Options { get; }

    public (int DependencyCount, int SymbolCount) Info => (dependencies.Count, Children.Count);

    public override void Clear()
    {
        base.Clear();
        dependencies.Clear();
    }

    public void AddDependencies(params SymbolTable[] tables)
    {
        foreach (var table in tables)
        {
            dependencies.Add(table);
        }
    }

    public void RemoveDependency(SymbolTable table)
    {
        dependencies.Remove(table);
    }

    public T AddNewSymbolOfType<T>(ScopedSymbol? parent, params object[] args) where T : BaseSymbol
    {
        var result = (T)Activator.CreateInstance(typeof(T), args)!;
        (parent ?? this).AddSymbol(result);
        return result;
    }

    public async Task<NamespaceSymbol> AddNewNamespaceFromPathAsync(ScopedSymbol? parent, string path, string delimiter = ".")
    {
        var parts = path.Split(delimiter);
        var currentParent = parent ?? this;
        for (var i = 0; i < parts.Length - 1; ++i)
        {
            var part = parts[i];
            var ns = await currentParent.ResolveAsync(part, true) as NamespaceSymbol;
            if (ns == null)
            {
                ns = AddNewSymbolOfType<NamespaceSymbol>(currentParent, part);
            }
            currentParent = ns;
        }

        return AddNewSymbolOfType<NamespaceSymbol>(currentParent, parts[parts.Length - 1]);
    }

    public NamespaceSymbol AddNewNamespaceFromPath(ScopedSymbol? parent, string path, string delimiter = ".")
    {
        var parts = path.Split(delimiter);
        var currentParent = parent ?? this;
        for (var i = 0; i < parts.Length - 1; ++i)
        {
            var part = parts[i];
            var ns = currentParent.Resolve(part, true) as NamespaceSymbol;
            if (ns == null)
            {
                ns = AddNewSymbolOfType<NamespaceSymbol>(currentParent, part);
            }
            currentParent = ns;
        }

        return AddNewSymbolOfType<NamespaceSymbol>(currentParent, parts[parts.Length - 1]);
    }

    public override async Task<IList<T>> GetAllSymbolsAsync<T>(bool localOnly = false)
    {
        var result = new List<T>(await base.GetAllSymbolsAsync<T>(localOnly));

        if (!localOnly)
        {
            var tasks = dependencies.Select(d => d.GetAllSymbolsAsync<T>(localOnly)).ToList();
            var dependencyResults = await Task.WhenAll(tasks);
            result.AddRange(dependencyResults.SelectMany(l => l));
        }

        return result;
    }

    public override IList<T> GetAllSymbols<T>(bool localOnly = false)
    {
        var result = new List<T>(base.GetAllSymbols<T>(localOnly));

        if (!localOnly)
        {
            foreach (var dependency in dependencies)
            {
                result.AddRange(dependency.GetAllSymbols<T>(localOnly));
            }
        }

        return result;
    }

    public async Task<BaseSymbol?> SymbolWithContextAsync(IParseTree context)
    {
        BaseSymbol? findRecursive(BaseSymbol symbol)
        {
            if (symbol.Context == context)
            {
                return symbol;
            }

            if (symbol is ScopedSymbol scopedSymbol)
            {
                foreach (var child in scopedSymbol.Children)
                {
                    var result = findRecursive(child);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        var symbols = await GetAllSymbolsAsync<BaseSymbol>(false);
        foreach (var symbol in symbols)
        {
            var result = findRecursive(symbol);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    public BaseSymbol? SymbolWithContext(IParseTree context)
    {
        BaseSymbol? findRecursive(BaseSymbol symbol)
        {
            if (symbol.Context == context)
            {
                return symbol;
            }

            if (symbol is ScopedSymbol scopedSymbol)
            {
                foreach (var child in scopedSymbol.Children)
                {
                    var result = findRecursive(child);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        var symbols = GetAllSymbols<BaseSymbol>(false);
        foreach (var symbol in symbols)
        {
            var result = findRecursive(symbol);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    public override async Task<BaseSymbol?> ResolveAsync(string name, bool localOnly = false)
    {
        var result = await base.ResolveAsync(name, localOnly);
        if (result == null && !localOnly)
        {
            foreach (var dependency in dependencies)
            {
                result = await dependency.ResolveAsync(name, false);
                if (result != null)
                {
                    return result;
                }
            }
        }

        return result;
    }

    public override BaseSymbol? Resolve(string name, bool localOnly = false)
    {
        var result = base.Resolve(name, localOnly);
        if (result == null && !localOnly)
        {
            foreach (var dependency in dependencies)
            {
                result = dependency.Resolve(name, false);
                if (result != null)
                {
                    return result;
                }
            }
        }

        return result;
    }

}