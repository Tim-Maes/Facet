using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Handles extraction and generation of copiable attributes from source type members.
/// </summary>
internal static class AttributeProcessor
{
    /// <summary>
    /// Extracts copiable attributes from a member symbol.
    /// Filters out internal compiler attributes and non-copiable attributes.
    /// </summary>
    public static List<string> ExtractCopiableAttributes(ISymbol member, FacetMemberKind targetKind)
    {
        var (attributes, _) = ExtractCopiableAttributesWithNamespaces(member, targetKind);
        return attributes;
    }

    /// <summary>
    /// Extracts copiable attributes from a member symbol along with their namespaces.
    /// Filters out internal compiler attributes and non-copiable attributes.
    /// </summary>
    public static (List<string> attributes, HashSet<string> namespaces) ExtractCopiableAttributesWithNamespaces(ISymbol member, FacetMemberKind targetKind)
    {
        var copiableAttributes = new List<string>();
        var attributeNamespaces = new HashSet<string>();

        foreach (var attr in member.GetAttributes())
        {
            if (attr.AttributeClass == null) continue;

            var attributeFullName = attr.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (attributeFullName.StartsWith("global::System.Runtime.CompilerServices.")) continue;

            if (attributeFullName == "global::System.ComponentModel.DataAnnotations.ValidationAttribute") continue;

            if (IsSourceGeneratorTriggerAttribute(attributeFullName)) continue;

            var attributeTargets = GetAttributeTargets(attr.AttributeClass);
            if (targetKind == FacetMemberKind.Property && !attributeTargets.HasFlag(AttributeTargets.Property)) continue;
            if (targetKind == FacetMemberKind.Field && !attributeTargets.HasFlag(AttributeTargets.Field)) continue;

            var attributeSyntax = GenerateAttributeSyntax(attr);
            if (!string.IsNullOrWhiteSpace(attributeSyntax))
            {
                copiableAttributes.Add(attributeSyntax);

                var ns = ExtractNamespaceFromAttributeFullName(attributeFullName);
                if (!string.IsNullOrWhiteSpace(ns))
                {
                    attributeNamespaces.Add(ns!);
                }
            }
        }

        return (copiableAttributes, attributeNamespaces);
    }

    /// <summary>
    /// Extracts the namespace from a fully qualified attribute name.
    /// E.g., "global::System.ComponentModel.DataAnnotations.Schema.ColumnAttribute" -> "System.ComponentModel.DataAnnotations.Schema"
    /// </summary>
    private static string? ExtractNamespaceFromAttributeFullName(string attributeFullName)
    {
        var name = GeneratorUtilities.StripGlobalPrefix(attributeFullName);

        var genericIndex = name.IndexOf('<');
        if (genericIndex > 0)
        {
            name = name.Substring(0, genericIndex);
        }

        var lastDotIndex = name.LastIndexOf('.');
        if (lastDotIndex > 0)
        {
            return name.Substring(0, lastDotIndex);
        }

        return null;
    }

    /// <summary>
    /// Gets the AttributeTargets for an attribute type symbol.
    /// </summary>
    private static AttributeTargets GetAttributeTargets(INamedTypeSymbol attributeType)
    {
        var attributeUsage = attributeType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.AttributeUsageAttribute");

        if (attributeUsage != null && attributeUsage.ConstructorArguments.Length > 0)
        {
            var targets = attributeUsage.ConstructorArguments[0];
            if (targets.Value is int targetValue)
            {
                return (AttributeTargets)targetValue;
            }
        }

        return AttributeTargets.All;
    }

