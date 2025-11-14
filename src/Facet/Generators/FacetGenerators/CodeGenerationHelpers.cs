using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Facet.Generators;

/// <summary>
/// Provides utility methods for code generation tasks.
/// </summary>
internal static class CodeGenerationHelpers
{
    /// <summary>
    /// Collects all namespaces that need to be imported based on the types used in the model.
    /// </summary>
    public static HashSet<string> CollectNamespaces(FacetTargetModel model)
    {
        var namespaces = new HashSet<string>
        {
            "System",
            "System.Linq",
            "System.Linq.Expressions"
        };

        // Add System.ComponentModel.DataAnnotations if any member has attributes
        if (model.CopyAttributes && model.Members.Any(m => m.Attributes.Count > 0))
        {
            namespaces.Add("System.ComponentModel.DataAnnotations");
        }

        // If the source type is nested in another type, don't add it as a regular namespace
        // It will be handled by CollectStaticUsingTypes instead
        if (model.SourceContainingTypes.Length == 0)
        {
            var sourceTypeNamespace = ExtractNamespaceFromFullyQualifiedType(model.SourceTypeName);
            if (!string.IsNullOrWhiteSpace(sourceTypeNamespace))
            {
                namespaces.Add(sourceTypeNamespace!);
            }
        }

        foreach (var member in model.Members)
        {
            var memberTypeNamespace = ExtractNamespaceFromFullyQualifiedType(member.TypeName);
            if (!string.IsNullOrWhiteSpace(memberTypeNamespace))
            {
                namespaces.Add(memberTypeNamespace!);
            }
        }

        if (!string.IsNullOrWhiteSpace(model.ConfigurationTypeName))
        {
            var configNamespace = ExtractNamespaceFromFullyQualifiedType(model.ConfigurationTypeName!);
            if (!string.IsNullOrWhiteSpace(configNamespace))
            {
                namespaces.Add(configNamespace!);
            }
        }

        if (!string.IsNullOrWhiteSpace(model.Namespace))
        {
            namespaces.Remove(model.Namespace!);
        }

        namespaces.Remove("");

        return namespaces;
    }

    /// <summary>
    /// Collects types that need 'using static' directives (for types nested in other types).
    /// </summary>
    public static HashSet<string> CollectStaticUsingTypes(FacetTargetModel model)
    {
        var staticUsingTypes = new HashSet<string>();

        // If the source type is nested within another type, we need 'using static' for the containing type
        if (model.SourceContainingTypes.Length > 0)
        {
            // Build the fully qualified containing type path
            // Example: if source is Application.Example1.Foo.Bar
            // and SourceContainingTypes is ["Foo"]
            // we need to extract "Application.Example1.Foo"

            var sourceTypeName = model.SourceTypeName;

            // Remove global:: prefix if present
            if (sourceTypeName.StartsWith("global::"))
            {
                sourceTypeName = sourceTypeName.Substring(8);
            }

            // Remove generic parameters if present
            var genericIndex = sourceTypeName.IndexOf('<');
            if (genericIndex > 0)
            {
                sourceTypeName = sourceTypeName.Substring(0, genericIndex);
            }

            // Remove nullable marker if present
            if (sourceTypeName.EndsWith("?"))
            {
                sourceTypeName = sourceTypeName.Substring(0, sourceTypeName.Length - 1);
            }

            // Remove the last part (the nested type name itself) to get the containing type
            var lastDotIndex = sourceTypeName.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                var containingType = sourceTypeName.Substring(0, lastDotIndex);
                staticUsingTypes.Add(containingType);
            }
        }

