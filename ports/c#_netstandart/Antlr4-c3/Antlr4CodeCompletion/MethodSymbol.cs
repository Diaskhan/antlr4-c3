/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using System;

namespace Antlr4CodeCompletion;

[Flags]
public enum MethodFlags
{
    None = 0,
    Virtual = 1,
    Const = 2,
    Overwritten = 4,

    /// <summary>
    /// Distinguished by the return type.
    /// </summary>
    SetterOrGetter = 8,

    /// <summary>
    /// Special flag used e.g. in C++ for explicit constructors.
    /// </summary>
    Explicit = 16,
}

/// <summary>
/// A function which belongs to a class or other outer container structure.
/// </summary>
public class MethodSymbol : RoutineSymbol
{
    public MethodFlags MethodFlags { get; set; } = MethodFlags.None;

    public MethodSymbol(string name, IType? parent) : base(name, parent)
    {
    }
}