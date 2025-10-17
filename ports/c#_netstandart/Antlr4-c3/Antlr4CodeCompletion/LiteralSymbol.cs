/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

namespace Antlr4CodeCompletion;

/// <summary>
/// A symbol representing a literal value.
/// </summary>
public class LiteralSymbol : TypedSymbol
{
    /// <summary>
    /// Gets the value of this literal.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LiteralSymbol"/> class.
    /// </summary>
    /// <param name="name">The name of the symbol.</param>
    /// <param name="value">The value of the symbol.</param>
    /// <param name="type">The type of the symbol.</param>
    public LiteralSymbol(string name, object value, IType? type = null) : base(name, type)
    {
        Value = value;
    }
}