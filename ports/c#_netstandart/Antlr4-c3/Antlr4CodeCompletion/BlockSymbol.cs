/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

namespace Antlr4CodeCompletion;

/// <summary>
/// A symbol representing a block of code, which is a scope.
/// </summary>
public class BlockSymbol : ScopedSymbol
{
    // A block symbol is just a scoped symbol and does not add any new members.
}