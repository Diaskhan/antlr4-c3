"use strict";
/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */
Object.defineProperty(exports, "__esModule", { value: true });
exports.longestCommonPrefix = void 0;
var longestCommonPrefix = function (arr1, arr2) {
    if (!arr1 || !arr2) {
        return [];
    }
    var i;
    for (i = 0; i < Math.min(arr1.length, arr2.length); i++) {
        if (arr1[i] !== arr2[i]) {
            break;
        }
    }
    return arr1.slice(0, i);
};
exports.longestCommonPrefix = longestCommonPrefix;
