﻿namespace Everywhere.Extensions;

public static class ReflectionExtension
{
    public static IEnumerable<Type> EnumerateBaseTypes(this Type type)
    {
        var currentType = type.BaseType;
        while (currentType != null)
        {
            yield return currentType;
            currentType = currentType.BaseType;
        }
    }
}