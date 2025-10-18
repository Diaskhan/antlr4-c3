/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using System.Collections.Generic;

namespace Antlr4CodeCompletion;

/// <summary>
/// Visibility (aka. accessibility) of a symbol member.
/// </summary>
public enum MemberVisibility
{
    /// <summary>
    /// Not specified, default depends on the language and type.
    /// </summary>
    Unknown,

    /// <summary>
    /// Used in Swift, member can be accessed outside of the defining module and extended.
    /// </summary>
    Open,

    /// <summary>
    /// Like Open, but in Swift such a type cannot be extended.
    /// </summary>
    Public,

    /// <summary>
    /// Member is only accessible in the defining class and any derived class.
    /// </summary>
    Protected,

    /// <summary>
    /// Member can only be accessed from the defining class.
    /// </summary>
    Private,

    /// <summary>
    /// Used in Swift and Java, member can be accessed from everywhere in a defining module, not outside however.
    /// Also known as package private.
    /// </summary>
    FilePrivate,

    /// <summary>
    /// Custom enum for special usage.
    /// </summary>
    Library,
}

/// <summary>
/// The modifier of a symbol member.
/// </summary>
public enum Modifier
{
    Static,
    Final,
    Sealed,
    Abstract,
    Deprecated,
    Virtual,
    Const,
    Overwritten,
}

/// <summary>
/// Rough categorization of a type.
/// </summary>
public enum TypeKind
{
    Unknown,
    Integer,
    Float,
    Number,
    String,
    Char,
    Boolean,
    Class,
    Interface,
    Array,
    Map,
    Enum,
    Alias,
}

/// <summary>
/// Describes a reference to a type.
/// </summary>
public enum ReferenceKind
{
    Irrelevant,

    /// <summary>
    /// Default for most languages for dynamically allocated memory ("Type*" in C++).
    /// </summary>
    Pointer,

    /// <summary>
    /// "Type&amp;" in C++, all non-primitive types in Java/Javascript/Typescript etc.
    /// </summary>
    Reference,

    /// <summary>
    /// "Type" as such and default for all value types.
    /// </summary>
    Instance,
}

/// <summary>
/// The root type interface. Used for typed symbols and type aliases.
/// </summary>
public interface IType
{
    string Name { get;  }

    /// <summary>
    /// The super type of this type or empty if this is a fundamental type.
    /// Also used as the target type for type aliases.
    /// </summary>
    IList<IType> BaseTypes { get;  }
    TypeKind Kind { get;  }
    ReferenceKind Reference { get;  }
}

public interface ISymbolTableOptions
{
    bool? AllowDuplicateSymbols { get; set; }
}