/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Antlr4CodeCompletion;

/// <summary>
/// A standalone function/procedure/rule.
/// </summary>
public class RoutineSymbol : ScopedSymbol
{
    /// <summary>
    /// Gets or sets the return type of this routine. Can be null if the routine does not return a value.
    /// </summary>
    public IType? ReturnType { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutineSymbol"/> class.
    /// </summary>
    /// <param name="name">The name of the routine.</param>
    /// <param name="returnType">The return type of the routine.</param>
    public RoutineSymbol(string name, IType? returnType = null) : base(name)
    {
        ReturnType = returnType;
    }

    /// <summary>
    /// Gets the variables defined in this routine.
    /// </summary>
    /// <param name="localOnly">If true, only returns symbols from the routine's direct scope.</param>
    /// <returns>A task representing the asynchronous operation that yields a list of variable symbols.</returns>
    public Task<List<VariableSymbol>> GetVariablesAsync(bool localOnly = true)
    {
        return Task.FromResult(GetSymbolsOfType<VariableSymbol, object>(x => (VariableSymbol)x[0]).Result);
    }

    /// <summary>
    /// Gets the parameters of this routine.
    /// </summary>
    /// <param name="localOnly">If true, only returns symbols from the routine's direct scope.</param>
    /// <returns>A task representing the asynchronous operation that yields a list of parameter symbols.</returns>
    public Task<List<ParameterSymbol>> GetParametersAsync(bool localOnly = true)
    {
        //return GetSymbolsOfTypeAsync<ParameterSymbol>(localOnly);
        return Task.FromResult(GetSymbolsOfType<ParameterSymbol, object>(x => (ParameterSymbol)x[0]).Result);
    }
}
