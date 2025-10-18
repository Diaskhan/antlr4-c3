/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

namespace Antlr4CodeCompletion;

/// <summary>
/// Represents an interface symbol.
/// </summary>
public class InterfaceSymbol : ScopedSymbol, IType
{
    /// <summary>
    /// Gets the list of types this interface extends.
    /// </summary>
    /// <remarks>
    /// The TypeScript version supports extending classes as well as interfaces.
    /// In this C# representation, we use the common <see cref="IType"/> interface.
    /// </remarks>
    public IList<IType> ExtendedTypes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InterfaceSymbol"/> class.
    /// </summary>
    /// <param name="name">The name of the interface.</param>
    /// <param name="extended">The list of types that this interface extends.</param>
    public InterfaceSymbol(string name, IList<IType> extended) : base(name)
    {
        ExtendedTypes = extended ?? new List<IType>();
    }

    /// <summary>
    /// Compatibility constructor used by tests where object[]/Array.Empty<object>() may be passed via Activator.
    /// </summary>
    public InterfaceSymbol(string name, object[] extended) : this(name, ConvertToITypeList(extended))
    {
    }

    private static IList<IType> ConvertToITypeList(object[]? arr)
    {
        if (arr == null) return new List<IType>();
        return arr.OfType<IType>().ToList();
    }

    /// <inheritdoc/>
    public IList<IType> BaseTypes => ExtendedTypes;

    /// <inheritdoc/>
    public TypeKind Kind => TypeKind.Interface;

    /// <inheritdoc/>
    public ReferenceKind Reference { get; } = ReferenceKind.Irrelevant;

    /// <summary>
    /// Gets a list of all methods in this interface.
    /// </summary>
    /// <param name="includeInherited">This parameter is not used.</param>
    /// <returns>A task that represents the asynchronous operation and contains the list of all methods.</returns>
    public Task<List<MethodSymbol>> GetMethodsAsync(bool includeInherited = false)
    {
        return Task.FromResult(GetSymbolsOfType<MethodSymbol, object>(x => (MethodSymbol)x[0]).Result);
    }

    /// <summary>
    /// Gets a list of all fields in this interface.
    /// </summary>
    /// <param name="includeInherited">This parameter is not used.</param>
    /// <returns>A task that represents the asynchronous operation and contains the list of all fields.</returns>
    public Task<List<FieldSymbol>> GetFieldsAsync(bool includeInherited = false)
    {
        return Task.FromResult(GetSymbolsOfType<FieldSymbol, object>(x => (FieldSymbol)x[0]).Result);
    }
}