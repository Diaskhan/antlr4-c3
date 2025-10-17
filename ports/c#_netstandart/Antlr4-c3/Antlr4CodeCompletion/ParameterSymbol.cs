/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

namespace Antlr4CodeCompletion;

/// <summary>
/// A symbol representing a parameter to a function or method.
/// </summary>
public class ParameterSymbol : VariableSymbol
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterSymbol"/> class.
    /// </summary>
    /// <param name="name">The name of the symbol.</param>
    /// <param name="value">The value of the symbol.</param>
    /// <param name="type">The type of the symbol.</param>
    public ParameterSymbol(string name, object value, IType? type = null) : base(name, value, type)
    {
    }
}