using Facet.Generators.Shared;
using Facet.Generators;
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
        var ignoreNestedIds = GetNamedArg(attribute.NamedArguments, "IgnoreNestedIds", false);
        var ignoreForeignKeyClashes = GetNamedArg(attribute.NamedArguments, "IgnoreForeignKeyClashes", false);
        var includeCollections = GetNamedArg(attribute.NamedArguments, "IncludeCollections", false);

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
            ignoreNestedIds,
            ignoreForeignKeyClashes,
            includeCollections,
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
        bool ignoreNestedIds,
        bool ignoreForeignKeyClashes,
        bool includeCollections,
        CancellationToken token)
    {
        var properties = new List<FlattenProperty>();
        var seenNames = new HashSet<string>();
        var collectionTypeCache = new Dictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);
        var leafTypeCache = new Dictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);

        // Collect potential foreign keys and their navigation properties for clash detection
        var foreignKeyPaths = ignoreForeignKeyClashes
            ? CollectAllForeignKeyPaths(sourceType, includeFields, namingStrategy, new List<string>(), new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), collectionTypeCache, 0, maxDepth)
            : new HashSet<string>();

        // For SmartLeaf, we need to identify collisions first
        var collidingLeafNames = new HashSet<string>();
        if (namingStrategy == FlattenNamingStrategy.SmartLeaf)
        {
            collidingLeafNames = IdentifyCollidingLeafNames(
                sourceType,
                excludedPaths,
                maxDepth,
                includeFields,
                ignoreNestedIds,
                ignoreForeignKeyClashes,
                foreignKeyPaths,
                includeCollections,
                collectionTypeCache,
                leafTypeCache,
                token);
        }

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
            ignoreNestedIds,
            ignoreForeignKeyClashes,
            foreignKeyPaths,
            includeCollections,
            properties,
            seenNames,
            collidingLeafNames,
            new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default),
            collectionTypeCache,
            leafTypeCache,
            token);

        return properties.ToImmutableArray();
    }

    private static HashSet<string> IdentifyCollidingLeafNames(
        INamedTypeSymbol sourceType,
        HashSet<string> excludedPaths,
        int maxDepth,
        bool includeFields,
        bool ignoreNestedIds,
        bool ignoreForeignKeyClashes,
        HashSet<string> foreignKeyPaths,
        bool includeCollections,
        Dictionary<ITypeSymbol, bool> collectionTypeCache,
        Dictionary<ITypeSymbol, bool> leafTypeCache,
        CancellationToken token)
    {
        var leafNameCounts = new Dictionary<string, int>();

        CollectLeafNamesRecursive(
            sourceType,
            "",
            new List<string>(),
            0,
            maxDepth,
            excludedPaths,
            includeFields,
            ignoreNestedIds,
            ignoreForeignKeyClashes,
            foreignKeyPaths,
            includeCollections,
            leafNameCounts,
            new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default),
            collectionTypeCache,
            leafTypeCache,
            token);

        // Return only names that appear more than once
        return new HashSet<string>(leafNameCounts
            .Where(kvp => kvp.Value > 1)
            .Select(kvp => kvp.Key));
    }

    private static void CollectLeafNamesRecursive(
        ITypeSymbol currentType,
        string currentPath,
        List<string> pathSegments,
        int depth,
        int maxDepth,
        HashSet<string> excludedPaths,
        bool includeFields,
        bool ignoreNestedIds,
        bool ignoreForeignKeyClashes,
        HashSet<string> foreignKeyPaths,
        bool includeCollections,
        Dictionary<string, int> leafNameCounts,
        HashSet<ITypeSymbol> visitedTypes,
        Dictionary<ITypeSymbol, bool> collectionTypeCache,
        Dictionary<ITypeSymbol, bool> leafTypeCache,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (maxDepth > 0 && depth >= maxDepth) return;
        if (depth >= 10) return;

        if (!visitedTypes.Add(currentType)) return;

        if (currentType is not INamedTypeSymbol namedType) return;

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

            var newPath = string.IsNullOrEmpty(currentPath) ? memberName : $"{currentPath}.{memberName}";

            if (IsExcluded(newPath, excludedPaths)) continue;

            if (ignoreNestedIds && IsIdProperty(memberName))
            {
                if (depth > 0 || memberName != "Id")
                {
                    continue;
                }
            }

            var newPathSegments = new List<string>(pathSegments) { memberName };
            var leafName = memberName;

            // For SmartLeaf, use LeafOnly naming to identify collisions
            var flattenedName = GenerateFlattenedName(newPathSegments, FlattenNamingStrategy.LeafOnly);

            if (ignoreForeignKeyClashes && depth > 0 && foreignKeyPaths.Contains(flattenedName))
            {
                continue;
            }

            if (IsCollectionType(memberType, collectionTypeCache))
            {
                // If includeCollections is true, count it as a leaf for collision detection
                if (includeCollections)
                {
                    if (leafNameCounts.ContainsKey(leafName))
                    {
                        leafNameCounts[leafName]++;
                    }
                    else
                    {
                        leafNameCounts[leafName] = 1;
                    }
                }
                continue;
            }

            if (ShouldFlattenAsLeaf(memberType, leafTypeCache))
            {
                if (leafNameCounts.ContainsKey(leafName))
                {
                    leafNameCounts[leafName]++;
                }
                else
                {
                    leafNameCounts[leafName] = 1;
                }
            }
            else
            {
                CollectLeafNamesRecursive(
                    memberType,
                    newPath,
                    newPathSegments,
                    depth + 1,
                    maxDepth,
                    excludedPaths,
                    includeFields,
                    ignoreNestedIds,
                    ignoreForeignKeyClashes,
                    foreignKeyPaths,
                    includeCollections,
                    leafNameCounts,
                    visitedTypes,
                    collectionTypeCache,
                    leafTypeCache,
                    token);
            }
        }

        visitedTypes.Remove(currentType);
    }

    private static HashSet<string> CollectAllForeignKeyPaths(
        INamedTypeSymbol currentType,
        bool includeFields,
        FlattenNamingStrategy namingStrategy,
        List<string> currentPath,
        HashSet<ITypeSymbol> visitedTypes,
        Dictionary<ITypeSymbol, bool> collectionTypeCache,
        int depth,
        int maxDepth)
    {
        var foreignKeyPaths = new HashSet<string>();

        // Check depth limits
        if (maxDepth > 0 && depth >= maxDepth) return foreignKeyPaths;
        if (depth >= 10) return foreignKeyPaths; // Safety limit

        // Prevent infinite recursion
        if (!visitedTypes.Add(currentType)) return foreignKeyPaths;

        var members = includeFields
            ? currentType.GetMembers().Where(m => m is IPropertySymbol or IFieldSymbol)
            : currentType.GetMembers().OfType<IPropertySymbol>();

        var allPropertyNames = new HashSet<string>();
        var propertyTypes = new Dictionary<string, ITypeSymbol>();

        foreach (var member in members)
        {
            if (member.DeclaredAccessibility != Accessibility.Public) continue;

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

            allPropertyNames.Add(member.Name);
            propertyTypes[member.Name] = memberType;
        }

        foreach (var member in members)
        {
            if (member.DeclaredAccessibility != Accessibility.Public) continue;

            var memberName = member.Name;
            var memberType = propertyTypes[memberName];

            // Check if this is a FK pattern
            if (memberName.EndsWith("Id") && memberName.Length > 2)
            {
                var underlyingType = memberType;
                if (memberType is INamedTypeSymbol named && named.IsGenericType &&
                    named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
                {
                    underlyingType = named.TypeArguments[0];
                }

                if (underlyingType.IsValueType)
                {
                    // Check if there's a matching navigation property
                    var potentialNavProp = memberName.Substring(0, memberName.Length - 2);
                    if (allPropertyNames.Contains(potentialNavProp))
                    {
                        // This is a forein key!
                        var fkPath = new List<string>(currentPath) { memberName };
                        var flattenedFkName = GenerateFlattenedName(fkPath, namingStrategy);
                        foreignKeyPaths.Add(flattenedFkName);
                    }
                }
            }

            if (IsCollectionType(memberType, collectionTypeCache))
            {
                continue; // Skip collections
            }

            if (memberType.TypeKind == TypeKind.Class && memberType.SpecialType == SpecialType.None)
            {
                var newPath = new List<string>(currentPath) { memberName };
                var nestedFks = CollectAllForeignKeyPaths(
                    (INamedTypeSymbol)memberType,
                    includeFields,
                    namingStrategy,
                    newPath,
                    visitedTypes,
                    collectionTypeCache,
                    depth + 1,
                    maxDepth);

                foreignKeyPaths.UnionWith(nestedFks);
            }
        }

        visitedTypes.Remove(currentType);
        return foreignKeyPaths;
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
        bool ignoreNestedIds,
        bool ignoreForeignKeyClashes,
        HashSet<string> foreignKeyPaths,
        bool includeCollections,
        List<FlattenProperty> properties,
        HashSet<string> seenNames,
        HashSet<string> collidingLeafNames,
        HashSet<ITypeSymbol> visitedTypes,
        Dictionary<ITypeSymbol, bool> collectionTypeCache,
        Dictionary<ITypeSymbol, bool> leafTypeCache,
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

            // Check if this is an ID property that should be ignored
            if (ignoreNestedIds && IsIdProperty(memberName))
            {
                // At root level (depth == 0): only keep the exact "Id" property, exclude foreign keys (e.g., "CustomerId")
                // At nested levels (depth > 0): exclude all ID properties
                if (depth > 0 || memberName != "Id")
                {
                    continue;
                }
            }

            var newPathSegments = new List<string>(pathSegments) { memberName };

            var flattenedName = GenerateFlattenedName(newPathSegments, namingStrategy, collidingLeafNames);

            if (ignoreForeignKeyClashes && depth > 0 && foreignKeyPaths.Contains(flattenedName))
            {
                // This property would clash with a fk property
                // Skip it to avoid duplication
                continue;
            }

            bool isCollection = IsCollectionType(memberType, collectionTypeCache);
            if (isCollection)
            {
                // If includeCollections is enabled, add collection properties as-is
                if (includeCollections)
                {
                    if (seenNames.Contains(flattenedName))
                    {
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

                    // Use the exact type name from the source - collections are included as-is
                    var typeName = GeneratorUtilities.GetTypeNameWithNullability(memberType);

                    // Create flattened property for the collection
                    properties.Add(new FlattenProperty(
                        flattenedName,
                        typeName,
                        newPath,
                        newPathSegments.ToImmutableArray(),
                        false, // Collections are reference types
                        xmlDoc,
                        true)); // Mark as collection
                }
                continue;
            }

            if (ShouldFlattenAsLeaf(memberType, leafTypeCache))
            {
                if (seenNames.Contains(flattenedName))
                {
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

                // Determine the type name - nested value types need to be nullable because of ?. operators
                string typeName;
                if (depth > 0 && memberType.IsValueType && memberType.NullableAnnotation != NullableAnnotation.Annotated)
                {
                    // Make nested value types nullable (e.g., Customer.Id becomes int? not int)
                    typeName = $"{GeneratorUtilities.GetTypeNameWithNullability(memberType)}?";
                }
                else
                {
                    typeName = GeneratorUtilities.GetTypeNameWithNullability(memberType);
                }

                // Create flattened property
                properties.Add(new FlattenProperty(
                    flattenedName,
                    typeName,
                    newPath,
                    newPathSegments.ToImmutableArray(),
                    memberType.IsValueType,
                    xmlDoc,
                    false)); // Not a collection
            }
            else
            {
                // Recurse into complex type (reuse visitedTypes for better performance)
                DiscoverPropertiesRecursive(
                    memberType,
                    newPath,
                    newPathSegments,
                    depth + 1,
                    maxDepth,
                    excludedPaths,
                    namingStrategy,
                    includeFields,
                    ignoreNestedIds,
                    ignoreForeignKeyClashes,
                    foreignKeyPaths,
                    includeCollections,
                    properties,
                    seenNames,
                    collidingLeafNames,
                    visitedTypes,
                    collectionTypeCache,
                    leafTypeCache,
                    token);
            }
        }

        visitedTypes.Remove(currentType);
    }

    private static bool ShouldFlattenAsLeaf(ITypeSymbol type, Dictionary<ITypeSymbol, bool> cache)
    {
        // Check cache first
        if (cache.TryGetValue(type, out var cachedResult))
        {
            return cachedResult;
        }

        bool result = ShouldFlattenAsLeafCore(type);
        cache[type] = result;
        return result;
    }

    private static bool ShouldFlattenAsLeafCore(ITypeSymbol type)
    {
        // Special types - fast path for primitives
        var specialType = type.SpecialType;
        if (specialType != SpecialType.None && specialType != SpecialType.System_Object)
        {
            return true; // string, int, bool, etc.
        }

        // Enums - fast path
        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        // Note: Collections are already filtered out before this method is called

        // Value types (struct, DateTime, Guid, etc.) but not complex nested structs
        if (type.IsValueType)
        {
            // Check for common value types by namespace and name (faster than ToDisplayString)
            var ns = type.ContainingNamespace;
            if (ns?.Name == "System" && ns.ContainingNamespace?.IsGlobalNamespace == true)
            {
                var name = type.Name;
                if (name == "DateTime" ||
                    name == "DateTimeOffset" ||
                    name == "TimeSpan" ||
                    name == "Guid" ||
                    name == "Decimal")
                {
                    return true;
                }
            }

            // For other value types, check if they're simple (few properties)
            // This is expensive, so only do it if necessary
            if (type is INamedTypeSymbol namedValueType)
            {
                // Quick check: if it has no members, it's simple
                var members = namedValueType.GetMembers();
                if (members.Length == 0)
                {
                    return true;
                }

                // Count properties (cached by Roslyn)
                var propertyCount = 0;
                foreach (var member in members)
                {
                    if (member is IPropertySymbol)
                    {
                        propertyCount++;
                        if (propertyCount > 2) // Early exit
                        {
                            return false;
                        }
                    }
                }
                return propertyCount <= 2;
            }
        }

        return false;
    }

    private static bool IsCollectionType(ITypeSymbol type, Dictionary<ITypeSymbol, bool> cache)
    {
        // Check cache first
        if (cache.TryGetValue(type, out var cachedResult))
        {
            return cachedResult;
        }

        bool result = IsCollectionTypeCore(type);
        cache[type] = result;
        return result;
    }

    private static bool IsCollectionTypeCore(ITypeSymbol type)
    {
        // Quick check: string is not a collection even though it's IEnumerable<char>
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        // Arrays are collections
        if (type.TypeKind == TypeKind.Array)
        {
            return true;
        }

        // Check the type name itself first (e.g., List<T>, IEnumerable<T>)
        // This is much faster than checking namespace or interfaces
        var typeName = type.Name;
        if (typeName.StartsWith("IEnumerable") ||
            typeName.StartsWith("ICollection") ||
            typeName.StartsWith("IList") ||
            typeName.StartsWith("IReadOnlyCollection") ||
            typeName.StartsWith("IReadOnlyList") ||
            typeName.StartsWith("List") ||
            typeName.StartsWith("HashSet") ||
            typeName.StartsWith("Dictionary") ||
            typeName.StartsWith("Collection"))
        {
            return true;
        }

        // Check namespace - most collections are in System.Collections
        var ns = type.ContainingNamespace;
        while (ns != null && !ns.IsGlobalNamespace)
        {
            if (ns.Name == "Collections")
            {
                return true;
            }
            ns = ns.ContainingNamespace;
        }

        // Only check interfaces as absolute last resort - this is very expensive
        // Only do this for types we haven't already identified
        if (type is INamedTypeSymbol namedType && namedType.AllInterfaces.Length > 0)
        {
            foreach (var iface in namedType.AllInterfaces)
            {
                var ifaceName = iface.Name;
                if (ifaceName.StartsWith("IEnumerable") ||
                    ifaceName.StartsWith("ICollection") ||
                    ifaceName.StartsWith("IList"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsIdProperty(string propertyName)
    {
        // Check if property is named "Id" or ends with "Id"
        // Note: This includes both the entity's own Id and foreign keys like CustomerId, CompanyId, etc.
        return propertyName == "Id" || propertyName.EndsWith("Id");
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

    private static string GenerateFlattenedName(List<string> pathSegments, FlattenNamingStrategy strategy, HashSet<string>? collidingLeafNames = null)
    {
        if (strategy == FlattenNamingStrategy.LeafOnly)
        {
            return pathSegments.Last();
        }

        if (strategy == FlattenNamingStrategy.SmartLeaf)
        {
            var leafName = pathSegments.Last();

            // If this leaf name collides with another, use parent + leaf
            if (collidingLeafNames != null && collidingLeafNames.Contains(leafName) && pathSegments.Count >= 2)
            {
                // Use immediate parent + leaf name
                var parentName = pathSegments[pathSegments.Count - 2];
                return parentName + leafName;
            }

            // No collision, use leaf only
            return leafName;
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

        // Handle enum types - Roslyn returns enums as their underlying int value
        if (typeof(T).IsEnum && value is int intValue)
        {
            return (T)(object)intValue;
        }

        return defaultValue;
    }
}
