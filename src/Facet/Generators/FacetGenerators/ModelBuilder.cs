using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Facet.Generators;

/// <summary>
/// Builds FacetTargetModel instances from attribute syntax contexts.
/// </summary>
internal static class ModelBuilder
{
    /// <summary>
    /// Builds one <see cref="FacetTargetModel"/> per <c>[Facet]</c> attribute found on the target
    /// type, allowing multiple source-type mappings to the same target class.
    /// </summary>
    public static ImmutableArray<FacetTargetModel?> BuildModels(
        GeneratorAttributeSyntaxContext context,
        GlobalConfigurationDefaults globalDefaults,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol) return ImmutableArray<FacetTargetModel?>.Empty;
        if (context.Attributes.Length == 0) return ImmutableArray<FacetTargetModel?>.Empty;

        var builder = ImmutableArray.CreateBuilder<FacetTargetModel?>(context.Attributes.Length);
        foreach (var attribute in context.Attributes)
        {
            token.ThrowIfCancellationRequested();
            builder.Add(BuildModelForAttribute(context, attribute, globalDefaults, token));
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// Builds a FacetTargetModel from the generator attribute syntax context.
    /// Only processes the first <c>[Facet]</c> attribute found on the type.
    /// Use <see cref="BuildModels"/> to process all attributes.
    /// </summary>
    public static FacetTargetModel? BuildModel(
        GeneratorAttributeSyntaxContext context,
        GlobalConfigurationDefaults globalDefaults,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol) return null;
        if (context.Attributes.Length == 0) return null;

        return BuildModelForAttribute(context, context.Attributes[0], globalDefaults, token);
    }

    private static FacetTargetModel? BuildModelForAttribute(
        GeneratorAttributeSyntaxContext context,
        AttributeData attribute,
        GlobalConfigurationDefaults globalDefaults,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol targetSymbol) return null;

        var sourceType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        if (sourceType == null) return null;

        var excluded = AttributeParser.ExtractExcludedMembers(attribute);
        var (included, isIncludeMode) = AttributeParser.ExtractIncludedMembers(attribute);

