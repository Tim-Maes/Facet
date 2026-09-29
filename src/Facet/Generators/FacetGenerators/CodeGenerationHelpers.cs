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
    /// Collects namespaces needed only for the <c>.Properties.g.cs</c> split file.
    /// Excludes mapping-only namespaces such as <c>System.Linq</c>,
    /// <c>System.Linq.Expressions</c>, the source type's namespace, and the
    /// configuration type's namespace — those are only required by the Mappings file.
    /// </summary>
    public static HashSet<string> CollectNamespacesForProperties(FacetTargetModel model)
    {
        var namespaces = new HashSet<string>();

        bool hasImmutableCollections = false;
        foreach (var member in model.Members)
        {
            foreach (var attrNamespace in member.AttributeNamespaces)
                if (!string.IsNullOrWhiteSpace(attrNamespace))
                    namespaces.Add(attrNamespace);

            if (member.CollectionWrapper != null && IsImmutableCollectionWrapper(member.CollectionWrapper))
                hasImmutableCollections = true;

            if (member.IsNestedFacet || member.IsNestedType)
                continue;

            var memberTypeNamespace = ExtractNamespaceFromFullyQualifiedType(member.TypeName);
            if (!string.IsNullOrWhiteSpace(memberTypeNamespace))
                namespaces.Add(memberTypeNamespace!);
        }

        if (hasImmutableCollections)
            namespaces.Add("System.Collections.Immutable");

        if (!string.IsNullOrWhiteSpace(model.Namespace))
            namespaces.Remove(model.Namespace!);

        namespaces.Remove("");
        return namespaces;
    }

    /// <summary>
    /// Collects <c>using static</c> directives needed only for the <c>.Properties.g.cs</c>
    /// split file. Excludes source-type containing-type entries (those are only needed by
    /// the Mappings file for constructor parameters and projection expressions).
    /// </summary>
    public static HashSet<string> CollectStaticUsingTypesForProperties(FacetTargetModel model)
    {
        var staticUsingTypes = new HashSet<string>();

        foreach (var member in model.Members)
        {
            if (member.IsNestedType && !member.IsNestedFacet)
            {
                var memberTypeName = GeneratorUtilities.StripGlobalPrefix(member.TypeName);

                var genericIndex = memberTypeName.IndexOf('<');
                if (genericIndex > 0)
                {
                    var genericEnd = memberTypeName.LastIndexOf('>');
                    if (genericEnd > genericIndex)
                        memberTypeName = GeneratorUtilities.StripGlobalPrefix(
                            memberTypeName.Substring(genericIndex + 1, genericEnd - genericIndex - 1));
                }

                if (memberTypeName.EndsWith("?"))
                    memberTypeName = memberTypeName.Substring(0, memberTypeName.Length - 1);

                var lastDotIndex = memberTypeName.LastIndexOf('.');
                if (lastDotIndex > 0)
                    staticUsingTypes.Add(memberTypeName.Substring(0, lastDotIndex));
            }
        }

        foreach (var member in model.Members)
        {
            if (member.IsNestedFacet)
            {
                var memberTypeName = GeneratorUtilities.StripGlobalPrefix(member.TypeName);

                var genericIndex = memberTypeName.IndexOf('<');
                if (genericIndex > 0)
                    memberTypeName = memberTypeName.Substring(0, genericIndex);

                if (memberTypeName.EndsWith("?"))
                    memberTypeName = memberTypeName.Substring(0, memberTypeName.Length - 1);

                var memberNamespace = ExtractNamespaceFromFullyQualifiedType(memberTypeName);
                string typeNameWithoutNamespace = !string.IsNullOrWhiteSpace(memberNamespace)
                    ? memberTypeName.Substring(memberNamespace.Length + 1)
                    : memberTypeName;

                if (typeNameWithoutNamespace.Contains('.'))
                {
                    var lastDotIndex = memberTypeName.LastIndexOf('.');
                    if (lastDotIndex > 0)
                        staticUsingTypes.Add(memberTypeName.Substring(0, lastDotIndex));
                }
            }
        }

        return staticUsingTypes;
    }

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

        if (model.SourceContainingTypes.Length == 0)
        {
            var sourceTypeNamespace = ExtractNamespaceFromFullyQualifiedType(model.SourceTypeName);
            if (!string.IsNullOrWhiteSpace(sourceTypeNamespace))
            {
                namespaces.Add(sourceTypeNamespace!);
            }
        }

        bool hasImmutableCollections = false;
        foreach (var member in model.Members)
        {
            foreach (var attrNamespace in member.AttributeNamespaces)
            {
                if (!string.IsNullOrWhiteSpace(attrNamespace))
                {
                    namespaces.Add(attrNamespace);
                }
            }

            if (member.CollectionWrapper != null && IsImmutableCollectionWrapper(member.CollectionWrapper))
            {
                hasImmutableCollections = true;
            }

            if (member.IsNestedFacet || member.IsNestedType)
            {
                continue;
            }

            var memberTypeNamespace = ExtractNamespaceFromFullyQualifiedType(member.TypeName);
            if (!string.IsNullOrWhiteSpace(memberTypeNamespace))
            {
                namespaces.Add(memberTypeNamespace!);
            }
        }

        if (hasImmutableCollections)
        {
            namespaces.Add("System.Collections.Immutable");
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

        if (model.SourceContainingTypes.Length > 0)
        {
            var sourceTypeName = model.SourceTypeName;

            sourceTypeName = GeneratorUtilities.StripGlobalPrefix(sourceTypeName);

            var genericIndex = sourceTypeName.IndexOf('<');
            if (genericIndex > 0)
            {
                sourceTypeName = sourceTypeName.Substring(0, genericIndex);
            }

            if (sourceTypeName.EndsWith("?"))
            {
                sourceTypeName = sourceTypeName.Substring(0, sourceTypeName.Length - 1);
            }

            var lastDotIndex = sourceTypeName.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                var containingType = sourceTypeName.Substring(0, lastDotIndex);
                staticUsingTypes.Add(containingType);
            }
        }

        foreach (var member in model.Members)
        {
            if (member.IsNestedType && !member.IsNestedFacet)
            {
                var memberTypeName = member.TypeName;

                memberTypeName = GeneratorUtilities.StripGlobalPrefix(memberTypeName);

                var genericIndex = memberTypeName.IndexOf('<');
                if (genericIndex > 0)
                {
                    var genericEnd = memberTypeName.LastIndexOf('>');
                    if (genericEnd > genericIndex)
                    {
                        memberTypeName = memberTypeName.Substring(genericIndex + 1, genericEnd - genericIndex - 1);
                        
                        memberTypeName = GeneratorUtilities.StripGlobalPrefix(memberTypeName);
                    }
                }

                if (memberTypeName.EndsWith("?"))
                {
                    memberTypeName = memberTypeName.Substring(0, memberTypeName.Length - 1);
                }

                var lastDotIndex = memberTypeName.LastIndexOf('.');
                if (lastDotIndex > 0)
                {
                    var containingType = memberTypeName.Substring(0, lastDotIndex);
                    staticUsingTypes.Add(containingType);
                }
            }
        }

        foreach (var member in model.Members)
        {
            if (member.IsNestedFacet)
            {
                var memberTypeName = member.TypeName;

                memberTypeName = GeneratorUtilities.StripGlobalPrefix(memberTypeName);

                var genericIndex = memberTypeName.IndexOf('<');
                if (genericIndex > 0)
                {
                    memberTypeName = memberTypeName.Substring(0, genericIndex);
                }

                if (memberTypeName.EndsWith("?"))
                {
                    memberTypeName = memberTypeName.Substring(0, memberTypeName.Length - 1);
                }

                var memberNamespace = ExtractNamespaceFromFullyQualifiedType(memberTypeName);

                string typeNameWithoutNamespace;
                if (!string.IsNullOrWhiteSpace(memberNamespace))
                {
                    typeNameWithoutNamespace = memberTypeName.Substring(memberNamespace.Length + 1);
                }
                else
                {
                    typeNameWithoutNamespace = memberTypeName;
                }

                if (typeNameWithoutNamespace.Contains('.'))
                {
                    var lastDotIndex = memberTypeName.LastIndexOf('.');
                    if (lastDotIndex > 0)
                    {
                        var containingType = memberTypeName.Substring(0, lastDotIndex);
                        staticUsingTypes.Add(containingType);
                    }
                }
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
            foreach (var attrNamespace in member.AttributeNamespaces)
            {
                if (!string.IsNullOrWhiteSpace(attrNamespace))
                {
                    namespaces.Add(attrNamespace);
                }
            }

            var memberTypeNamespace = ExtractNamespaceFromFullyQualifiedType(member.TypeName);
            if (!string.IsNullOrWhiteSpace(memberTypeNamespace))
            {
                namespaces.Add(memberTypeNamespace!);
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
    /// Extracts the namespace from a fully qualified type name (e.g., "global::System.String" -> "System").
    /// </summary>
    public static string? ExtractNamespaceFromFullyQualifiedType(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedTypeName))
            return null;

        var typeName = GeneratorUtilities.StripGlobalPrefix(fullyQualifiedTypeName);

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

            foreach (var elementName in new[] { "summary", "value", "remarks", "example" })
            {
                var element = root.Element(elementName);
                if (element != null)
                {
                    AppendDocumentationElement(lines, element);
                }
            }

            return lines.Count > 0 ? string.Join("\n", lines) : string.Empty;
        }
        catch (System.Xml.XmlException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Writes one documentation element as <c>///</c> lines, keeping its inline markup.
    /// </summary>
    /// <remarks>
    /// The content is copied as XML, not as <see cref="System.Xml.Linq.XElement.Value"/>: that property
    /// concatenates the text nodes only, so a <c>&lt;see cref="..."/&gt;</c> or
    /// <c>&lt;see langword="null"/&gt;</c> vanished from the sentence, <c>&lt;c&gt;</c> and
    /// <c>&lt;para&gt;</c> lost their markup, and an escaped <c>&amp;lt;</c> was written back unescaped,
    /// which is malformed XML in the generated comment.
    /// <para>
    /// Crefs stay resolvable: Roslyn hands out the comment with every <c>cref</c> already turned into a
    /// documentation ID (<c>P:Namespace.Type.Member</c>), and the compiler takes an ID-form cref verbatim
    /// instead of binding it in the facet's namespace — so a facet in another namespace than its source
    /// gets no CS1574 (the reason the text used to be flattened, #45).
    /// </para>
    /// <para>
    /// A <c>paramref</c> or <c>typeparamref</c> points at a parameter of the source member that the
    /// generated member does not have, so it becomes <c>&lt;c&gt;name&lt;/c&gt;</c>.
    /// </para>
    /// </remarks>
    private static void AppendDocumentationElement(List<string> lines, System.Xml.Linq.XElement element)
    {
        foreach (var reference in element.Descendants()
                     .Where(descendant => descendant.Name == "paramref" || descendant.Name == "typeparamref")
                     .ToList())
        {
            reference.ReplaceWith(new System.Xml.Linq.XElement("c", (string?)reference.Attribute("name") ?? string.Empty));
        }

        var content = string.Concat(element.Nodes().Select(node => node.ToString(System.Xml.Linq.SaveOptions.DisableFormatting))).Trim();

        lines.Add($"/// <{element.Name}>");
        if (!string.IsNullOrEmpty(content))
        {
            foreach (var line in content.Split('\n'))
            {
                lines.Add($"/// {line.Trim()}");
            }
        }
        lines.Add($"/// </{element.Name}>");
    }

    /// <summary>
    /// Extracts and formats XML documentation from a symbol.
    /// When <paramref name="inheritDocs"/> is true and the symbol has no usable
    /// documentation (no docs at all, or only <c>&lt;inheritdoc/&gt;</c>), falls back to
    /// base classes and then interfaces. The walk continues past hierarchy entries that
    /// themselves only contain <c>&lt;inheritdoc/&gt;</c>. Supported for both member
    /// symbols (properties and fields) and type symbols.
    /// </summary>
    public static string? ExtractXmlDocumentation(ISymbol symbol, bool inheritDocs = false)
    {
        return ExtractXmlDocumentationCore(symbol, inheritDocs, visited: null, externalDocProvider: null);
    }

    /// <summary>
    /// Extracts and formats XML documentation from a symbol, with fallback to external
    /// assembly XML documentation files when the standard Roslyn API returns empty.
    /// </summary>
    public static string? ExtractXmlDocumentation(ISymbol symbol, bool inheritDocs, ExternalXmlDocProvider? externalDocProvider)
    {
        return ExtractXmlDocumentationCore(symbol, inheritDocs, visited: null, externalDocProvider);
    }

    private static string? ExtractXmlDocumentationCore(ISymbol symbol, bool inheritDocs, HashSet<ISymbol>? visited, ExternalXmlDocProvider? externalDocProvider)
    {
        var rawXml = symbol.GetDocumentationCommentXml();
        var formatted = FormatXmlDocumentation(rawXml ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(formatted))
            return formatted;

        if (externalDocProvider != null)
        {
            var externalXml = externalDocProvider.GetDocumentationForSymbol(symbol);
            var externalFormatted = FormatXmlDocumentation(externalXml ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(externalFormatted))
                return externalFormatted;
        }

        if (!inheritDocs)
            return null;

        visited ??= new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (!visited.Add(symbol))
            return null;

        return symbol switch
        {
            IPropertySymbol or IFieldSymbol => WalkMemberHierarchy(symbol, visited, externalDocProvider),
            INamedTypeSymbol type => WalkTypeHierarchy(type, visited, externalDocProvider),
            _ => null
        };
    }

    private static string? WalkMemberHierarchy(ISymbol member, HashSet<ISymbol> visited, ExternalXmlDocProvider? externalDocProvider)
    {
        var containingType = member.ContainingType;
        if (containingType is null)
            return null;

        foreach (var ancestor in EnumerateBaseTypesThenInterfaces(containingType))
        {
            var match = ancestor.GetMembers(member.Name).FirstOrDefault(m => m.Kind == member.Kind);
            if (match is null)
                continue;

            var doc = ExtractXmlDocumentationCore(match, inheritDocs: true, visited, externalDocProvider);
            if (!string.IsNullOrWhiteSpace(doc))
                return doc;
        }

        return null;
    }

    private static string? WalkTypeHierarchy(INamedTypeSymbol type, HashSet<ISymbol> visited, ExternalXmlDocProvider? externalDocProvider)
    {
        foreach (var ancestor in EnumerateBaseTypesThenInterfaces(type))
        {
            var doc = ExtractXmlDocumentationCore(ancestor, inheritDocs: true, visited, externalDocProvider);
            if (!string.IsNullOrWhiteSpace(doc))
                return doc;
        }

        return null;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateBaseTypesThenInterfaces(INamedTypeSymbol type)
    {
        for (var baseType = type.BaseType;
             baseType is not null && baseType.SpecialType != SpecialType.System_Object;
             baseType = baseType.BaseType)
        {
            yield return baseType;
        }

        foreach (var iface in type.AllInterfaces)
            yield return iface;
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

    /// <summary>
    /// Determines if a collection wrapper represents an immutable collection type.
    /// </summary>
    private static bool IsImmutableCollectionWrapper(string collectionWrapper)
    {
        return collectionWrapper switch
        {
            FacetConstants.CollectionWrappers.ImmutableArray => true,
            FacetConstants.CollectionWrappers.ImmutableList => true,
            FacetConstants.CollectionWrappers.ImmutableHashSet => true,
            FacetConstants.CollectionWrappers.ImmutableSortedSet => true,
            FacetConstants.CollectionWrappers.ImmutableQueue => true,
            FacetConstants.CollectionWrappers.ImmutableStack => true,
            FacetConstants.CollectionWrappers.IImmutableList => true,
            FacetConstants.CollectionWrappers.IImmutableSet => true,
            FacetConstants.CollectionWrappers.IImmutableQueue => true,
            FacetConstants.CollectionWrappers.IImmutableStack => true,
            _ => false
        };
    }
}
