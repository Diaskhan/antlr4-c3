/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using System;
using System.Collections.Generic;

namespace Antlr4CodeCompletion;

/// <summary>
/// A single class for all fundamental types. They are distinguished via the kind field.
/// </summary>
public class FundamentalType : IType
{
    private readonly TypeKind _typeKind;
    private readonly ReferenceKind _referenceKind;

    /// <summary>
    /// A shared instance of the integer type.
    /// </summary>
    public static readonly FundamentalType IntegerType = new("int", TypeKind.Integer, ReferenceKind.Instance);

    /// <summary>
    /// A shared instance of the float type.
    /// </summary>
    public static readonly FundamentalType FloatType = new("float", TypeKind.Float, ReferenceKind.Instance);

    /// <summary>
    /// A shared instance of the string type.
    /// </summary>
    public static readonly FundamentalType StringType = new("string", TypeKind.String, ReferenceKind.Instance);

    /// <summary>
    /// A shared instance of the boolean type.
    /// </summary>
    public static readonly FundamentalType BoolType = new("bool", TypeKind.Boolean, ReferenceKind.Instance);

    /// <summary>
    /// Initializes a new instance of the <see cref="FundamentalType"/> class.
    /// </summary>
    /// <param name="name">The name of the type.</param>
    /// <param name="typeKind">The kind of the type.</param>
    /// <param name="referenceKind">The reference kind of the type.</param>
    public FundamentalType(string name, TypeKind typeKind = TypeKind.Unknown,
        ReferenceKind referenceKind = ReferenceKind.Irrelevant)
    {
        Name = name;
        _typeKind = typeKind;
        _referenceKind = referenceKind;
    }

    /// <summary>
    /// Gets the name of the type.
    /// </summary>
    public string Name { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IType> BaseTypes => Array.Empty<IType>();

    /// <inheritdoc/>
    public TypeKind Kind => _typeKind;

    /// <inheritdoc/>
    public ReferenceKind Reference => _referenceKind;
}