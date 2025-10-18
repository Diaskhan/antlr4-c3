/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using System;
using System.Collections.Generic;

namespace Antlr4CodeCompletion;

/// <summary>
/// Represents an array type.
/// </summary>
public class ArrayType : BaseSymbol, IType
{
    private readonly ReferenceKind _referenceKind;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrayType"/> class.
    /// </summary>
    /// <param name="name">The name of the type.</param>
    /// <param name="referenceKind">The reference kind for this type.</param>
    /// <param name="elemType">The type of the elements in the array.</param>
    /// <param name="size">The size of the array, if fixed. A value of 0 or less indicates a variable-size array.</param>
    public ArrayType(string name, ReferenceKind referenceKind, IType elemType, int size = 0) : base(name)
    {
        _referenceKind = referenceKind;
        ElementType = elemType;
        Size = size;
    }

    /// <summary>
    /// Gets the type of the elements in this array.
    /// </summary>
    public IType ElementType { get; }

    /// <summary>
    /// Gets the size of the array if it's a fixed-size array, otherwise 0 or less.
    /// </summary>
    public int Size { get; }

    /// <inheritdoc/>
    public IList<IType> BaseTypes => Array.Empty<IType>();

    /// <inheritdoc/>
    public TypeKind Kind => TypeKind.Array;

    /// <inheritdoc/>
    public ReferenceKind Reference => _referenceKind;
}