        var includeFields = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.IncludeFields, globalDefaults.IncludeFields);
        var generateConstructor = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateConstructor, globalDefaults.GenerateConstructor);
        var generateParameterlessConstructor = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateParameterlessConstructor, globalDefaults.GenerateParameterlessConstructor);
        var chainToParameterlessConstructor = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.ChainToParameterlessConstructor, globalDefaults.ChainToParameterlessConstructor);
        var generateProjection = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateProjection, globalDefaults.GenerateProjection);
        var generateToSource = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateToSource, globalDefaults.GenerateToSource);
        var configurationTypeName = AttributeParser.ExtractConfigurationTypeName(attribute);
        var beforeMapConfigurationTypeName = AttributeParser.ExtractBeforeMapConfigurationTypeName(attribute);
        var afterMapConfigurationTypeName = AttributeParser.ExtractAfterMapConfigurationTypeName(attribute);

        var (typeKind, isRecord) = TypeAnalyzer.InferTypeKind(targetSymbol);

        var accessibility = targetSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.Private => "private",
            _ => "public"
        };

        var preserveInitOnlyDefault = isRecord;
        var preserveRequiredDefault = isRecord;

        var preserveInitOnly = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.PreserveInitOnlyProperties, preserveInitOnlyDefault);
        var preserveRequired = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.PreserveRequiredProperties, preserveRequiredDefault);
        var nullableProperties = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.NullableProperties, globalDefaults.NullableProperties);
        var copyAttributes = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.CopyAttributes, globalDefaults.CopyAttributes);
        var copyDocs = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.CopyDocs, globalDefaults.CopyDocs);
        var inheritDocs = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.InheritDocs, globalDefaults.InheritDocs);
        var maxDepth = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.MaxDepth, globalDefaults.MaxDepth);
        var preserveReferences = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.PreserveReferences, globalDefaults.PreserveReferences);
        var maxDepthToSource = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.MaxDepthToSource, globalDefaults.MaxDepthToSource);

        var convertEnumsTo = AttributeParser.ExtractConvertEnumsTo(attribute);

        var setAccessor = (PropertySetAccessor)AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.SetAccessor, (int)PropertySetAccessor.Preserve);

        var generateCopyConstructor = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateCopyConstructor, globalDefaults.GenerateCopyConstructor);
        var generateEquality = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateEquality, globalDefaults.GenerateEquality);

        var toSourceConfigurationTypeName = AttributeParser.ExtractToSourceConfigurationTypeName(attribute);

        var configTypeSymbol = AttributeParser.ExtractConfigurationTypeSymbol(attribute);
        var hasProjectionMapConfiguration = false;
        var hasMapConfiguration = false;
        if (configTypeSymbol != null)
        {
            var projectionConfigInterface = context.SemanticModel.Compilation
                .GetTypeByMetadataName(FacetConstants.ProjectionMapConfigurationInterfaceFullName);
            if (projectionConfigInterface != null)
            {
                hasProjectionMapConfiguration = configTypeSymbol.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, projectionConfigInterface));
            }

            var mapConfigInterface = context.SemanticModel.Compilation
                .GetTypeByMetadataName(FacetConstants.MapConfigurationInterfaceFullName);
            if (mapConfigInterface != null)
            {
                hasMapConfiguration = configTypeSymbol.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, mapConfigInterface));
            }
        }

        var collectionTargetType = AttributeParser.ExtractCollectionTargetType(attribute);
        var nestedFacetMappings = AttributeParser.ExtractNestedFacetMappings(attribute);

        var expressionMembers = new List<FacetMember>();
        var mapFromMappings = ExtractMapFromMappings(targetSymbol, expressionMembers, nullableProperties);

        var mapWhenMappings = ExtractMapWhenMappings(targetSymbol);

        var externalDocProvider = copyDocs ? new ExternalXmlDocProvider(context.SemanticModel.Compilation) : null;

        var typeXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(sourceType, inheritDocs, externalDocProvider);

        var baseClassMemberNames = GetBaseClassMemberNames(targetSymbol);

        var baseFacetInfo = GetBaseFacetInfo(targetSymbol, sourceType, context.SemanticModel.Compilation);

        if (baseFacetInfo != null && !baseFacetInfo.IncludedMembers.IsDefaultOrEmpty)
        {
            var extraBaseNames = new List<string>();
            foreach (var baseIncludedMember in baseFacetInfo.IncludedMembers)
            {
                included.Add(baseIncludedMember);
                if (!baseClassMemberNames.Contains(baseIncludedMember))
                {
                    extraBaseNames.Add(baseIncludedMember);
                }
            }
            if (extraBaseNames.Count > 0)
            {
                baseClassMemberNames = baseClassMemberNames.AddRange(extraBaseNames);
            }
        }

        if (baseFacetInfo != null && !baseFacetInfo.NestedFacetMappings.IsEmpty)
        {
            foreach (var baseNestedMapping in baseFacetInfo.NestedFacetMappings)
            {
                if (!nestedFacetMappings.ContainsKey(baseNestedMapping.Key))
                {
                    nestedFacetMappings[baseNestedMapping.Key] = baseNestedMapping.Value;
                }
            }
        }

        var (members, excludedRequiredMembers) = ExtractMembers(
            sourceType,
            excluded,
            included,
            isIncludeMode,
            includeFields,
            preserveInitOnly,
            preserveRequired,
            nullableProperties,
            copyAttributes,
            copyDocs,
            inheritDocs,
            nestedFacetMappings,
            mapFromMappings,
            mapWhenMappings,
            convertEnumsTo,
            baseClassMemberNames,
            collectionTargetType,
            externalDocProvider,
            token,
            setAccessor);

        if (expressionMembers.Count > 0)
        {
            members = members.AddRange(expressionMembers);
        }

        var ns = targetSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : targetSymbol.ContainingNamespace.ToDisplayString();

        var useFullName = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.UseFullName, globalDefaults.UseFullName);

        var containingTypes = TypeAnalyzer.GetContainingTypes(targetSymbol);

        string fullName;
        if (useFullName)
        {
            fullName = targetSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).GetSafeName();
        }
        else if (containingTypes.Length > 0)
        {
            fullName = string.Join(".", containingTypes) + "." + targetSymbol.Name;
        }
        else if (ns != null)
        {
            fullName = ns + "." + targetSymbol.Name;
        }
        else
        {
            fullName = targetSymbol.Name;
        }

        var sourceContainingTypes = TypeAnalyzer.GetContainingTypes(sourceType);

        var hasExistingPrimaryConstructor = TypeAnalyzer.HasExistingPrimaryConstructor(targetSymbol);

        var hasPositionalConstructor = TypeAnalyzer.HasPositionalConstructor(sourceType);

        if (generateToSource && !hasPositionalConstructor)
        {
            // Nested facets can access private members on the containing source type.
            var isNestedInSource = TypeAnalyzer.IsNestedInsideType(targetSymbol, sourceType);
            var hasAccessibleConstructor = TypeAnalyzer.HasAccessibleParameterlessConstructor(sourceType, context.SemanticModel.Compilation.Assembly, isNestedInSource);
            var hasAccessibleSetters = TypeAnalyzer.AllPropertiesHaveAccessibleSetters(sourceType, members, isNestedInSource, context.SemanticModel.Compilation.Assembly);

            if (!hasAccessibleConstructor || !hasAccessibleSetters)
            {
                // Users can still provide their own ToSource implementation.
                generateToSource = false;
            }
        }

        var flattenToTypes = AttributeParser.ExtractFlattenToTypes(attribute);

        var sourceTypeFullName = sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var baseHidesFacetMembers = BaseHidesFacetMembers(targetSymbol);
        var baseHidesFromSource = BaseHidesFromSource(targetSymbol, sourceTypeFullName);
        var baseHidesToSource = BaseHidesToSourceMembers(targetSymbol);

        var sourcePropertyNames = CollectSourcePropertyNames(sourceType);
        var declaredBaseTypeNames = GetDeclaredBaseTypeNames(targetSymbol, generateEquality);

        return new FacetTargetModel(
            targetSymbol.Name,
            ns,
            fullName,
            typeKind,
            isRecord,
            accessibility,
            generateConstructor,
            generateParameterlessConstructor,
            generateProjection,
            generateToSource,
            sourceTypeFullName,
            sourceContainingTypes,
            configurationTypeName,
            members,
            hasExistingPrimaryConstructor,
            hasPositionalConstructor,
            typeXmlDocumentation,
            containingTypes,
            useFullName,
            excludedRequiredMembers,
            nullableProperties,
            copyAttributes,
            maxDepth,
            preserveReferences,
            baseClassMemberNames,
            flattenToTypes,
            beforeMapConfigurationTypeName,
            afterMapConfigurationTypeName,
            chainToParameterlessConstructor,
            convertEnumsTo,
            generateCopyConstructor,
            generateEquality,
            toSourceConfigurationTypeName,
            baseHidesFacetMembers,
            hasProjectionMapConfiguration,
            baseHidesFromSource,
            hasMapConfiguration,
            baseFacetInfo,
            maxDepthToSource,
            sourcePropertyNames,
            baseHidesToSource,
            setAccessor,
            declaredBaseTypeNames);
    }

    #region Private Helper Methods

    private static (ImmutableArray<FacetMember> members, ImmutableArray<FacetMember> excludedRequiredMembers) ExtractMembers(
        INamedTypeSymbol sourceType,
        HashSet<string> excluded,
        HashSet<string> included,
        bool isIncludeMode,
        bool includeFields,
        bool preserveInitOnly,
        bool preserveRequired,
        bool nullableProperties,
        bool copyAttributes,
        bool copyDocs,
        bool inheritDocs,
        Dictionary<string, (string childFacetTypeName, string sourceTypeName)> nestedFacetMappings,
        Dictionary<string, (string targetName, string source, bool reversible, bool includeInProjection, string typeName, string? asCollection, bool isTargetRequired)> mapFromMappings,
        Dictionary<string, (List<string> conditions, string? defaultValue, bool includeInProjection)> mapWhenMappings,
        string? convertEnumsTo,
        ImmutableArray<string> baseClassMemberNames,
        string? collectionTargetType,
        ExternalXmlDocProvider? externalDocProvider,
        CancellationToken token,
        PropertySetAccessor setAccessor = PropertySetAccessor.Preserve)
    {
        var members = new List<FacetMember>();
        var excludedRequiredMembers = new List<FacetMember>();
        var addedMembers = new HashSet<string>();

        var allMembersWithModifiers = GeneratorUtilities.GetAllMembersWithModifiers(sourceType);

        foreach (var (member, isInitOnly, isRequired) in allMembersWithModifiers)
        {
            token.ThrowIfCancellationRequested();

            if (addedMembers.Contains(member.Name)) continue;

            bool shouldIncludeMember = isIncludeMode
                ? included.Contains(member.Name)
                : !excluded.Contains(member.Name);

            if (!shouldIncludeMember && isIncludeMode && baseClassMemberNames.Contains(member.Name))
            {
                shouldIncludeMember = true;
            }

            if (member is IPropertySymbol property && property.DeclaredAccessibility == Accessibility.Public)
            {
                ProcessProperty(
                    property,
                    shouldIncludeMember,
                    isInitOnly,
                    isRequired,
                    preserveInitOnly,
                    preserveRequired,
                    nullableProperties,
                    copyAttributes,
                    copyDocs,
                    inheritDocs,
                    nestedFacetMappings,
                    mapFromMappings,
                    mapWhenMappings,
                    convertEnumsTo,
                    collectionTargetType,
                    externalDocProvider,
                    members,
                    excludedRequiredMembers,
                    addedMembers,
                    setAccessor);
            }
            else if (includeFields && member is IFieldSymbol field && field.DeclaredAccessibility == Accessibility.Public)
            {
                ProcessField(
                    field,
                    shouldIncludeMember,
                    isRequired,
                    preserveRequired,
                    nullableProperties,
                    copyAttributes,
                    copyDocs,
                    inheritDocs,
                    externalDocProvider,
                    members,
                    excludedRequiredMembers,
                    addedMembers);
            }
        }

        return (members.ToImmutableArray(), excludedRequiredMembers.ToImmutableArray());
    }

    private static void ProcessProperty(
        IPropertySymbol property,
        bool shouldIncludeMember,
        bool isInitOnly,
        bool isRequired,
        bool preserveInitOnly,
        bool preserveRequired,
        bool nullableProperties,
        bool copyAttributes,
        bool copyDocs,
        bool inheritDocs,
        Dictionary<string, (string childFacetTypeName, string sourceTypeName)> nestedFacetMappings,
        Dictionary<string, (string targetName, string source, bool reversible, bool includeInProjection, string typeName, string? asCollection, bool isTargetRequired)> mapFromMappings,
        Dictionary<string, (List<string> conditions, string? defaultValue, bool includeInProjection)> mapWhenMappings,
        string? convertEnumsTo,
        string? collectionTargetType,
        ExternalXmlDocProvider? externalDocProvider,
        List<FacetMember> members,
        List<FacetMember> excludedRequiredMembers,
        HashSet<string> addedMembers,
        PropertySetAccessor setAccessor = PropertySetAccessor.Preserve)
    {
        var memberXmlDocumentation = copyDocs ? CodeGenerationHelpers.ExtractXmlDocumentation(property, inheritDocs, externalDocProvider) : null;

        var hasMapFrom = mapFromMappings.TryGetValue(property.Name, out var mapFromInfo);

        if (!shouldIncludeMember && !hasMapFrom)
        {
            if (isRequired)
            {
                excludedRequiredMembers.Add(new FacetMember(
                    property.Name,
                    GeneratorUtilities.GetTypeNameWithNullability(property.Type),
                    FacetMemberKind.Property,
                    property.Type.IsValueType,
                    isInitOnly,
                    isRequired,
                    false, 
                    memberXmlDocumentation));
            }
            return;
        }

        var shouldPreserveInitOnly = setAccessor switch
        {
            PropertySetAccessor.Init => true,
            PropertySetAccessor.Set => false,
            _ => preserveInitOnly && isInitOnly,
        };
        
        var shouldPreserveRequired = (preserveRequired && isRequired) || (hasMapFrom && mapFromInfo.isTargetRequired);

        var typeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);
        var propertyTypeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        bool isNestedFacet = false;
        string? nestedFacetSourceTypeName = null;
        bool isCollection = false;
        string? collectionWrapper = null;
        string? sourceCollectionWrapper = null;

        bool isNestedType = GeneratorUtilities.IsNestedType(property.Type);

        bool isNullableReferenceType = property.Type.NullableAnnotation == NullableAnnotation.Annotated;
        bool shouldTreatAsNullable = isNullableReferenceType;

        if (!shouldTreatAsNullable && !property.Type.IsValueType)
        {
            // Treat missing nullable annotations as nullable for safety.
            if (property.Type.NullableAnnotation != NullableAnnotation.NotAnnotated)
            {
                shouldTreatAsNullable = true;
            }
        }

        if (GeneratorUtilities.TryGetCollectionElementType(property.Type, out var elementType, out var wrapper))
        {
            isCollection = true;
            collectionWrapper = wrapper;

            var elementTypeName = elementType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (nestedFacetMappings.TryGetValue(elementTypeName, out var nestedMapping))
            {
                var effectiveWrapper = collectionTargetType ?? wrapper!;
                var sourceWrapper = (collectionTargetType != null && collectionTargetType != wrapper) ? wrapper : null;

                var wrappedType = GeneratorUtilities.WrapInCollectionType(nestedMapping.childFacetTypeName, effectiveWrapper);
                
                typeName = shouldTreatAsNullable ? wrappedType + "?" : wrappedType;
                isNestedFacet = true;
                collectionWrapper = effectiveWrapper;
                sourceCollectionWrapper = sourceWrapper;
                nestedFacetSourceTypeName = nestedMapping.sourceTypeName;
            }
        }
        
        else if (nestedFacetMappings.TryGetValue(propertyTypeName, out var nestedMapping))
        {
            typeName = shouldTreatAsNullable
                ? nestedMapping.childFacetTypeName + "?"
                : nestedMapping.childFacetTypeName;
            isNestedFacet = true;
            nestedFacetSourceTypeName = nestedMapping.sourceTypeName;
        }

        var sourceMemberTypeName = typeName;

        if (nullableProperties)
        {
            typeName = GeneratorUtilities.MakeNullable(typeName);
        }

        List<string> attributes;
        List<string> attributeNamespaces;
        if (copyAttributes)
        {
            var (attrs, namespaces) = AttributeProcessor.ExtractCopiableAttributesWithNamespaces(property, FacetMemberKind.Property);
            attributes = attrs;
            attributeNamespaces = namespaces.ToList();
        }
        else
        {
            attributes = new List<string>();
            attributeNamespaces = new List<string>();
        }

        var isSourcePartial = IsPartialDefiningProperty(property);

        string? defaultValue = null;
        if (!isNestedFacet && !nullableProperties && !isSourcePartial)
        {
            defaultValue = ExtractPropertyInitializer(property);
        }

        var memberName = hasMapFrom ? mapFromInfo.targetName : property.Name;
        var mapFromSource = hasMapFrom ? mapFromInfo.source : null;
        var mapFromReversible = hasMapFrom ? mapFromInfo.reversible : true;
        var mapFromIncludeInProjection = hasMapFrom ? mapFromInfo.includeInProjection : true;
        var mapFromAsCollection = hasMapFrom ? mapFromInfo.asCollection : null;
        var sourcePropertyName = property.Name; 

        var hasMapWhen = mapWhenMappings.TryGetValue(memberName, out var mapWhenInfo);

        var isUserDeclared = hasMapFrom || hasMapWhen;
        var mapWhenConditions = hasMapWhen ? mapWhenInfo.conditions : null;
        var mapWhenDefault = hasMapWhen ? mapWhenInfo.defaultValue : null;
        var mapWhenIncludeInProjection = hasMapWhen ? mapWhenInfo.includeInProjection : true;

        if (hasMapFrom && !string.IsNullOrEmpty(mapFromInfo.typeName))
        {
            typeName = mapFromInfo.typeName;
            if (nullableProperties)
            {
                typeName = GeneratorUtilities.MakeNullable(typeName);
            }
        }

        if (mapFromAsCollection != null && isCollection)
        {
            var originalWrapper = collectionWrapper;
            collectionWrapper = mapFromAsCollection;
            if (originalWrapper != mapFromAsCollection)
                sourceCollectionWrapper = originalWrapper;
        }

        bool isEnumConversion = false;
        string? originalEnumTypeName = null;
        if (convertEnumsTo != null && !isNestedFacet && !isUserDeclared)
        {
            if (isCollection && elementType != null)
            {
                var underlyingElementType = elementType;
                bool isNullableEnumElement = false;
                if (underlyingElementType is INamedTypeSymbol namedElementType &&
                    namedElementType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    underlyingElementType = namedElementType.TypeArguments[0];
                    isNullableEnumElement = true;
                }

                if (underlyingElementType.TypeKind == TypeKind.Enum)
                {
                    isEnumConversion = true;
                    originalEnumTypeName = underlyingElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    string convertedElementType;
                    if (convertEnumsTo == "string")
                    {
                        convertedElementType = isNullableEnumElement ? "string?" : "string";
                    }
                    else if (convertEnumsTo == "int")
                    {
                        convertedElementType = isNullableEnumElement ? "int?" : "int";
                    }
                    else
                    {
                        convertedElementType = isNullableEnumElement ? "string?" : "string";
                    }

                    var wrappedType = GeneratorUtilities.WrapInCollectionType(convertedElementType, collectionWrapper!);
                    
                    typeName = shouldTreatAsNullable ? wrappedType + "?" : wrappedType;

                    sourceMemberTypeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);

                    if (nullableProperties)
                    {
                        typeName = GeneratorUtilities.MakeNullable(typeName);
                    }

                    defaultValue = null;
                }
            }
            else if (!isCollection)
            {
                var underlyingType = property.Type;
                bool isNullableEnum = false;
                if (underlyingType is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    underlyingType = namedType.TypeArguments[0];
                    isNullableEnum = true;
                }

                if (underlyingType.TypeKind == TypeKind.Enum)
                {
                    isEnumConversion = true;
                    originalEnumTypeName = underlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    if (convertEnumsTo == "string")
                    {
                        typeName = isNullableEnum ? "string?" : "string";
                    }
                    else if (convertEnumsTo == "int")
                    {
                        typeName = isNullableEnum ? "int?" : "int";
                    }

                    sourceMemberTypeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);

                    if (nullableProperties)
                    {
                        typeName = GeneratorUtilities.MakeNullable(typeName);
                    }

                    defaultValue = null;
                }
            }
        }

        members.Add(new FacetMember(
            memberName,
            typeName,
            FacetMemberKind.Property,
            property.Type.IsValueType,
            shouldPreserveInitOnly,
            shouldPreserveRequired,
            false, 
            memberXmlDocumentation,
            isNestedFacet,
            nestedFacetSourceTypeName,
            attributes,
            isCollection,
            collectionWrapper,
            sourceCollectionWrapper,
            sourceMemberTypeName,
            mapFromSource,
            mapFromReversible,
            mapFromIncludeInProjection,
            sourcePropertyName,
            isUserDeclared,
            mapWhenConditions,
            mapWhenDefault,
            mapWhenIncludeInProjection,
            attributeNamespaces,
            defaultValue,
            isEnumConversion,
            originalEnumTypeName,
            isNestedType,
            isPartial: false, 
            isSourceInitOnly: isInitOnly)); 
        addedMembers.Add(memberName);
    }

    /// <summary>
    /// Determines whether a property symbol is a partial property defining declaration (C# 13+).
    /// A defining declaration has the <c>partial</c> modifier but no accessor body implementations.
    /// </summary>
    private static bool IsPartialDefiningProperty(IPropertySymbol property)
    {
        foreach (var syntaxRef in property.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is PropertyDeclarationSyntax propSyntax)
            {
                var hasPartial = propSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
                if (!hasPartial) continue;

                var hasAccessorBody = propSyntax.AccessorList?.Accessors
                    .Any(a => a.Body != null || a.ExpressionBody != null) == true;

                if (!hasAccessorBody)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Extracts the property initializer from the source property's syntax declaration.
    /// For example, for "public UserSettings Settings { get; set; } = new();" this returns "new()".
    /// </summary>
    private static string? ExtractPropertyInitializer(IPropertySymbol property)
    {
        foreach (var syntaxRef in property.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();
            if (syntax is PropertyDeclarationSyntax propSyntax && propSyntax.Initializer != null)
            {
                return propSyntax.Initializer.Value.ToFullString().Trim();
            }
        }
        return null;
    }

    private static void ProcessField(
        IFieldSymbol field,
        bool shouldIncludeMember,
        bool isRequired,
        bool preserveRequired,
        bool nullableProperties,
        bool copyAttributes,
        bool copyDocs,
        bool inheritDocs,
        ExternalXmlDocProvider? externalDocProvider,
        List<FacetMember> members,
        List<FacetMember> excludedRequiredMembers,
        HashSet<string> addedMembers)
    {
        var memberXmlDocumentation = copyDocs ? CodeGenerationHelpers.ExtractXmlDocumentation(field, inheritDocs, externalDocProvider) : null;

        if (!shouldIncludeMember)
        {
            if (isRequired)
            {
                excludedRequiredMembers.Add(new FacetMember(
                    field.Name,
                    GeneratorUtilities.GetTypeNameWithNullability(field.Type),
                    FacetMemberKind.Field,
                    field.Type.IsValueType,
                    false, 
                    isRequired,
                    field.IsReadOnly, 
                    memberXmlDocumentation));
            }
            return;
        }

        var shouldPreserveRequired = preserveRequired && isRequired;

        var typeName = GeneratorUtilities.GetTypeNameWithNullability(field.Type);
        var sourceMemberTypeName = typeName; 
        if (nullableProperties)
        {
            typeName = GeneratorUtilities.MakeNullable(typeName);
        }

        List<string> attributes;
        List<string> attributeNamespaces;
        if (copyAttributes)
        {
            var (attrs, namespaces) = AttributeProcessor.ExtractCopiableAttributesWithNamespaces(field, FacetMemberKind.Field);
            attributes = attrs;
            attributeNamespaces = namespaces.ToList();
        }
        else
        {
            attributes = new List<string>();
            attributeNamespaces = new List<string>();
        }

        string? defaultValue = null;
        if (!nullableProperties)
        {
            defaultValue = ExtractFieldInitializer(field);
        }

        members.Add(new FacetMember(
            field.Name,
            typeName,
            FacetMemberKind.Field,
            field.Type.IsValueType,
            false, 
            shouldPreserveRequired,
            field.IsReadOnly, 
            memberXmlDocumentation,
            false, 
            null,
            attributes,
            false, 
            null,  // Fields never use collection wrappers.
            null,  
            sourceMemberTypeName,
            null,  
            false, 
            true,  
            null,  
            false, 
            null,  
            null,  
            true,  
            attributeNamespaces,
            defaultValue));
        addedMembers.Add(field.Name);
    }

    /// <summary>
    /// Extracts the field initializer from the source field's syntax declaration.
    /// For example, for "public string Name = string.Empty;" this returns "string.Empty".
    /// </summary>
    private static string? ExtractFieldInitializer(IFieldSymbol field)
    {
        foreach (var syntaxRef in field.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();
            if (syntax is VariableDeclaratorSyntax varSyntax && varSyntax.Initializer != null)
            {
                return varSyntax.Initializer.Value.ToFullString().Trim();
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts MapFrom attribute mappings from the target type's properties.
    /// Returns a dictionary mapping source property names to (targetName, source, reversible, includeInProjection, typeName).
    /// Also returns a list of expression-based members that should be added directly.
    /// Walks the full base-class chain so that [MapFrom] mappings declared in a base DTO class
    /// are inherited by derived DTO classes.
    /// </summary>
private static Dictionary<string, (string targetName, string source, bool reversible, bool includeInProjection, string typeName, string? asCollection, bool isTargetRequired)> ExtractMapFromMappings(
    INamedTypeSymbol targetSymbol,
    List<FacetMember> expressionMembers,
    bool nullableProperties)
{
    var mappings = new Dictionary<string, (string targetName, string source, bool reversible, bool includeInProjection, string typeName, string? asCollection, bool isTargetRequired)>();

    var typeChain = new List<INamedTypeSymbol>();
    var ancestor = targetSymbol.BaseType;
    while (ancestor != null && ancestor.SpecialType != SpecialType.System_Object)
    {
        typeChain.Add(ancestor);
        ancestor = ancestor.BaseType;
    }
    typeChain.Reverse();       
    typeChain.Add(targetSymbol); 

    // Let derived [MapFrom] declarations overwrite base ones.
    var pendingExpressionMembers = new Dictionary<string, FacetMember>();

    foreach (var typeToProcess in typeChain)
    {
        foreach (var member in typeToProcess.GetMembers())
        {
        if (member is not IPropertySymbol property) continue;

        foreach (var attr in property.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == FacetConstants.MapFromAttributeFullName)
            {
                string? sourceFromData = null;
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string s)
                {
                    sourceFromData = s;
                }

                string? source = sourceFromData;
                bool hadLeadingAt = false;
                if (attr.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax attrSyntax)
                {
                    var firstArgExpr = attrSyntax.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
                    if (firstArgExpr != null)
                    {
                        var (resolved, hadAt) = NameOfResolver.ResolveExpression(firstArgExpr);
                        
                        if (!string.IsNullOrEmpty(resolved) && hadAt && IsNameOfExpression(firstArgExpr))
                        {
                            var segments = resolved.Split('.');
                            if (segments.Length > 1)
                            {
                                source = string.Join(".", segments.Skip(1));
                            }
                            else
                            {
                                source = resolved;
                            }
                            hadLeadingAt = hadAt;
                        }
                    }
                }

                if (string.IsNullOrEmpty(source))
                {
                    break;
                }

                var reversible = false;
                var includeInProjection = true;
                string? asCollection = null;

                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Reversible" && namedArg.Value.Value is bool rev)
                    {
                        reversible = rev;
                    }
                    else if (namedArg.Key == "IncludeInProjection" && namedArg.Value.Value is bool incProj)
                    {
                        includeInProjection = incProj;
                    }
                    else if (namedArg.Key == "AsCollection" && namedArg.Value.Value is INamedTypeSymbol asCollectionType)
                    {
                        asCollection = AttributeParser.TypeToCollectionWrapper(asCollectionType);
                    }
                }

                var typeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);
                if (nullableProperties)
                {
                    typeName = GeneratorUtilities.MakeNullable(typeName);
                }

                if (IsExpression(source) || source.Contains(".") || hadLeadingAt)
                {
                    pendingExpressionMembers[property.Name] = new FacetMember(
                        property.Name,
                        typeName,
                        FacetMemberKind.Property,
                        property.Type.IsValueType,
                        false, 
                        false, 
                        false, 
                        null,  
                        false, 
                        null,  
                        null,  
                        false, 
                        null,  
                        null,  
                        null,  
                        source, 
                        reversible,
                        includeInProjection,
                        property.Name, 
                        true); 
                }
                else
                {
                    mappings[source] = (property.Name, source, reversible, includeInProjection, typeName, asCollection, property.IsRequired);
                }
            }
        }
        }
    }

    foreach (var em in pendingExpressionMembers.Values)
        expressionMembers.Add(em);

    return mappings;
}

    /// <summary>
    /// Determines if the source string is an expression (contains operators, spaces, etc.)
    /// </summary>
    private static bool IsExpression(string source)
    {
        return source.Contains(" ") ||
               source.Contains("+") ||
               source.Contains("-") ||
               source.Contains("*") ||
               source.Contains("/") ||
               source.Contains("(") ||
               source.Contains("?") ||
               source.Contains(":");
    }

    /// <summary>
    /// Gets all member names from the target type's base classes.
    /// This is used to avoid generating properties that already exist in base classes.
    /// Also detects base Facet types and includes the properties they would generate
    /// (source generators can't see their own output, so these are invisible to Roslyn).
    /// </summary>
    private static ImmutableArray<string> GetBaseClassMemberNames(INamedTypeSymbol targetSymbol)
    {
        var memberNames = new List<string>();

        var baseType = targetSymbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in baseType.GetMembers())
            {
                if (member is IPropertySymbol property && property.DeclaredAccessibility == Accessibility.Public)
                {
                    memberNames.Add(property.Name);
                }
                else if (member is IFieldSymbol field && field.DeclaredAccessibility == Accessibility.Public && !field.IsImplicitlyDeclared)
                {
                    memberNames.Add(field.Name);
                }
            }

            var facetAttrs = baseType.GetAttributes()
                .Where(a => a.AttributeClass?.ToDisplayString() == FacetConstants.FacetAttributeFullName)
                .ToList();

            foreach (var facetAttr in facetAttrs)
            {
                if (facetAttr.ConstructorArguments.Length == 0 ||
                    facetAttr.ConstructorArguments[0].Value is not INamedTypeSymbol baseSourceType)
                    continue;

                var (baseIncluded, baseIsIncludeMode) = AttributeParser.ExtractIncludedMembers(facetAttr);
                var baseExcluded = AttributeParser.ExtractExcludedMembers(facetAttr);

                foreach (var (sourceMember, _, _) in GeneratorUtilities.GetAllMembersWithModifiers(baseSourceType))
                {
                    if (sourceMember is not IPropertySymbol sourceProp ||
                        sourceProp.DeclaredAccessibility != Accessibility.Public)
                        continue;

                    bool shouldInclude = baseIsIncludeMode
                        ? baseIncluded.Contains(sourceProp.Name)
                        : !baseExcluded.Contains(sourceProp.Name);

                    if (shouldInclude && !memberNames.Contains(sourceProp.Name))
                    {
                        memberNames.Add(sourceProp.Name);
                    }
                }
            }

            baseType = baseType.BaseType;
        }

        return memberNames.ToImmutableArray();
    }

    /// <summary>
    /// Returns true when any base class of the target type declares a member whose name matches
    /// one of the name-hidden members Facet generates (ToSource, BackTo, Projection), OR when
    /// a base class is itself decorated with [Facet] (meaning those members will be generated
    /// for it, even though source generators cannot see their own output at analysis time).
    /// When true, the 'new' modifier must be emitted on those members to suppress CS0108.
    /// <para/>
    /// <c>FromSource</c> is excluded because it takes the source type as a parameter. When
    /// the derived facet maps a different source type, the parameter types differ and the
    /// method does not actually hide the base method. Emitting <c>new</c> in that case
    /// produces CS0109. Use <see cref="BaseHidesFromSource"/> instead.
    /// </summary>
    private static bool BaseHidesFacetMembers(INamedTypeSymbol targetSymbol)
    {
        // FromSource only hides when the parameter type matches; BaseHidesFromSource handles that.
        var nameHiddenMembers = new System.Collections.Generic.HashSet<string>
        {
            "ToSource", "BackTo", "Projection"
        };

        var baseType = targetSymbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            var baseFacetAttrs = baseType.GetAttributes()
                .Where(a => a.AttributeClass?.ToDisplayString() == FacetConstants.FacetAttributeFullName)
                .ToArray();

            // Single-source base facets use the default member names; multi-source facets do not.
            if (baseFacetAttrs.Length == 1)
            {
                return true;
            }

            foreach (var member in baseType.GetMembers())
            {
                if (nameHiddenMembers.Contains(member.Name) &&
                    member.DeclaredAccessibility == Accessibility.Public)
                {
                    return true;
                }
            }
            baseType = baseType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Returns true when any base class of the target type has a <c>FromSource</c> method
    /// whose single parameter type matches <paramref name="sourceTypeName"/>. This covers
    /// both explicit declarations and the case where a base Facet maps the same source type.
    /// </summary>
    private static bool BaseHidesFromSource(INamedTypeSymbol targetSymbol, string sourceTypeName)
    {
        var baseType = targetSymbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            // A base facet only hides FromSource when it maps the same source type.
            foreach (var attr in baseType.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == FacetConstants.FacetAttributeFullName &&
                    attr.ConstructorArguments.Length > 0 &&
                    attr.ConstructorArguments[0].Value is INamedTypeSymbol baseFacetSourceType)
                {
                    var baseSourceName = baseFacetSourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (baseSourceName == sourceTypeName)
                        return true;
                }
            }

            foreach (var member in baseType.GetMembers("FromSource"))
            {
                if (member is IMethodSymbol method &&
                    method.DeclaredAccessibility == Accessibility.Public &&
                    method.Parameters.Length == 1 &&
                    method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == sourceTypeName)
                {
                    return true;
                }
            }

            baseType = baseType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Returns true when any single-source base Facet of the target also has
    /// <c>GenerateToSource = true</c>, meaning it generates <c>ToSource()</c>,
    /// <c>BackTo()</c>, and <c>ApplyToSource()</c> methods that the derived class would hide.
    /// Emitting <c>new</c> on those methods without this being true causes CS0109.
    /// </summary>
    private static bool BaseHidesToSourceMembers(INamedTypeSymbol targetSymbol)
    {
        var baseType = targetSymbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            var baseFacetAttrs = baseType.GetAttributes()
                .Where(a => a.AttributeClass?.ToDisplayString() == FacetConstants.FacetAttributeFullName)
                .ToArray();

            // Only single-source facets use the default ToSource/BackTo/ApplyToSource names.
            if (baseFacetAttrs.Length == 1)
            {
                var generateToSource = baseFacetAttrs[0].NamedArguments
                    .FirstOrDefault(a => a.Key == FacetConstants.AttributeNames.GenerateToSource)
                    .Value;

                if (generateToSource.Value is true)
                    return true;
            }

            foreach (var member in baseType.GetMembers())
            {
                if ((member.Name == "ToSource" || member.Name == "BackTo" || member.Name == "ApplyToSource") &&
                    member.DeclaredAccessibility == Accessibility.Public)
                    return true;
            }

            baseType = baseType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Gets information about the base Facet class if the target inherits from another Facet.
    /// Returns null if the base class is not a Facet.
    /// </summary>
    private static BaseFacetInfo? GetBaseFacetInfo(INamedTypeSymbol targetSymbol, INamedTypeSymbol derivedSourceType, Compilation compilation)
    {
        string? nearestBaseTypeName = null;
        string? nearestBaseSourceTypeName = null;
        string? nearestBaseConfigurationTypeName = null;
        string? configurationSourceTypeName = null;
        string? configurationTargetTypeName = null;
        var allIncludedMembers = new List<string>();
        var allNestedFacetMappings = new Dictionary<string, (string childFacetTypeName, string sourceTypeName)>();
        var allBaseProjectionConfigs = new List<(string ConfigTypeName, string SourceTypeName, string TargetTypeName)>();
        bool foundAny = false;
        int nearestBaseFacetCount = 0;

        var baseType = targetSymbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            var facetAttrs = baseType.GetAttributes()
                .Where(a => a.AttributeClass?.ToDisplayString() == FacetConstants.FacetAttributeFullName)
                .ToList();

            if (facetAttrs.Count > 0)
            {
                AttributeData? bestFacetAttr = null;
                INamedTypeSymbol? bestBaseSourceType = null;
                int bestDistance = int.MaxValue;

                foreach (var facetAttr in facetAttrs)
                {
                    if (facetAttr.ConstructorArguments.Length == 0 ||
                        facetAttr.ConstructorArguments[0].Value is not INamedTypeSymbol baseSourceType)
                    {
                        continue;
                    }

                    var distance = GetInheritanceDistance(derivedSourceType, baseSourceType);
                    if (distance == null)
                        continue;

                    if (distance.Value < bestDistance)
                    {
                        bestDistance = distance.Value;
                        bestFacetAttr = facetAttr;
                        bestBaseSourceType = baseSourceType;
                        continue;
                    }

                    if (distance.Value == bestDistance &&
                        bestFacetAttr != null &&
                        !bestFacetAttr.NamedArguments.Any(arg => arg.Key == "Configuration") &&
                        facetAttr.NamedArguments.Any(arg => arg.Key == "Configuration"))
                    {
                        bestFacetAttr = facetAttr;
                        bestBaseSourceType = baseSourceType;
                    }
                }

                if (bestFacetAttr != null && bestBaseSourceType != null)
                {
                    if (!foundAny)
                    {
                        nearestBaseTypeName = baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        nearestBaseSourceTypeName = bestBaseSourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        foundAny = true;
                        nearestBaseFacetCount = facetAttrs.Count;
                    }

                    {
                        var configArg = bestFacetAttr.NamedArguments.FirstOrDefault(arg => arg.Key == "Configuration");
                        if (!configArg.Equals(default(KeyValuePair<string, TypedConstant>)))
                        {
                            var configType = configArg.Value.Value as INamedTypeSymbol;
                            if (configType != null)
                            {
                                var projectionMapConfigInterface = compilation.GetTypeByMetadataName(
                                    FacetConstants.ProjectionMapConfigurationInterfaceFullName);

                                if (projectionMapConfigInterface != null)
                                {
                                    var implementsProjectionConfig = configType.AllInterfaces.Any(i =>
                                        SymbolEqualityComparer.Default.Equals(i.ConstructedFrom, projectionMapConfigInterface) &&
                                        i.TypeArguments.Length == 2 &&
                                        SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], bestBaseSourceType) &&
                                        SymbolEqualityComparer.Default.Equals(i.TypeArguments[1], baseType));

                                    if (implementsProjectionConfig)
                                    {
                                        var cfgTypeName = configType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                        var cfgSourceTypeName = bestBaseSourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                        var cfgTargetTypeName = baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                                        allBaseProjectionConfigs.Add((cfgTypeName, cfgSourceTypeName, cfgTargetTypeName));

                                        if (nearestBaseConfigurationTypeName == null)
                                        {
                                            nearestBaseConfigurationTypeName = cfgTypeName;
                                            configurationSourceTypeName = cfgSourceTypeName;
                                            configurationTargetTypeName = cfgTargetTypeName;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    var (baseIncludedMembers, _) = AttributeParser.ExtractIncludedMembers(bestFacetAttr);
                    if (baseIncludedMembers.Count > 0)
                    {
                        foreach (var member in baseIncludedMembers)
                        {
                            if (!allIncludedMembers.Contains(member))
                            {
                                allIncludedMembers.Add(member);
                            }
                        }
                    }

                    var baseNestedFacetMappings = AttributeParser.ExtractNestedFacetMappings(bestFacetAttr);
                    foreach (var mapping in baseNestedFacetMappings)
                    {
                        if (!allNestedFacetMappings.ContainsKey(mapping.Key))
                        {
                            allNestedFacetMappings[mapping.Key] = mapping.Value;
                        }
                    }
                }
            }

            baseType = baseType.BaseType;
        }

        if (!foundAny)
            return null;

        return new BaseFacetInfo(
            nearestBaseTypeName!,
            nearestBaseSourceTypeName!,
            nearestBaseConfigurationTypeName,
            allIncludedMembers.ToImmutableArray(),
            allNestedFacetMappings.ToImmutableDictionary(),
            isBaseSingleSource: nearestBaseFacetCount == 1,
            baseConfigurationSourceTypeName: configurationSourceTypeName,
            baseConfigurationTargetTypeName: configurationTargetTypeName,
            allBaseProjectionConfigs: allBaseProjectionConfigs.ToImmutableArray());
    }

    /// <summary>
    /// Returns the inheritance distance from <paramref name="derivedType"/> to <paramref name="candidateBaseType"/>.
    /// 0 means exact match, 1 means direct base type, etc. Returns null when not assignable.
    /// </summary>
    private static int? GetInheritanceDistance(INamedTypeSymbol derivedType, INamedTypeSymbol candidateBaseType)
    {
        var current = derivedType;
        var distance = 0;

        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, candidateBaseType))
                return distance;

            current = current.BaseType;
            distance++;
        }

        return null;
    }

    /// <summary>
    /// Extracts MapWhen attribute mappings from the target type's properties.
    /// Returns a dictionary mapping property names to (conditions, defaultValue, includeInProjection).
    /// </summary>
    private static Dictionary<string, (List<string> conditions, string? defaultValue, bool includeInProjection)> ExtractMapWhenMappings(
        INamedTypeSymbol targetSymbol)
    {
        var mappings = new Dictionary<string, (List<string> conditions, string? defaultValue, bool includeInProjection)>();

        foreach (var member in targetSymbol.GetMembers())
        {
            if (member is not IPropertySymbol property) continue;

            var conditions = new List<string>();
            string? defaultValue = null;
            bool includeInProjection = true;

            foreach (var attr in property.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == FacetConstants.MapWhenAttributeFullName)
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string condition)
                    {
                        conditions.Add(condition);

                        foreach (var namedArg in attr.NamedArguments)
                        {
                            if (namedArg.Key == "Default" && namedArg.Value.Value != null)
                            {
                                defaultValue = ConvertDefaultValueToString(namedArg.Value);
                            }
                            else if (namedArg.Key == "IncludeInProjection" && namedArg.Value.Value is bool incProj)
                            {
                                includeInProjection = incProj;
                            }
                        }
                    }
                }
            }

            if (conditions.Count > 0)
            {
                mappings[property.Name] = (conditions, defaultValue, includeInProjection);
            }
        }

        return mappings;
    }

    /// <summary>
    /// Converts a TypedConstant default value to its C# string representation.
    /// </summary>
    private static string? ConvertDefaultValueToString(TypedConstant value)
    {
        if (value.IsNull)
            return "null";

        return value.Value switch
        {
            string s => $"\"{s.Replace("\"", "\\\"")}\"",
            char c => $"'{c}'",
            bool b => b ? "true" : "false",
            int i => i.ToString(),
            long l => $"{l}L",
            float f => $"{f}f",
            double d => $"{d}d",
            decimal m => $"{m}m",
            _ => value.Value?.ToString()
        };
    }

    /// <summary>
    /// Checks if an expression is a nameof() call.
    /// </summary>
    private static bool IsNameOfExpression(ExpressionSyntax? expr)
    {
        if (expr is InvocationExpressionSyntax invocation)
        {
            var invokedExpr = invocation.Expression;
            var invokedName = invokedExpr switch
            {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                MemberAccessExpressionSyntax ma when ma.Name is IdentifierNameSyntax id2 => id2.Identifier.ValueText,
                _ => null
            };

            return string.Equals(invokedName, "nameof", System.StringComparison.Ordinal);
        }

        return false;
    }

    private static ImmutableArray<string> CollectSourcePropertyNames(INamedTypeSymbol sourceType)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var current = (INamedTypeSymbol?)sourceType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.Kind == SymbolKind.Property || member.Kind == SymbolKind.Field)
                    names.Add(member.Name);
            }
            current = current.BaseType;
        }
        return names.ToImmutableArray();
    }

    /// <summary>
    /// Returns fully-qualified names (with <c>global::</c>) of the user-declared base class
    /// and interfaces on <paramref name="targetSymbol"/>. Used to preserve the type's
    /// inheritance chain in the <c>.Properties.g.cs</c> split file.
    /// <para>
    /// <c>IEquatable&lt;T&gt;</c> is excluded when <paramref name="generateEquality"/> is
    /// <see langword="true"/> because Facet emits that interface itself on the Mappings partial.
    /// </para>
    /// </summary>
    private static ImmutableArray<string> GetDeclaredBaseTypeNames(INamedTypeSymbol targetSymbol, bool generateEquality)
    {
        var result = ImmutableArray.CreateBuilder<string>();

        if (targetSymbol.BaseType is { SpecialType: not SpecialType.System_Object and not SpecialType.System_ValueType })
            result.Add(targetSymbol.BaseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        foreach (var iface in targetSymbol.Interfaces)
        {
            if (generateEquality &&
                iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.IEquatable<T>")
                continue;

            result.Add(iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return result.ToImmutable();
    }

    #endregion
}
