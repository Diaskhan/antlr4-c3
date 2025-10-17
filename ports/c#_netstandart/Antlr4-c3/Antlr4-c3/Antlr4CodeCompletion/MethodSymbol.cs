/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

namespace Antlr4CodeCompletion;


/// <summary>
/// A collection of flags for a method.
/// </summary>
[Flags]
public enum MethodFlags
{
    /// <summary>
    /// No flag set.
    /// </summary>
    None = 0,

    /// <summary>
    /// The method is virtual.
    /// </summary>
    Virtual = 1,

    /// <summary>
    /// The method is constant.
    /// </summary>
    Const = 2,

    /// <summary>
    /// The method is an override.
    /// </summary>
    Overwritten = 4,

    /// <summary>
    /// The method is a setter or getter. Distinguished by the return type.
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
    /// <summary>
    /// Gets or sets the flags for this method.
    /// </summary>
    public MethodFlags MethodFlags { get; set; } = MethodFlags.None;

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodSymbol"/> class.
    /// </summary>
    /// <param name="name">The name of the method.</param>
    /// <param name="returnType">The return type of the method.</param>
    public MethodSymbol(string name, IType? returnType = null) : base(name, returnType)
    {
    }
}