/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using System;

namespace Antlr4CodeCompletion.Core;

/// <summary>
/// Represents an error that occurs when a duplicate symbol is found.
/// </summary>
public class DuplicateSymbolError : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateSymbolError"/> class.
    /// </summary>
    public DuplicateSymbolError()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateSymbolError"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DuplicateSymbolError(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateSymbolError"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public DuplicateSymbolError(string message, Exception innerException) : base(message, innerException)
    {
    }
}