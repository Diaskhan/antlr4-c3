/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using System;
using System.Collections.Generic;

namespace Antlr4CodeCompletion;

/// <summary>
/// Defines properties for a namespace symbol.
/// </summary>
public interface INamespaceSymbol : IScopedSymbol
{
    /// <summary>
    /// Gets a value indicating whether the namespace is an inline namespace.
    /// </summary>
    bool IsInline { get; }

    /// <summary>
    /// Gets the attributes of the namespace.
    /// </summary>
    IReadOnlyList<string> Attributes { get; }
}

/// <summary>
/// Represents a namespace symbol.
/// </summary>
public class NamespaceSymbol : ScopedSymbol, INamespaceSymbol
{
    /// <inheritdoc/>
    public bool IsInline { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Attributes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NamespaceSymbol"/> class.
    /// </summary>
    /// <param name="name">The name of the namespace.</param>
    /// <param name="isInline">A value indicating whether the namespace is inline.</param>
    /// <param name="attributes">A list of attributes for the namespace.</param>
    public NamespaceSymbol(string name, bool isInline = false, IReadOnlyList<string>? attributes = null) : base(name)
    {
        IsInline = isInline;
        Attributes = attributes ?? Array.Empty<string>();
    }
}