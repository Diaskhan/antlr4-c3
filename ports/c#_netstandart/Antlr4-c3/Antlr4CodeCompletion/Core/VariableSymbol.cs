/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */


namespace Antlr4CodeCompletion.Core;

/// <summary>
/// A symbol with a value and an optional type.
/// </summary>
public class VariableSymbol : TypedSymbol
{
    /// <summary>
    /// Gets or sets the value of this variable. The type of the value is not checked against the type of the symbol.
    /// </summary>
    public object Value { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VariableSymbol"/> class.
    /// </summary>
    /// <param name="name">The name of the symbol.</param>
    /// <param name="value">The value of the symbol.</param>
    /// <param name="type">The type of the symbol.</param>
    public VariableSymbol(string name, object value, IType type = null) : base(name, type)
    {
        Value = value;
    }
}