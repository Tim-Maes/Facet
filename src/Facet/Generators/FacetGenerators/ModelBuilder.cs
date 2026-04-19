using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        // Parse attribute arguments
        var excluded = AttributeParser.ExtractExcludedMembers(attribute);
        var (included, isIncludeMode) = AttributeParser.ExtractIncludedMembers(attribute);

        // Extract configuration settings - use global defaults when not explicitly set on the attribute
        var includeFields = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.IncludeFields, globalDefaults.IncludeFields);
        var generateConstructor = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateConstructor, globalDefaults.GenerateConstructor);
        var generateParameterlessConstructor = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateParameterlessConstructor, globalDefaults.GenerateParameterlessConstructor);
        var chainToParameterlessConstructor = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.ChainToParameterlessConstructor, globalDefaults.ChainToParameterlessConstructor);
        var generateProjection = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateProjection, globalDefaults.GenerateProjection);
        var generateToSource = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateToSource, globalDefaults.GenerateToSource);
        var configurationTypeName = AttributeParser.ExtractConfigurationTypeName(attribute);
        var beforeMapConfigurationTypeName = AttributeParser.ExtractBeforeMapConfigurationTypeName(attribute);
        var afterMapConfigurationTypeName = AttributeParser.ExtractAfterMapConfigurationTypeName(attribute);

        // Infer the type kind and whether it's a record from the target type declaration
        var (typeKind, isRecord) = TypeAnalyzer.InferTypeKind(targetSymbol);

        // Get the accessibility modifier
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

        // For record types, default to preserving init-only and required modifiers
        // unless explicitly overridden by the user
        var preserveInitOnlyDefault = isRecord;
        var preserveRequiredDefault = isRecord;

        var preserveInitOnly = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.PreserveInitOnlyProperties, preserveInitOnlyDefault);
        var preserveRequired = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.PreserveRequiredProperties, preserveRequiredDefault);
        var nullableProperties = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.NullableProperties, globalDefaults.NullableProperties);
        var copyAttributes = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.CopyAttributes, globalDefaults.CopyAttributes);
        var maxDepth = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.MaxDepth, globalDefaults.MaxDepth);
        var preserveReferences = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.PreserveReferences, globalDefaults.PreserveReferences);

        // Extract ConvertEnumsTo parameter
        var convertEnumsTo = AttributeParser.ExtractConvertEnumsTo(attribute);

        // Extract GenerateCopyConstructor and GenerateEquality parameters - use global defaults
        var generateCopyConstructor = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateCopyConstructor, globalDefaults.GenerateCopyConstructor);
        var generateEquality = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateEquality, globalDefaults.GenerateEquality);

        // Extract ToSourceConfiguration parameter
        var toSourceConfigurationTypeName = AttributeParser.ExtractToSourceConfigurationTypeName(attribute);

        // Detect if the configuration type implements IFacetProjectionMapConfiguration<TSource, TTarget>
        // and/or IFacetMapConfiguration<TSource, TTarget>
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

        // Extract CollectionTargetType parameter
        var collectionTargetType = AttributeParser.ExtractCollectionTargetType(attribute);

        // Extract nested facets parameter and build mapping from source type to child facet type
        var nestedFacetMappings = AttributeParser.ExtractNestedFacetMappings(attribute, context.SemanticModel.Compilation);

        // Extract MapFrom attribute mappings from target type properties
        var expressionMembers = new List<FacetMember>();
        var mapFromMappings = ExtractMapFromMappings(targetSymbol, expressionMembers, nullableProperties);

        // Extract MapWhen attribute mappings from target type properties
        var mapWhenMappings = ExtractMapWhenMappings(targetSymbol);

        // Extract type-level XML documentation from the source type
        var typeXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(sourceType);

        // Collect base class member names early, needed by ExtractMembers to auto-include
        var baseClassMemberNames = GetBaseClassMemberNames(targetSymbol);

        // Get base Facet info early so we can merge Include properties from the base Facet
        var baseFacetInfo = GetBaseFacetInfo(targetSymbol, sourceType, context.SemanticModel.Compilation);

        // If the target inherits from another Facet that has Include properties, merge them
        if (baseFacetInfo != null && !baseFacetInfo.IncludedMembers.IsDefaultOrEmpty)
        {
            foreach (var baseIncludedMember in baseFacetInfo.IncludedMembers)
            {
                included.Add(baseIncludedMember);
            }
        }

        // If the target inherits from another Facet that has NestedFacets, merge them
        if (baseFacetInfo != null && !baseFacetInfo.NestedFacetMappings.IsEmpty)
        {
            foreach (var baseNestedMapping in baseFacetInfo.NestedFacetMappings)
            {
                // Only add if not already defined in derived Facet (derived takes precedence)
                if (!nestedFacetMappings.ContainsKey(baseNestedMapping.Key))
                {
                    nestedFacetMappings[baseNestedMapping.Key] = baseNestedMapping.Value;
                }
            }
        }

        // Build members
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
            nestedFacetMappings,
            mapFromMappings,
            mapWhenMappings,
            convertEnumsTo,
            baseClassMemberNames,
            collectionTargetType,
            token);

        // Add expression-based members (from MapFrom with expressions)
        if (expressionMembers.Count > 0)
        {
            members = members.AddRange(expressionMembers);
        }

        // Get the namespace early - needed for fullName calculation (GitHub issue #249)
        var ns = targetSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : targetSymbol.ContainingNamespace.ToDisplayString();

        // Determine full name - use global default
        var useFullName = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.UseFullName, globalDefaults.UseFullName);

        // Get containing types for nested classes
        var containingTypes = TypeAnalyzer.GetContainingTypes(targetSymbol);

        // For nested classes, automatically use hierarchical name to avoid collisions
        // even if UseFullName is false
        string fullName;
        if (useFullName)
        {
            fullName = targetSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).GetSafeName();
        }
        else if (containingTypes.Length > 0)
        {
            // Build hierarchical name: ParentClass.NestedClass
            fullName = string.Join(".", containingTypes) + "." + targetSymbol.Name;
        }
        else if (ns != null)
        {
            // Include namespace to avoid collisions for types with the same name in different namespaces
            // (GitHub issue #249)
            fullName = ns + "." + targetSymbol.Name;
        }
        else
        {
            fullName = targetSymbol.Name;
        }

        // Get containing types for the source type (to detect nesting in static classes)
        var sourceContainingTypes = TypeAnalyzer.GetContainingTypes(sourceType);

        // Check if the target type already has a primary constructor
        var hasExistingPrimaryConstructor = TypeAnalyzer.HasExistingPrimaryConstructor(targetSymbol);

        // Check if the source type has a positional constructor
        var hasPositionalConstructor = TypeAnalyzer.HasPositionalConstructor(sourceType);

        // Check if ToSource can actually be generated (GitHub issue #220)
        // If the source type doesn't have an accessible parameterless constructor or has inaccessible setters,
        // skip ToSource generation to avoid compilation errors
        if (generateToSource && !hasPositionalConstructor)
        {
            // Nested types in C# have access to all members of their containing type, including private
            var isNestedInSource = TypeAnalyzer.IsNestedInsideType(targetSymbol, sourceType);

            // For non-positional types, we need a parameterless constructor and accessible setters
            var hasAccessibleConstructor = TypeAnalyzer.HasAccessibleParameterlessConstructor(sourceType, context.SemanticModel.Compilation.Assembly, isNestedInSource);
            var hasAccessibleSetters = TypeAnalyzer.AllPropertiesHaveAccessibleSetters(sourceType, members, isNestedInSource, context.SemanticModel.Compilation.Assembly);

            if (!hasAccessibleConstructor || !hasAccessibleSetters)
            {
                // Cannot generate ToSource - disable it silently
                // Note: Users can still manually write their own ToSource method if needed
                generateToSource = false;
            }
        }

        // Extract FlattenTo types for generating collection flattening methods
        var flattenToTypes = AttributeParser.ExtractFlattenToTypes(attribute);

        // Check if the base class already declares any of the generated members
        var sourceTypeFullName = sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var baseHidesFacetMembers = BaseHidesFacetMembers(targetSymbol);
        var baseHidesFromSource = BaseHidesFromSource(targetSymbol, sourceTypeFullName);

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
            baseFacetInfo);
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
        Dictionary<string, (string childFacetTypeName, string sourceTypeName)> nestedFacetMappings,
        Dictionary<string, (string targetName, string source, bool reversible, bool includeInProjection, string typeName, string? asCollection, bool isTargetRequired)> mapFromMappings,
        Dictionary<string, (List<string> conditions, string? defaultValue, bool includeInProjection)> mapWhenMappings,
        string? convertEnumsTo,
        ImmutableArray<string> baseClassMemberNames,
        string? collectionTargetType,
        CancellationToken token)
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

            // In Include mode, also include source properties that match properties inherited
            // by the facet from its base class. These properties won't be generated (MemberGenerator
            // skips them) but need to be in the model for ToSource/FromSource/projection mappings.
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
                    nestedFacetMappings,
                    mapFromMappings,
                    mapWhenMappings,
                    convertEnumsTo,
                    collectionTargetType,
                    members,
                    excludedRequiredMembers,
                    addedMembers);
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
        Dictionary<string, (string childFacetTypeName, string sourceTypeName)> nestedFacetMappings,
        Dictionary<string, (string targetName, string source, bool reversible, bool includeInProjection, string typeName, string? asCollection, bool isTargetRequired)> mapFromMappings,
        Dictionary<string, (List<string> conditions, string? defaultValue, bool includeInProjection)> mapWhenMappings,
        string? convertEnumsTo,
        string? collectionTargetType,
        List<FacetMember> members,
        List<FacetMember> excludedRequiredMembers,
        HashSet<string> addedMembers)
    {
        var memberXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(property);

        // Check if this source property has a MapFrom attribute pointing to it
        var hasMapFrom = mapFromMappings.TryGetValue(property.Name, out var mapFromInfo);

        if (!shouldIncludeMember && !hasMapFrom)
        {
            // If this is a required member that was excluded, track it for ToSource generation
            if (isRequired)
            {
                excludedRequiredMembers.Add(new FacetMember(
                    property.Name,
                    GeneratorUtilities.GetTypeNameWithNullability(property.Type),
                    FacetMemberKind.Property,
                    property.Type.IsValueType,
                    isInitOnly,
                    isRequired,
                    false, // Properties are not readonly
                    memberXmlDocumentation));
            }
            return;
        }

        var shouldPreserveInitOnly = preserveInitOnly && isInitOnly;
        // Honor the target property's own 'required' modifier when it maps from a non-required source property.
        var shouldPreserveRequired = (preserveRequired && isRequired) || (hasMapFrom && mapFromInfo.isTargetRequired);

        var typeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);
        var propertyTypeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        bool isNestedFacet = false;
        string? nestedFacetSourceTypeName = null;
        bool isCollection = false;
        string? collectionWrapper = null;
        string? sourceCollectionWrapper = null;

        // Detect if the property type is a nested type (has a containing type)
        // This is needed to generate 'using static' instead of 'using' for the containing type
        bool isNestedType = GeneratorUtilities.IsNestedType(property.Type);

        // Check if the property type is nullable (reference types)
        bool isNullableReferenceType = property.Type.NullableAnnotation == NullableAnnotation.Annotated;
        bool shouldTreatAsNullable = isNullableReferenceType;

        if (!shouldTreatAsNullable && !property.Type.IsValueType)
        {
            // NotAnnotated means the property is in a #nullable enable context and the
            // type is explicitly declared as non-nullable. Respect the author's intent.
            // None means nullable annotations are not enabled — treat as nullable for safety.
            if (property.Type.NullableAnnotation != NullableAnnotation.NotAnnotated)
            {
                shouldTreatAsNullable = true;
            }
        }

        // Check if this property's type is a collection
        if (GeneratorUtilities.TryGetCollectionElementType(property.Type, out var elementType, out var wrapper))
        {
            // Check if the collection element type matches a child facet source type
            var elementTypeName = elementType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (nestedFacetMappings.TryGetValue(elementTypeName, out var nestedMapping))
            {
                // Apply CollectionTargetType override if set, otherwise preserve the source wrapper
                var effectiveWrapper = collectionTargetType ?? wrapper!;
                var sourceWrapper = (collectionTargetType != null && collectionTargetType != wrapper) ? wrapper : null;

                // Wrap the child facet type in the target collection type
                var wrappedType = GeneratorUtilities.WrapInCollectionType(nestedMapping.childFacetTypeName, effectiveWrapper);
                // Preserve nullability if the collection itself was nullable
                typeName = shouldTreatAsNullable ? wrappedType + "?" : wrappedType;
                isNestedFacet = true;
                isCollection = true;
                collectionWrapper = effectiveWrapper;
                sourceCollectionWrapper = sourceWrapper;
                nestedFacetSourceTypeName = nestedMapping.sourceTypeName;
            }
        }
        // Check if this property's type matches a child facet source type (non-collection)
        else if (nestedFacetMappings.TryGetValue(propertyTypeName, out var nestedMapping))
        {
            // Preserve nullability when assigning nested facet type name
            typeName = shouldTreatAsNullable
                ? nestedMapping.childFacetTypeName + "?"
                : nestedMapping.childFacetTypeName;
            isNestedFacet = true;
            nestedFacetSourceTypeName = nestedMapping.sourceTypeName;
        }

        // Store the source type name before applying NullableProperties
        var sourceMemberTypeName = typeName;

        // Apply NullableProperties setting to all properties, including nested facets
        if (nullableProperties)
        {
            typeName = GeneratorUtilities.MakeNullable(typeName);
        }

        // Extract copiable attributes and their namespaces if requested
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

        // Detect if the source property is a partial defining declaration (C# 13+).
        // We detect this to properly handle initializer extraction (partial defining declarations
        // cannot have initializers in C# 13+), but we do NOT propagate the partial modifier
        // to the generated target type. Generating a partial defining declaration would require
        // the user to provide an implementing declaration, which breaks the DTO use case.
        // It also doesn't work with other source generators (e.g., CommunityToolkit.Mvvm)
        // because source generators don't chain. (GitHub issue #277)
        var isSourcePartial = IsPartialDefiningProperty(property);

        // Extract property initializer/default value from source
        // Skip initializers for:
        // 1. Nested facets - the type changes and the initializer won't be compatible
        // 2. NullableProperties = true - query DTOs should default to null, not the source initializer
        // 3. Partial source properties - the source defining declaration cannot have initializers,
        //    so there's nothing to extract anyway
        string? defaultValue = null;
        if (!isNestedFacet && !nullableProperties && !isSourcePartial)
        {
            defaultValue = ExtractPropertyInitializer(property);
        }

        // Determine final member name and mapping properties
        var memberName = hasMapFrom ? mapFromInfo.targetName : property.Name;
        var mapFromSource = hasMapFrom ? mapFromInfo.source : null;
        var mapFromReversible = hasMapFrom ? mapFromInfo.reversible : true;
        var mapFromIncludeInProjection = hasMapFrom ? mapFromInfo.includeInProjection : true;
        var mapFromAsCollection = hasMapFrom ? mapFromInfo.asCollection : null;
        var sourcePropertyName = property.Name; // Always use the actual source property name

        // Get MapWhen conditions for this property (keyed by target property name)
        var hasMapWhen = mapWhenMappings.TryGetValue(memberName, out var mapWhenInfo);

        // User declared the property with [MapFrom] or [MapWhen]
        var isUserDeclared = hasMapFrom || hasMapWhen;
        var mapWhenConditions = hasMapWhen ? mapWhenInfo.conditions : null;
        var mapWhenDefault = hasMapWhen ? mapWhenInfo.defaultValue : null;
        var mapWhenIncludeInProjection = hasMapWhen ? mapWhenInfo.includeInProjection : true;

        // If user declared, use their type name instead
        if (hasMapFrom && !string.IsNullOrEmpty(mapFromInfo.typeName))
        {
            typeName = mapFromInfo.typeName;
            if (nullableProperties)
            {
                typeName = GeneratorUtilities.MakeNullable(typeName);
            }
        }

        // Apply AsCollection override from MapFrom attribute
        if (mapFromAsCollection != null && isCollection)
        {
            var originalWrapper = collectionWrapper;
            collectionWrapper = mapFromAsCollection;
            if (originalWrapper != mapFromAsCollection)
                sourceCollectionWrapper = originalWrapper;
        }

        // Enum conversion: if ConvertEnumsTo is set and this property is an enum type, convert it
        bool isEnumConversion = false;
        string? originalEnumTypeName = null;
        if (convertEnumsTo != null && !isNestedFacet && !isCollection && !isUserDeclared)
        {
            // Get the underlying type (strip nullable wrapper if present)
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

                // Update sourceMemberTypeName to reflect the original type before conversion
                sourceMemberTypeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);

                // Apply NullableProperties if needed
                if (nullableProperties)
                {
                    typeName = GeneratorUtilities.MakeNullable(typeName);
                }

                // Clear default value since it's an enum initializer and won't match the new type
                defaultValue = null;
            }
        }

        members.Add(new FacetMember(
            memberName,
            typeName,
            FacetMemberKind.Property,
            property.Type.IsValueType,
            shouldPreserveInitOnly,
            shouldPreserveRequired,
            false, // Properties are not readonly
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
            isPartial: false)); // Never propagate partial from source (GitHub issue #277)
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

                // Defining declaration: partial modifier present and no accessor has a body
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
        // Try to get the syntax for the property declaration
        foreach (var syntaxRef in property.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();
            if (syntax is PropertyDeclarationSyntax propSyntax && propSyntax.Initializer != null)
            {
                // Return the initializer value (the part after the '=')
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
        List<FacetMember> members,
        List<FacetMember> excludedRequiredMembers,
        HashSet<string> addedMembers)
    {
        var memberXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(field);

        if (!shouldIncludeMember)
        {
            // If this is a required field that was excluded, track it for ToSource generation
            if (isRequired)
            {
                excludedRequiredMembers.Add(new FacetMember(
                    field.Name,
                    GeneratorUtilities.GetTypeNameWithNullability(field.Type),
                    FacetMemberKind.Field,
                    field.Type.IsValueType,
                    false, // Fields don't have init-only
                    isRequired,
                    field.IsReadOnly, // Fields can be readonly
                    memberXmlDocumentation));
            }
            return;
        }

        var shouldPreserveRequired = preserveRequired && isRequired;

        var typeName = GeneratorUtilities.GetTypeNameWithNullability(field.Type);
        var sourceMemberTypeName = typeName; // Store source type before applying NullableProperties
        if (nullableProperties)
        {
            typeName = GeneratorUtilities.MakeNullable(typeName);
        }

        // Extract copiable attributes and their namespaces if requested
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

        // Extract field initializer/default value from source
        // Skip initializers when NullableProperties = true (query DTOs should default to null)
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
            false, // Fields don't have init-only
            shouldPreserveRequired,
            field.IsReadOnly, // Fields can be readonly
            memberXmlDocumentation,
            false, // Fields don't support nested facets
            null,
            attributes,
            false, // Fields are not collections
            null,  // No collection wrapper for fields
            null,  // sourceCollectionWrapper
            sourceMemberTypeName,
            null,  // mapFromSource
            false, // mapFromReversible
            true,  // mapFromIncludeInProjection
            null,  // sourcePropertyName
            false, // isUserDeclared
            null,  // mapWhenConditions
            null,  // mapWhenDefault
            true,  // mapWhenIncludeInProjection
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
        // Try to get the syntax for the field declaration
        foreach (var syntaxRef in field.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();
            if (syntax is VariableDeclaratorSyntax varSyntax && varSyntax.Initializer != null)
            {
                // Return the initializer value (the part after the '=')
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

    // Build the type chain from the most-base class up to (and including) the direct target.
    // Processing in this order means derived-class declarations overwrite base-class ones.
    var typeChain = new List<INamedTypeSymbol>();
    var ancestor = targetSymbol.BaseType;
    while (ancestor != null && ancestor.SpecialType != SpecialType.System_Object)
    {
        typeChain.Add(ancestor);
        ancestor = ancestor.BaseType;
    }
    typeChain.Reverse();       // most-base first
    typeChain.Add(targetSymbol); // direct target last (highest priority)

    // Track expression-member target names already added so we don't duplicate.
    // Using a dict here so the derived-class declaration (processed last) overwrites the base.
    var pendingExpressionMembers = new Dictionary<string, FacetMember>();

    foreach (var typeToProcess in typeChain)
    {
        foreach (var member in typeToProcess.GetMembers())
        {
        if (member is not IPropertySymbol property) continue;

        // Look for MapFrom attribute
        foreach (var attr in property.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == FacetConstants.MapFromAttributeFullName)
            {
                // Try to obtain the source string from the compiled attribute data first
                string? sourceFromData = null;
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string s)
                {
                    sourceFromData = s;
                }

                // Try to resolve original syntax (to detect `@` in nameof, etc.)
                string? source = sourceFromData;
                bool hadLeadingAt = false;
                if (attr.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax attrSyntax)
                {
                    var firstArgExpr = attrSyntax.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
                    if (firstArgExpr != null)
                    {
                        var (resolved, hadAt) = NameOfResolver.ResolveExpression(firstArgExpr);
                        // Only use the resolved value if it came from a nameof() expression with @ prefix
                        // For string literals, stick with the compiled data to avoid including quotes
                        if (!string.IsNullOrEmpty(resolved) && hadAt && IsNameOfExpression(firstArgExpr))
                        {
                            // When using @nameof(TypeName.PropertyPath), extract just the property path
                            // by removing the first segment (the source type name)
                            var segments = resolved.Split('.');
                            if (segments.Length > 1)
                            {
                                // Skip the first segment (type name) and rebuild the path
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
                    // nothing meaningful to do
                    break;
                }

                // Get named arguments
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

                // Get the property type name
                var typeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);
                if (nullableProperties)
                {
                    typeName = GeneratorUtilities.MakeNullable(typeName);
                }

                // If the argument is an expression, a nested path, or was written with @nameof(...) to force a full path,
                // treat it as an expression-based member / nested path.
                if (IsExpression(source) || source.Contains(".") || hadLeadingAt)
                {
                    // Store in dict — derived-class entry (processed last) overwrites base-class entry.
                    pendingExpressionMembers[property.Name] = new FacetMember(
                        property.Name,
                        typeName,
                        FacetMemberKind.Property,
                        property.Type.IsValueType,
                        false, // isInitOnly
                        false, // isRequired
                        false, // isReadOnly
                        null,  // xmlDocumentation
                        false, // isNestedFacet
                        null,  // nestedFacetSourceTypeName
                        null,  // attributes
                        false, // isCollection
                        null,  // collectionWrapper
                        null,  // sourceCollectionWrapper
                        null,  // sourceMemberTypeName
                        source, // mapFromSource
                        reversible,
                        includeInProjection,
                        property.Name, // sourcePropertyName (use target name as placeholder)
                        true); // isUserDeclared
                }
                else
                {
                    // Simple property rename - map to source property.
                    // Derived-class declaration overwrites base-class (last write wins since
                    // typeChain is ordered most-base first, direct target last).
                    mappings[source] = (property.Name, source, reversible, includeInProjection, typeName, asCollection, property.IsRequired);
                }
            }
        }
        }
    }

    // Flush expression members collected from the whole type chain
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
    /// </summary>
    private static ImmutableArray<string> GetBaseClassMemberNames(INamedTypeSymbol targetSymbol)
    {
        var memberNames = new List<string>();

        // Walk up the inheritance chain
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
        // Members hidden purely by name (properties and parameterless methods).
        // FromSource is NOT included because it takes a source-type parameter and
        // only hides when the parameter types match — handled by BaseHidesFromSource.
        var nameHiddenMembers = new System.Collections.Generic.HashSet<string>
        {
            "ToSource", "BackTo", "Projection"
        };

        var baseType = targetSymbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            // If the base class is itself a Facet, its Projection/ToSource/BackTo members are
            // generated by this same source generator and therefore not yet visible in the
            // semantic model. We must still emit 'new' to suppress CS0108.
            if (baseType.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == FacetConstants.FacetAttributeFullName))
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
            // If the base class is itself a Facet, check whether it maps the same source type.
            // Only then will its generated FromSource have a matching parameter type.
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

            // Also check for manually declared FromSource methods with matching parameter type
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
    /// Gets information about the base Facet class if the target inherits from another Facet.
    /// Returns null if the base class is not a Facet.
    /// </summary>
    private static BaseFacetInfo? GetBaseFacetInfo(INamedTypeSymbol targetSymbol, INamedTypeSymbol derivedSourceType, Compilation compilation)
    {
        var baseType = targetSymbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            // A base Facet can have multiple [Facet] attributes (multi-source).
            // Select the attribute whose source type best matches the current derived source type.
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

                    // Tie-breaker for same source-distance:
                    // prefer the attribute with an explicit Configuration when the current best does not have one.
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
                    var baseTypeName = baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var baseSourceTypeName = bestBaseSourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // Extract the Configuration type if specified and compatible with the selected base source/target pair
                    string? baseConfigurationTypeName = null;
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
                                    baseConfigurationTypeName = configType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                }
                            }
                        }
                    }

                    // Extract Include/NestedFacets from the selected matching [Facet] attribute
                    var (baseIncludedMembers, _) = AttributeParser.ExtractIncludedMembers(bestFacetAttr);
                    var baseNestedFacetMappings = AttributeParser.ExtractNestedFacetMappings(bestFacetAttr, compilation);

                    return new BaseFacetInfo(
                        baseTypeName,
                        baseSourceTypeName,
                        baseConfigurationTypeName,
                        baseIncludedMembers.ToImmutableArray(),
                        baseNestedFacetMappings.ToImmutableDictionary());
                }
            }

            baseType = baseType.BaseType;
        }

        return null;
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

        // Get all members from the target type (user-declared properties)
        foreach (var member in targetSymbol.GetMembers())
        {
            if (member is not IPropertySymbol property) continue;

            var conditions = new List<string>();
            string? defaultValue = null;
            bool includeInProjection = true;

            // Look for MapWhen attributes (can have multiple)
            foreach (var attr in property.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == FacetConstants.MapWhenAttributeFullName)
                {
                    // Get the Condition constructor argument
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string condition)
                    {
                        conditions.Add(condition);

                        // Get named arguments
                        foreach (var namedArg in attr.NamedArguments)
                        {
                            if (namedArg.Key == "Default" && namedArg.Value.Value != null)
                            {
                                // Convert the default value to a string representation
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

    #endregion
}