        return staticUsingTypes;
    }

    /// <summary>
    /// Collects all namespaces that need to be imported for wrapper types.
    /// </summary>
    public static HashSet<string> CollectNamespacesForWrapper(WrapperTargetModel model)
    {
        var namespaces = new HashSet<string>
        {
            "System"
        };

        // Add System.ComponentModel.DataAnnotations if any member has attributes
        if (model.CopyAttributes && model.Members.Any(m => m.Attributes.Count > 0))
        {
            namespaces.Add("System.ComponentModel.DataAnnotations");
        }

        // Add the source type namespace
        if (model.SourceContainingTypes.Length == 0)
        {
            var sourceTypeNamespace = ExtractNamespaceFromFullyQualifiedType(model.SourceTypeName);
            if (!string.IsNullOrWhiteSpace(sourceTypeNamespace))
            {
                namespaces.Add(sourceTypeNamespace!);
            }
        }

        // Add namespaces for member types
        foreach (var member in model.Members)
        {
            var memberTypeNamespace = ExtractNamespaceFromFullyQualifiedType(member.TypeName);
            if (!string.IsNullOrWhiteSpace(memberTypeNamespace))
            {
                namespaces.Add(memberTypeNamespace!);
            }
        }

        // Remove the wrapper's own namespace
        if (!string.IsNullOrWhiteSpace(model.Namespace))
        {
            namespaces.Remove(model.Namespace!);
        }

        namespaces.Remove("");

        return namespaces;
    }

    /// <summary>
    /// Extracts the namespace from a fully qualified type name (e.g., "global::System.String" -> "System").
    /// </summary>
    public static string? ExtractNamespaceFromFullyQualifiedType(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedTypeName))
            return null;

        // Remove global:: prefix if present
        var typeName = fullyQualifiedTypeName.StartsWith("global::")
            ? fullyQualifiedTypeName.Substring(8)
            : fullyQualifiedTypeName;

        var genericIndex = typeName.IndexOf('<');
        if (genericIndex > 0)
        {
            typeName = typeName.Substring(0, genericIndex);
        }

        if (typeName.EndsWith("?"))
        {
            typeName = typeName.Substring(0, typeName.Length - 1);
        }

        var lastDotIndex = typeName.LastIndexOf('.');
        if (lastDotIndex > 0)
        {
            return typeName.Substring(0, lastDotIndex);
        }

        return null;
    }

    /// <summary>
    /// Formats XML documentation comment into proper /// format for code generation.
    /// </summary>
    public static string FormatXmlDocumentation(string xmlDoc)
    {
        if (string.IsNullOrWhiteSpace(xmlDoc))
            return string.Empty;

        var lines = new List<string>();

        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xmlDoc);
            var root = doc.Root;

            if (root == null)
                return string.Empty;

            // Process summary
            var summary = root.Element("summary");
            if (summary != null)
            {
                lines.Add("/// <summary>");
                var summaryText = summary.Value.Trim();
                if (!string.IsNullOrEmpty(summaryText))
                {
                    foreach (var line in summaryText.Split('\n'))
                    {
                        lines.Add($"/// {line.Trim()}");
                    }
                }
                lines.Add("/// </summary>");
            }

            // Process value
            var value = root.Element("value");
            if (value != null)
            {
                lines.Add("/// <value>");
                var valueText = value.Value.Trim();
                if (!string.IsNullOrEmpty(valueText))
                {
                    foreach (var line in valueText.Split('\n'))
                    {
                        lines.Add($"/// {line.Trim()}");
                    }
                }
                lines.Add("/// </value>");
            }

            // Process remarks
            var remarks = root.Element("remarks");
            if (remarks != null)
            {
                lines.Add("/// <remarks>");
                var remarksText = remarks.Value.Trim();
                if (!string.IsNullOrEmpty(remarksText))
                {
                    foreach (var line in remarksText.Split('\n'))
                    {
                        lines.Add($"/// {line.Trim()}");
                    }
                }
                lines.Add("/// </remarks>");
            }

            // Process example
            var example = root.Element("example");
            if (example != null)
            {
                lines.Add("/// <example>");
                var exampleText = example.Value.Trim();
                if (!string.IsNullOrEmpty(exampleText))
                {
                    foreach (var line in exampleText.Split('\n'))
                    {
                        lines.Add($"/// {line.Trim()}");
                    }
                }
                lines.Add("/// </example>");
            }

            return lines.Count > 0 ? string.Join("\n", lines) : string.Empty;
        }
        catch
        {
            // If XML parsing fails, return empty string rather than crashing the generator
            return string.Empty;
        }
    }

    /// <summary>
    /// Extracts and formats XML documentation from a symbol.
    /// </summary>
    public static string? ExtractXmlDocumentation(ISymbol symbol)
    {
        var documentationComment = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(documentationComment))
            return null;

        return FormatXmlDocumentation(documentationComment!);
    }

    /// <summary>
    /// Gets the simple type name from a fully qualified type name.
    /// </summary>
    public static string GetSimpleTypeName(string fullyQualifiedTypeName)
    {
        var lastDotIndex = fullyQualifiedTypeName.LastIndexOf('.');
        if (lastDotIndex >= 0 && lastDotIndex < fullyQualifiedTypeName.Length - 1)
        {
            return fullyQualifiedTypeName.Substring(lastDotIndex + 1);
        }
        return fullyQualifiedTypeName;
    }

    /// <summary>
    /// Gets the indentation string for the current nesting level
    /// </summary>
    public static string GetIndentation(FacetTargetModel model)
    {
        return new string(' ', 4 * (model.ContainingTypes.Length + 1));
    }
}
