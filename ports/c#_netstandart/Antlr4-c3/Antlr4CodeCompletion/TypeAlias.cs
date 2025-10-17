/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using System.Collections.Generic;

namespace Antlr4CodeCompletion;

/// <summary>
/// An alias for another type.
/// </summary>
public class TypeAlias : BaseSymbol, IType
{
    private readonly IType _targetType;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeAlias"/> class.
    /// </summary>
    /// <param name="name">The name of the alias.</param>
    /// <param name="target">The type this alias refers to.</param>
    public TypeAlias(string name, IType target) : base(name)
    {
        _targetType = target;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IType> BaseTypes => new[] { _targetType };

    /// <inheritdoc/>
    public TypeKind Kind => TypeKind.Alias;

    /// <inheritdoc/>
    public ReferenceKind Reference => ReferenceKind.Irrelevant;
}