/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

namespace Antlr4CodeCompletion;

/// <summary>
/// A field which belongs to a class or other outer container structure.
/// </summary>
public class FieldSymbol : VariableSymbol
{
    /// <summary>
    /// Gets or sets the setter for this field, if any.
    /// </summary>
    public MethodSymbol? Setter { get; set; }

    /// <summary>
    /// Gets or sets the getter for this field, if any.
    /// </summary>
    public MethodSymbol? Getter { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldSymbol"/> class.
    /// </summary>
    /// <param name="name">The name of the symbol.</param>
    /// <param name="value">The value of the symbol.</param>
    /// <param name="type">The type of the symbol.</param>
    public FieldSymbol(string name, object value, IType? type = null) : base(name, value, type)
    {
    }
}