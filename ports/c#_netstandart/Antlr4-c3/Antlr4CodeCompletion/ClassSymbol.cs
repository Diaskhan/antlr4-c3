/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Antlr4CodeCompletion;

/// <summary>
/// Represents symbols for classes and structs.
/// </summary>
public class ClassSymbol : ScopedSymbol, IType
{
    /// <summary>
    /// Gets or sets a value indicating whether this symbol represents a struct.
    /// </summary>
    public bool IsStruct { get; set; }

    /// <summary>
    /// Gets the list of classes this class extends. C# supports single inheritance, 
    /// but other languages may support multiple, so a list is used.
    /// </summary>
    public IList<ClassSymbol> BaseClasses { get; }

    /// <summary>
    /// Gets the list of interfaces and classes this class implements.
    /// </summary>
    public IList<IType> ImplementedInterfaces { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassSymbol"/> class.
    /// </summary>
    /// <param name="name">The name of the class.</param>
    /// <param name="baseClasses">The list of classes this class extends.</param>
    /// <param name="implementedInterfaces">The list of interfaces and classes this class implements.</param>
    public ClassSymbol(string name, IList<ClassSymbol> baseClasses, IList<IType> implementedInterfaces)
        : base(name)
    {
        BaseClasses = baseClasses;
        ImplementedInterfaces = implementedInterfaces;
    }

    /// <inheritdoc/>
    public IList<IType> BaseTypes => (IList<IType>)BaseClasses;

    /// <inheritdoc/>
    public TypeKind Kind => TypeKind.Class;

    /// <inheritdoc/>
    public ReferenceKind Reference { get; set; } = ReferenceKind.Irrelevant;

    /// <summary>
    /// Gets a list of all methods in this class.
    /// </summary>
    /// <param name="includeInherited">This parameter is not used.</param>
    /// <returns>A task that represents the asynchronous operation and contains the list of all methods.</returns>
    public Task<IList<MethodSymbol>> GetMethodsAsync(bool includeInherited = false)
    {
        return GetSymbolsOfTypeAsync<MethodSymbol>();
    }

    /// <summary>
    /// Gets a list of all fields in this class.
    /// </summary>
    /// <param name="includeInherited">This parameter is not used.</param>
    /// <returns>A task that represents the asynchronous operation and contains the list of all fields.</returns>
    public Task<IList<FieldSymbol>> GetFieldsAsync(bool includeInherited = false)
    {
        return GetSymbolsOfTypeAsync<FieldSymbol>();
    }
}