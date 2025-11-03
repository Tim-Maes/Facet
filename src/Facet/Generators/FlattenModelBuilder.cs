using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Facet.Generators;

/// <summary>
/// Builds FlattenTargetModel instances from Flatten attribute syntax contexts.
/// </summary>
internal static class FlattenModelBuilder
{
    /// <summary>
    /// Builds a FlattenTargetModel from the generator attribute syntax context.
    /// </summary>
    public static FlattenTargetModel? BuildModel(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol targetSymbol) return null;
        if (context.Attributes.Length == 0) return null;

        var attribute = context.Attributes[0];
        token.ThrowIfCancellationRequested();

        var sourceType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        if (sourceType == null) return null;

        // Extract attribute parameters
        var excludedPaths = ExtractExcludedPaths(attribute);
        var maxDepth = GetNamedArg(attribute.NamedArguments, "MaxDepth", 3);
        var namingStrategy = GetNamedArg(attribute.NamedArguments, "NamingStrategy", FlattenNamingStrategy.Prefix);
        var includeFields = GetNamedArg(attribute.NamedArguments, "IncludeFields", false);
        var generateParameterlessConstructor = GetNamedArg(attribute.NamedArguments, "GenerateParameterlessConstructor", true);
        var generateProjection = GetNamedArg(attribute.NamedArguments, "GenerateProjection", true);
        var useFullName = GetNamedArg(attribute.NamedArguments, "UseFullName", false);

        // Infer the type kind from the target type declaration
        var (typeKind, isRecord) = TypeAnalyzer.InferTypeKind(targetSymbol);

        // Extract type-level XML documentation
        var typeXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(sourceType);

        // Discover flattened properties
        var properties = DiscoverFlattenedProperties(
            sourceType,
            excludedPaths,
            maxDepth,
            namingStrategy,
            includeFields,
            token);

