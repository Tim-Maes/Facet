using Facet.Generators.Shared;
using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        var excludedPaths = ExtractExcludedPaths(attribute, context);
        var maxDepth = GetNamedArg(attribute.NamedArguments, "MaxDepth", 3);
        var namingStrategy = GetNamedArg(attribute.NamedArguments, "NamingStrategy", FlattenNamingStrategy.Prefix);
        var includeFields = GetNamedArg(attribute.NamedArguments, "IncludeFields", false);
        var generateParameterlessConstructor = GetNamedArg(attribute.NamedArguments, "GenerateParameterlessConstructor", true);
        var generateProjection = GetNamedArg(attribute.NamedArguments, "GenerateProjection", true);
        var useFullName = GetNamedArg(attribute.NamedArguments, "UseFullName", false);
        var ignoreNestedIds = GetNamedArg(attribute.NamedArguments, "IgnoreNestedIds", false);
        var ignoreForeignKeyClashes = GetNamedArg(attribute.NamedArguments, "IgnoreForeignKeyClashes", false);
        var includeCollections = GetNamedArg(attribute.NamedArguments, "IncludeCollections", false);

        var (typeKind, isRecord) = TypeAnalyzer.InferTypeKind(targetSymbol);

        var externalDocProvider = new ExternalXmlDocProvider(context.SemanticModel.Compilation);

        var typeXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(sourceType, false, externalDocProvider);

        var properties = DiscoverFlattenedProperties(
            sourceType,
            excludedPaths,
            maxDepth,
            namingStrategy,
            includeFields,
            ignoreNestedIds,
            ignoreForeignKeyClashes,
            includeCollections,
            externalDocProvider,
            token);

        string fullName = useFullName
            ? targetSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).GetSafeName()
            : targetSymbol.Name;

        var ns = targetSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : targetSymbol.ContainingNamespace.ToDisplayString();

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

    private static HashSet<string> ExtractExcludedPaths(AttributeData attribute, GeneratorAttributeSyntaxContext? context = null)
    {
        var excluded = new HashSet<string>();

        var sourceType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        var sourceTypeName = sourceType?.Name ?? string.Empty;

        void ProcessExpression(ExpressionSyntax expr)
        {
            if (expr == null) return;

            try
            {
                var (resolved, hadLeadingAt) = NameOfResolver.ResolveExpression(expr);
                if (!string.IsNullOrEmpty(resolved))
                {
                    var path = resolved!;
                    if (hadLeadingAt && !string.IsNullOrEmpty(sourceTypeName) && path.StartsWith(sourceTypeName + "."))
                    {
                        path = path.Substring(sourceTypeName.Length + 1);
                    }
                    excluded.Add(path);
                    return;
                }
            }
            catch
            {
            }

            if (expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var val = lit.Token.ValueText;
                if (!string.IsNullOrEmpty(val)) excluded.Add(val);
                return;
            }

            var text = expr.ToString().Trim();
            if (text.Length == 0) return;

            if ((text.StartsWith("@\"") && text.EndsWith("\"")) || (text.StartsWith("\"") && text.EndsWith("\"")))
            {
                var firstQuote = text.IndexOf('"');
                if (firstQuote >= 0 && text.Length - firstQuote - 2 > 0)
                    text = text.Substring(firstQuote + 1, text.Length - firstQuote - 2);
                else
                    text = string.Empty;
            }

            if (!string.IsNullOrEmpty(text)) excluded.Add(text);
        }

        if (context != null && attribute.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax attrSyntax)
        {
            var positionalArgs = attrSyntax.ArgumentList?.Arguments.Where(a => a.NameEquals == null && a.NameColon == null).ToList();
            if (positionalArgs != null && positionalArgs.Count > 1)
            {
                var excludeArg = positionalArgs[1];
                var expr = excludeArg.Expression;
                InitializerExpressionSyntax? initializer = null;
                switch (expr)
                {
                    case ImplicitArrayCreationExpressionSyntax implicitArray:
                        initializer = implicitArray.Initializer;
                        break;
                    case ArrayCreationExpressionSyntax arrayCreation:
                        initializer = arrayCreation.Initializer;
                        break;
                    case InitializerExpressionSyntax directInit:
                        initializer = directInit;
                        break;
                }

                if (initializer != null)
                {
                    foreach (var e in initializer.Expressions)
                        ProcessExpression(e);
                    return excluded;
                }
            }

            var excludeNamed = attrSyntax.ArgumentList?.Arguments.FirstOrDefault(a => (a.NameEquals?.Name.Identifier.ValueText == "Exclude") || (a.NameColon?.Name.Identifier.ValueText == "Exclude"));
            if (excludeNamed != null)
            {
                var expr = excludeNamed.Expression;
                InitializerExpressionSyntax? initializer = null;
                
                switch (expr)
                {
                    case ImplicitArrayCreationExpressionSyntax implicitArray:
                        initializer = implicitArray.Initializer;
                        break;
                    case ArrayCreationExpressionSyntax arrayCreation:
                        initializer = arrayCreation.Initializer;
                        break;
                    case InitializerExpressionSyntax directInit:
                        initializer = directInit;
                        break;
                    case CollectionExpressionSyntax collectionExpr:
                        
                        foreach (var element in collectionExpr.Elements)
                        {
                            if (element is ExpressionElementSyntax exprElem)
                            {
                                ProcessExpression(exprElem.Expression);
                            }
                        }
                        return excluded;
                }

                if (initializer != null)
                {
                    foreach (var e in initializer.Expressions)
                        ProcessExpression(e);
                    return excluded;
                }
            }

        }

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
        ExternalXmlDocProvider? externalDocProvider,
        CancellationToken token)
    {
        var properties = new List<FlattenProperty>();
        var seenNames = new HashSet<string>();
        var collectionTypeCache = new Dictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);
        var leafTypeCache = new Dictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);

        var foreignKeyPaths = ignoreForeignKeyClashes
            ? CollectAllForeignKeyPaths(sourceType, includeFields, namingStrategy, new List<string>(), new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), collectionTypeCache, 0, maxDepth)
            : new HashSet<string>();

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
            externalDocProvider,
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

            var flattenedName = GenerateFlattenedName(newPathSegments, FlattenNamingStrategy.LeafOnly);

            if (ignoreForeignKeyClashes && depth > 0 && foreignKeyPaths.Contains(flattenedName))
            {
                continue;
            }

            if (IsCollectionType(memberType, collectionTypeCache))
            {
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

        if (maxDepth > 0 && depth >= maxDepth) return foreignKeyPaths;
        if (depth >= 10) return foreignKeyPaths; 

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
                    var potentialNavProp = memberName.Substring(0, memberName.Length - 2);
                    if (allPropertyNames.Contains(potentialNavProp))
                    {
                        var fkPath = new List<string>(currentPath) { memberName };
                        var flattenedFkName = GenerateFlattenedName(fkPath, namingStrategy);
                        foreignKeyPaths.Add(flattenedFkName);
                    }
                }
            }

            if (IsCollectionType(memberType, collectionTypeCache))
            {
                continue; 
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
        ExternalXmlDocProvider? externalDocProvider,
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

            var flattenedName = GenerateFlattenedName(newPathSegments, namingStrategy, collidingLeafNames);

            if (ignoreForeignKeyClashes && depth > 0 && foreignKeyPaths.Contains(flattenedName))
            {
                continue;
            }

            bool isCollection = IsCollectionType(memberType, collectionTypeCache);
            if (isCollection)
            {
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

                    var xmlDoc = CodeGenerationHelpers.ExtractXmlDocumentation(member, false, externalDocProvider);

                    var typeName = GeneratorUtilities.GetTypeNameWithNullability(memberType);

                    properties.Add(new FlattenProperty(
                        flattenedName,
                        typeName,
                        newPath,
                        newPathSegments.ToImmutableArray(),
                        false, 
                        xmlDoc,
                        true)); 
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

                var xmlDoc = CodeGenerationHelpers.ExtractXmlDocumentation(member, false, externalDocProvider);

                string typeName;
                if (depth > 0 && memberType.IsValueType && memberType.NullableAnnotation != NullableAnnotation.Annotated)
                {
                    typeName = $"{GeneratorUtilities.GetTypeNameWithNullability(memberType)}?";
                }
                else
                {
                    typeName = GeneratorUtilities.GetTypeNameWithNullability(memberType);
                }

                properties.Add(new FlattenProperty(
                    flattenedName,
                    typeName,
                    newPath,
                    newPathSegments.ToImmutableArray(),
                    memberType.IsValueType,
                    xmlDoc,
                    false)); 
            }
            else
            {
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
                    externalDocProvider,
                    token);
            }
        }

        visitedTypes.Remove(currentType);
    }

    private static bool ShouldFlattenAsLeaf(ITypeSymbol type, Dictionary<ITypeSymbol, bool> cache)
    {
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
        var specialType = type.SpecialType;
        if (specialType != SpecialType.None && specialType != SpecialType.System_Object)
        {
            return true; 
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (type.IsValueType)
        {
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

            if (type is INamedTypeSymbol namedValueType)
            {
                var members = namedValueType.GetMembers();
                if (members.Length == 0)
                {
                    return true;
                }

                var propertyCount = 0;
                foreach (var member in members)
                {
                    if (member is IPropertySymbol)
                    {
                        propertyCount++;
                        if (propertyCount > 2) 
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
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (type.TypeKind == TypeKind.Array)
        {
            return true;
        }

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

        var ns = type.ContainingNamespace;
        while (ns != null && !ns.IsGlobalNamespace)
        {
            if (ns.Name == "Collections")
            {
                return true;
            }
            ns = ns.ContainingNamespace;
        }

        // Checking interfaces is the expensive fallback.
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
        // Treat foreign keys like CustomerId as Id properties too.
        return propertyName == "Id" || propertyName.EndsWith("Id");
    }

    private static bool IsExcluded(string path, HashSet<string> excludedPaths)
    {
        if (excludedPaths.Contains(path)) return true;

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

            if (collidingLeafNames != null && collidingLeafNames.Contains(leafName) && pathSegments.Count >= 2)
            {
                var parentName = pathSegments[pathSegments.Count - 2];
                return parentName + leafName;
            }

            return leafName;
        }

        return string.Join("", pathSegments);
    }

    private static T GetNamedArg<T>(ImmutableArray<KeyValuePair<string, TypedConstant>> namedArgs, string name, T defaultValue)
    {
        var arg = namedArgs.FirstOrDefault(x => x.Key == name);
        if (arg.Key == null) return defaultValue;

        var value = arg.Value.Value;
        if (value is T typedValue) return typedValue;

        if (typeof(T).IsEnum && value is int intValue)
        {
            return (T)(object)intValue;
        }

        return defaultValue;
    }
}