    /// <summary>
    /// Generates the C# syntax for an attribute from AttributeData.
    /// </summary>
    private static string GenerateAttributeSyntax(AttributeData attribute)
    {
        var sb = new StringBuilder();

        var attributeName = attribute.AttributeClass!.Name;
        if (attributeName.EndsWith("Attribute") && attributeName.Length > 9)
        {
            attributeName = attributeName.Substring(0, attributeName.Length - 9);
        }

        sb.Append($"[{attributeName}");

        if (attribute.AttributeClass.IsGenericType)
        {
            sb.Append("<");
            var typeArguments = attribute.AttributeClass.TypeArguments;
            var typeArgumentStrings = new List<string>();

            foreach (var typeArg in typeArguments)
            {
                typeArgumentStrings.Add(typeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }

            sb.Append(string.Join(", ", typeArgumentStrings));
            sb.Append(">");
        }

        var hasArguments = attribute.ConstructorArguments.Length > 0 || attribute.NamedArguments.Length > 0;

        if (hasArguments)
        {
            sb.Append("(");
            var arguments = new List<string>();

            foreach (var arg in attribute.ConstructorArguments)
            {
                arguments.Add(FormatTypedConstant(arg));
            }

            foreach (var namedArg in attribute.NamedArguments)
            {
                arguments.Add($"{namedArg.Key} = {FormatTypedConstant(namedArg.Value)}");
            }

            sb.Append(string.Join(", ", arguments));
            sb.Append(")");
        }

        sb.Append("]");
        return sb.ToString();
    }

    /// <summary>
    /// Formats a TypedConstant value for attribute syntax generation.
    /// </summary>
    private static string FormatTypedConstant(TypedConstant constant)
    {
        if (constant.IsNull)
            return "null";

        switch (constant.Kind)
        {
            case TypedConstantKind.Primitive:
                if (constant.Value is string strValue)
                {
                    var escaped = strValue.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    return $"\"{escaped}\"";
                }
                if (constant.Value is bool boolValue)
                    return boolValue ? "true" : "false";
                if (constant.Value is char charValue)
                    return $"'{charValue}'";
                if (constant.Value is double doubleValue)
                    return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "d";
                if (constant.Value is float floatValue)
                    return floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f";
                if (constant.Value is decimal decimalValue)
                    return decimalValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
                return constant.Value?.ToString() ?? "null";

            case TypedConstantKind.Enum:
                return FormatEnumConstant(constant);

            case TypedConstantKind.Type:
                if (constant.Value is ITypeSymbol typeValue)
                    return $"typeof({typeValue.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
                return "null";

            case TypedConstantKind.Array:
                var arrayElements = constant.Values.Select(FormatTypedConstant);
                return $"new[] {{ {string.Join(", ", arrayElements)} }}";

            default:
                return constant.Value?.ToString() ?? "null";
        }
    }

    /// <summary>
    /// Formats an enum TypedConstant value for attribute syntax generation.
    /// Resolves the enum member name from the underlying value.
    /// </summary>
    private static string FormatEnumConstant(TypedConstant constant)
    {
        var enumType = constant.Type as INamedTypeSymbol;
        if (enumType == null)
            return constant.Value?.ToString() ?? "0";

        var enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var underlyingValue = constant.Value;

        if (enumType.GetAttributes().Any(a => a.AttributeClass?.Name == "FlagsAttribute"))
        {
            return FormatFlagsEnumConstant(enumType, enumTypeName, underlyingValue);
        }

        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol field && field.HasConstantValue)
            {
                if (AreEnumValuesEqual(field.ConstantValue, underlyingValue))
                {
                    return $"{enumTypeName}.{field.Name}";
                }
            }
        }

        return $"({enumTypeName}){underlyingValue}";
    }

    /// <summary>
    /// Formats a flags enum value that may be a combination of multiple members.
    /// </summary>
    private static string FormatFlagsEnumConstant(INamedTypeSymbol enumType, string enumTypeName, object? underlyingValue)
    {
        if (underlyingValue == null)
            return $"({enumTypeName})0";

        var longValue = Convert.ToInt64(underlyingValue);

        if (longValue == 0)
        {
            foreach (var member in enumType.GetMembers())
            {
                if (member is IFieldSymbol field && field.HasConstantValue)
                {
                    if (Convert.ToInt64(field.ConstantValue) == 0)
                    {
                        return $"{enumTypeName}.{field.Name}";
                    }
                }
            }
            return $"({enumTypeName})0";
        }

        var membersWithValues = new List<(string Name, long Value)>();
        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol field && field.HasConstantValue)
            {
                var fieldValue = Convert.ToInt64(field.ConstantValue);
                if (fieldValue != 0) 
                {
                    membersWithValues.Add((field.Name, fieldValue));
                }
            }
        }

        membersWithValues.Sort((a, b) => b.Value.CompareTo(a.Value));

        var matchedMembers = new List<string>();
        var remainingValue = longValue;

        foreach (var (name, value) in membersWithValues)
        {
            if (value == longValue)
            {
                return $"{enumTypeName}.{name}";
            }
        }

        foreach (var (name, value) in membersWithValues)
        {
            if (value != 0 && (remainingValue & value) == value)
            {
                matchedMembers.Add($"{enumTypeName}.{name}");
                remainingValue &= ~value;
            }
        }

        if (remainingValue == 0 && matchedMembers.Count > 0)
        {
            return string.Join(" | ", matchedMembers);
        }

        return $"({enumTypeName}){underlyingValue}";
    }

    /// <summary>
    /// Compares two enum underlying values for equality, handling different numeric types.
    /// </summary>
    private static bool AreEnumValuesEqual(object? fieldValue, object? constantValue)
    {
        if (fieldValue == null || constantValue == null)
            return fieldValue == constantValue;

        try
        {
            var fieldLong = Convert.ToInt64(fieldValue);
            var constantLong = Convert.ToInt64(constantValue);
            return fieldLong == constantLong;
        }
        catch
        {
            return fieldValue.Equals(constantValue);
        }
    }

    /// <summary>
    /// Namespace prefixes of attributes that are consumed by other source generators to trigger
    /// code generation. Copying these to a Facet-generated DTO would cause analyzer errors
    /// because the DTO is not set up for that source generator's pipeline.
    /// See GitHub issue #277.
    /// </summary>
    private static readonly string[] SourceGeneratorTriggerNamespacePrefixes = new[]
    {
        "global::CommunityToolkit.Mvvm.",
    };

    /// <summary>
    /// Determines whether an attribute is a source-generator-triggering attribute that should
    /// not be copied to generated DTOs. These attributes are consumed by other source generators
    /// and copying them would cause analyzer errors on the target type.
    /// </summary>
    private static bool IsSourceGeneratorTriggerAttribute(string attributeFullName)
    {
        foreach (var prefix in SourceGeneratorTriggerNamespacePrefixes)
        {
            if (attributeFullName.StartsWith(prefix))
                return true;
        }

        return false;
    }
}
