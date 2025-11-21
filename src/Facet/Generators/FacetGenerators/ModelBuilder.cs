using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Facet.Generators;

/// <summary>
/// Builds FacetTargetModel instances from attribute syntax contexts.
/// </summary>
internal static class ModelBuilder
{
    /// <summary>
    /// Builds a FacetTargetModel from the generator attribute syntax context.
    /// </summary>
    public static FacetTargetModel? BuildModel(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol targetSymbol) return null;
        if (context.Attributes.Length == 0) return null;

        var attribute = context.Attributes[0];
        token.ThrowIfCancellationRequested();

        var sourceType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        if (sourceType == null) return null;

        // Parse attribute arguments
        var excluded = AttributeParser.ExtractExcludedMembers(attribute);
        var (included, isIncludeMode) = AttributeParser.ExtractIncludedMembers(attribute);

        // Extract configuration settings
        var includeFields = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.IncludeFields, false);
        var generateConstructor = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateConstructor, true);
        var generateParameterlessConstructor = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateParameterlessConstructor, true);
        var generateProjection = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateProjection, true);
        // Support both GenerateToSource (new) and GenerateBackTo (deprecated) for backward compatibility
        var generateToSource = AttributeParser.HasNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateToSource)
            ? AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateToSource, false)
            : AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateBackTo, false);
        var configurationTypeName = AttributeParser.ExtractConfigurationTypeName(attribute);

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
        var nullableProperties = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.NullableProperties, false);
        var copyAttributes = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.CopyAttributes, false);
        var maxDepth = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.MaxDepth, FacetConstants.DefaultMaxDepth);
        var preserveReferences = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.PreserveReferences, FacetConstants.DefaultPreserveReferences);

        // Extract nested facets parameter and build mapping from source type to child facet type
        var nestedFacetMappings = AttributeParser.ExtractNestedFacetMappings(attribute, context.SemanticModel.Compilation);

        // Extract MapFrom attribute mappings from target type properties
        var mapFromMappings = ExtractMapFromMappings(targetSymbol);

        // Extract type-level XML documentation from the source type
        var typeXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(sourceType);

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
            token);

        // Determine full name
        var useFullName = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.UseFullName, false);
        string fullName = useFullName
            ? targetSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).GetSafeName()
            : targetSymbol.Name;

        var ns = targetSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : targetSymbol.ContainingNamespace.ToDisplayString();

        // Get containing types for nested classes
        var containingTypes = TypeAnalyzer.GetContainingTypes(targetSymbol);

        // Get containing types for the source type (to detect nesting in static classes)
        var sourceContainingTypes = TypeAnalyzer.GetContainingTypes(sourceType);

        // Check if the target type already has a primary constructor
        var hasExistingPrimaryConstructor = TypeAnalyzer.HasExistingPrimaryConstructor(targetSymbol);

        // Check if the source type has a positional constructor
        var hasPositionalConstructor = TypeAnalyzer.HasPositionalConstructor(sourceType);

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
            sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
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
            preserveReferences);
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
        Dictionary<string, (string targetName, string source, bool reversible, bool includeInProjection, string typeName)> mapFromMappings,
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
        Dictionary<string, (string targetName, string source, bool reversible, bool includeInProjection, string typeName)> mapFromMappings,
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
        var shouldPreserveRequired = preserveRequired && isRequired;

        var typeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);
        var propertyTypeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        bool isNestedFacet = false;
        string? nestedFacetSourceTypeName = null;
        bool isCollection = false;
        string? collectionWrapper = null;

        // Check if the property type is nullable (reference types)
        bool isNullableReferenceType = property.Type.NullableAnnotation == NullableAnnotation.Annotated;
        bool shouldTreatAsNullable = isNullableReferenceType;

        if (!shouldTreatAsNullable && !property.Type.IsValueType)
        {
            bool isExplicitlyNonNullable = property.Type.NullableAnnotation == NullableAnnotation.NotAnnotated &&
                                            property.IsRequired;

            if (!isExplicitlyNonNullable)
            {
                // treat as potentially nullable for safety
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
                // Wrap the child facet type in the same collection type
                var wrappedType = GeneratorUtilities.WrapInCollectionType(nestedMapping.childFacetTypeName, wrapper!);
                // Preserve nullability if the collection itself was nullable
                typeName = shouldTreatAsNullable ? wrappedType + "?" : wrappedType;
                isNestedFacet = true;
                isCollection = true;
                collectionWrapper = wrapper;
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

        // Extract copiable attributes if requested
        var attributes = copyAttributes
            ? AttributeProcessor.ExtractCopiableAttributes(property, FacetMemberKind.Property)
            : new List<string>();

        // Determine final member name and mapping properties
        var memberName = hasMapFrom ? mapFromInfo.targetName : property.Name;
        var mapFromSource = hasMapFrom ? mapFromInfo.source : null;
        var mapFromReversible = hasMapFrom ? mapFromInfo.reversible : true;
        var mapFromIncludeInProjection = hasMapFrom ? mapFromInfo.includeInProjection : true;
        var sourcePropertyName = property.Name; // Always use the actual source property name
        var isUserDeclared = hasMapFrom; // User declared the property with [MapFrom]

        // If user declared, use their type name instead
        if (hasMapFrom && !string.IsNullOrEmpty(mapFromInfo.typeName))
        {
            typeName = mapFromInfo.typeName;
            if (nullableProperties)
            {
                typeName = GeneratorUtilities.MakeNullable(typeName);
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
            sourceMemberTypeName,
            mapFromSource,
            mapFromReversible,
            mapFromIncludeInProjection,
            sourcePropertyName,
            isUserDeclared));
        addedMembers.Add(memberName);
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

        // Extract copiable attributes if requested
        var attributes = copyAttributes
            ? AttributeProcessor.ExtractCopiableAttributes(field, FacetMemberKind.Field)
            : new List<string>();

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
            sourceMemberTypeName));
        addedMembers.Add(field.Name);
    }

    /// <summary>
    /// Extracts MapFrom attribute mappings from the target type's properties.
    /// Returns a dictionary mapping source property names to (targetName, source, reversible, includeInProjection, typeName).
    /// </summary>
    private static Dictionary<string, (string targetName, string source, bool reversible, bool includeInProjection, string typeName)> ExtractMapFromMappings(
        INamedTypeSymbol targetSymbol)
    {
        var mappings = new Dictionary<string, (string targetName, string source, bool reversible, bool includeInProjection, string typeName)>();

        // Get all members from the target type (user-declared properties)
        foreach (var member in targetSymbol.GetMembers())
        {
            if (member is not IPropertySymbol property) continue;

            // Look for MapFrom attribute
            foreach (var attr in property.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == FacetConstants.MapFromAttributeFullName)
                {
                    // Get the Source constructor argument
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string source)
                    {
                        // Parse the source to get the property name (handle nested paths like "Company.Name")
                        var sourcePropertyName = source.Contains(".") ? source.Split('.')[0] : source;

                        // Get named arguments
                        var reversible = true;
                        var includeInProjection = true;

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
                        }

                        // Get the property type name
                        var typeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);

                        mappings[sourcePropertyName] = (property.Name, source, reversible, includeInProjection, typeName);
                    }
                    break;
                }
            }
        }

        return mappings;
    }

    #endregion
}
