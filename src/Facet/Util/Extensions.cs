using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Facet.Util;

public static class Extensions
{

    public static string WithIndent(this string src, string indent)
        => src.Split('\n')
            .Select(x => $"{indent}{x}")
            .JoinStrings("\n");
    
    public static string Fqn(this ITypeSymbol ts)
    {
        var displayString = ts.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (ts.NullableAnnotation == NullableAnnotation.Annotated && !displayString.EndsWith("?"))
        {
            displayString += "?";
        }

        return displayString;
    }

    public static string Fqn(this Type type)
    {
        var fqn = "global::";
        if (!String.IsNullOrEmpty(type.Namespace))
        {
            fqn += type.Namespace + ".";
        }

        if (!type.IsGenericType)
        {
            fqn += type.Name;
        }
        else
        {
            var name = type.Name.Split('`')[0];
            fqn += $"{name}<{String.Join(", ", type.GenericTypeArguments.Select(t => t.Fqn()))}>";
        }

        return fqn;
    }

    public static string JoinStrings(this IEnumerable<string> strings, string sep)
    {
        return String.Join(sep, strings);
    }

    public static bool IsWrappedNullable(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

}