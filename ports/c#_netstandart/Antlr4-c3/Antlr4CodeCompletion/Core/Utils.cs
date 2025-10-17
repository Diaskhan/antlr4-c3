/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Antlr4CodeCompletion.Core;

public static class Utils
{
    public static T[] LongestCommonPrefix<T>(T[]? arr1, T[]? arr2)
    {
        if (arr1 == null || arr2 == null)
        {
            return Array.Empty<T>();
        }

        int i;
        for (i = 0; i < Math.Min(arr1.Length, arr2.Length); i++)
        {
            if (!EqualityComparer<T>.Default.Equals(arr1[i], arr2[i]))
            {
                break;
            }
        }

        return arr1.Take(i).ToArray();
    }
}