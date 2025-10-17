/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

import { IType, ReferenceKind, TypeKind } from "./types.js";

import { FieldSymbol } from "./FieldSymbol.js";
import { InterfaceSymbol } from "./InterfaceSymbol.js";
import { MethodSymbol } from "./MethodSymbol.js";
import { ScopedSymbol } from "./ScopedSymbol.js";

/** Classes and structs. */
export class ClassSymbol extends ScopedSymbol implements IType {
    public isStruct = false;
    public reference = ReferenceKind.Irrelevant;

    /** Usually only one member, unless the language supports multiple inheritance (like C++). */
    // eslint-disable-next-line no-use-before-define
    public readonly extends: ClassSymbol[];

    /** Typescript allows a class to implement a class, not only interfaces. */
    // eslint-disable-next-line no-use-before-define
    public readonly implements: Array<ClassSymbol | InterfaceSymbol>;

    public constructor(name: string, ext: ClassSymbol[], impl: Array<ClassSymbol | InterfaceSymbol>) {
        super(name);
        this.extends = ext;
        this.implements = impl;
    }

    public get baseTypes(): IType[] { return this.extends; }
    public get kind(): TypeKind { return TypeKind.Class; }

    /**
     * @param _includeInherited Not used.
     *
     * @returns a list of all methods.
     */
    public getMethods(_includeInherited = false): Promise<MethodSymbol[]> {
        return this.getSymbolsOfType(MethodSymbol);
    }

    /**
     * @param _includeInherited Not used.
     *
     * @returns all fields.
     */
    public getFields(_includeInherited = false): Promise<FieldSymbol[]> {
        return this.getSymbolsOfType(FieldSymbol);
    }
}
