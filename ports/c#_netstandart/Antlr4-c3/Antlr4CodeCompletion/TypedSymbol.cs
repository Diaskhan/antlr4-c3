/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

namespace Antlr4CodeCompletion;

/// <summary>
/// A symbol with an attached type (variables, fields etc.).
/// </summary>
public class TypedSymbol : BaseSymbol
{
    /// <summary>
    /// Gets or sets the type of this symbol.
    /// </summary>
    public IType? Type { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedSymbol"/> class.
    /// </summary>
    /// <param name="name">The name of the symbol.</param>
    /// <param name="type">The type of the symbol.</param>
    public TypedSymbol(string name, IType? type = null) : base(name)
    {
        Type = type;
    }
}