        // Determine full name
        string fullName = useFullName
            ? targetSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).GetSafeName()
            : targetSymbol.Name;

        var ns = targetSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : targetSymbol.ContainingNamespace.ToDisplayString();

        // Get containing types for nested classes
        var containingTypes = TypeAnalyzer.GetContainingTypes(targetSymbol);

        return new FlattenTargetModel(
            targetSymbol.Name,
            ns,
            fullName,
            typeKind,
            isRecord,
            generateParameterlessConstructor,
            generateProjection,
            sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            properties,
            typeXmlDocumentation,
            containingTypes,
            useFullName,
            namingStrategy,
            maxDepth);
    }

    private static HashSet<string> ExtractExcludedPaths(AttributeData attribute)
    {
        var excluded = new HashSet<string>();

        // Check constructor argument (params string[] exclude)
        if (attribute.ConstructorArguments.Length > 1)
        {
            var excludeArg = attribute.ConstructorArguments[1];
            if (!excludeArg.IsNull && excludeArg.Kind == TypedConstantKind.Array)
            {
                foreach (var item in excludeArg.Values)
                {
                    if (item.Value is string excludePath)
                    {
                        excluded.Add(excludePath);
                    }
                }
            }
        }

        // Check named argument
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "Exclude" && namedArg.Value.Kind == TypedConstantKind.Array)
            {
                foreach (var item in namedArg.Value.Values)
                {
                    if (item.Value is string excludePath)
                    {
                        excluded.Add(excludePath);
                    }
                }
            }
        }

        return excluded;
    }

    private static ImmutableArray<FlattenProperty> DiscoverFlattenedProperties(
        INamedTypeSymbol sourceType,
        HashSet<string> excludedPaths,
        int maxDepth,
        FlattenNamingStrategy namingStrategy,
        bool includeFields,
        CancellationToken token)
    {
        var properties = new List<FlattenProperty>();
        var seenNames = new HashSet<string>();

        // Start recursive discovery
        DiscoverPropertiesRecursive(
            sourceType,
            "",
            new List<string>(),
            0,
            maxDepth,
            excludedPaths,
            namingStrategy,
            includeFields,
            properties,
            seenNames,
            new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default),
            token);

        return properties.ToImmutableArray();
    }

    private static void DiscoverPropertiesRecursive(
        ITypeSymbol currentType,
        string currentPath,
        List<string> pathSegments,
        int depth,
        int maxDepth,
        HashSet<string> excludedPaths,
        FlattenNamingStrategy namingStrategy,
        bool includeFields,
        List<FlattenProperty> properties,
        HashSet<string> seenNames,
        HashSet<ITypeSymbol> visitedTypes,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        // Check max depth (0 means unlimited, but we cap at 10 for safety)
        if (maxDepth > 0 && depth >= maxDepth) return;
        if (depth >= 10) return; // Safety limit

        // Prevent infinite recursion
        if (!visitedTypes.Add(currentType)) return;

        if (currentType is not INamedTypeSymbol namedType) return;

        // Get all members
        var members = includeFields
            ? namedType.GetMembers().Where(m => m is IPropertySymbol or IFieldSymbol)
            : namedType.GetMembers().OfType<IPropertySymbol>();

        foreach (var member in members)
        {
            token.ThrowIfCancellationRequested();

            if (member.DeclaredAccessibility != Accessibility.Public) continue;

            var memberName = member.Name;
            ITypeSymbol memberType;

            if (member is IPropertySymbol property)
            {
                memberType = property.Type;
            }
            else if (member is IFieldSymbol field)
            {
                memberType = field.Type;
            }
            else
            {
                continue;
            }

            // Build the path for this member
            var newPath = string.IsNullOrEmpty(currentPath) ? memberName : $"{currentPath}.{memberName}";

            // Check if excluded
            if (IsExcluded(newPath, excludedPaths)) continue;

            var newPathSegments = new List<string>(pathSegments) { memberName };

            // Check if this is a "leaf" type (primitive, string, enum, value type that we should flatten)
            if (ShouldFlattenAsLeaf(memberType))
            {
                // Generate flattened property name
                var flattenedName = GenerateFlattenedName(newPathSegments, namingStrategy);

                // Handle name collisions
                if (seenNames.Contains(flattenedName))
                {
                    // Add numeric suffix
                    int counter = 2;
                    string uniqueName;
                    do
                    {
                        uniqueName = $"{flattenedName}{counter}";
                        counter++;
                    } while (seenNames.Contains(uniqueName));

                    flattenedName = uniqueName;
                }

                seenNames.Add(flattenedName);

                // Get XML documentation
                var xmlDoc = CodeGenerationHelpers.ExtractXmlDocumentation(member);

                // Create flattened property
                properties.Add(new FlattenProperty(
                    flattenedName,
                    GeneratorUtilities.GetTypeNameWithNullability(memberType),
                    newPath,
                    newPathSegments.ToImmutableArray(),
                    memberType.IsValueType,
                    xmlDoc));
            }
            else
            {
                // Recurse into complex type
                DiscoverPropertiesRecursive(
                    memberType,
                    newPath,
                    newPathSegments,
                    depth + 1,
                    maxDepth,
                    excludedPaths,
                    namingStrategy,
                    includeFields,
                    properties,
                    seenNames,
                    new HashSet<ITypeSymbol>(visitedTypes, SymbolEqualityComparer.Default),
                    token);
            }
        }

        visitedTypes.Remove(currentType);
    }

    private static bool ShouldFlattenAsLeaf(ITypeSymbol type)
    {
        // Special types
        if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object)
        {
            return true; // string, int, bool, etc.
        }

        // Enums
        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        // Value types (struct, DateTime, Guid, etc.) but not complex nested structs
        if (type.IsValueType)
        {
            // Common value types we want to flatten
            var typeName = type.ToDisplayString();
            if (typeName.StartsWith("System.DateTime") ||
                typeName.StartsWith("System.DateTimeOffset") ||
                typeName.StartsWith("System.TimeSpan") ||
                typeName.StartsWith("System.Guid") ||
                typeName.StartsWith("System.Decimal"))
            {
                return true;
            }

            // For other value types, check if they're simple (few properties)
            if (type is INamedTypeSymbol namedValueType)
            {
                var propertyCount = namedValueType.GetMembers().OfType<IPropertySymbol>().Count();
                // Only flatten value types with 0-2 properties
                return propertyCount <= 2;
            }
        }

        return false;
    }

    private static bool IsExcluded(string path, HashSet<string> excludedPaths)
    {
        // Check exact match
        if (excludedPaths.Contains(path)) return true;

        // Check if any parent path is excluded
        foreach (var excludedPath in excludedPaths)
        {
            if (path.StartsWith(excludedPath + ".")) return true;
        }

        return false;
    }

    private static string GenerateFlattenedName(List<string> pathSegments, FlattenNamingStrategy strategy)
    {
        if (strategy == FlattenNamingStrategy.LeafOnly)
        {
            return pathSegments.Last();
        }

        // Prefix strategy (default)
        return string.Join("", pathSegments);
    }

    private static T GetNamedArg<T>(ImmutableArray<KeyValuePair<string, TypedConstant>> namedArgs, string name, T defaultValue)
    {
        var arg = namedArgs.FirstOrDefault(x => x.Key == name);
        if (arg.Key == null) return defaultValue;

        var value = arg.Value.Value;
        if (value is T typedValue) return typedValue;

        return defaultValue;
    }
}
