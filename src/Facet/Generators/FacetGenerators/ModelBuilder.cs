using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
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
        var generateToSource = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.GenerateToSource, false);
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
        var nullableProperties = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.NullableProperties, false);
        var copyAttributes = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.CopyAttributes, false);
        var maxDepth = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.MaxDepth, FacetConstants.DefaultMaxDepth);
        var preserveReferences = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.PreserveReferences, FacetConstants.DefaultPreserveReferences);

        // Extract nested facets parameter and build mapping from source type to child facet type
        var nestedFacetMappings = AttributeParser.ExtractNestedFacetMappings(attribute, context.SemanticModel.Compilation);

        // Extract MapFrom attribute mappings from target type properties
        var expressionMembers = new List<FacetMember>();
        var mapFromMappings = ExtractMapFromMappings(targetSymbol, expressionMembers, nullableProperties);

        // Extract MapWhen attribute mappings from target type properties
        var mapWhenMappings = ExtractMapWhenMappings(targetSymbol);

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
            mapWhenMappings,
            token);

        // Add expression-based members (from MapFrom with expressions)
        if (expressionMembers.Count > 0)
        {
            members = members.AddRange(expressionMembers);
        }

        // Determine full name
        var useFullName = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.UseFullName, false);

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
        else
        {
            fullName = targetSymbol.Name;
        }

        var ns = targetSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : targetSymbol.ContainingNamespace.ToDisplayString();

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
            // For non-positional types, we need a parameterless constructor and accessible setters
            var hasAccessibleConstructor = TypeAnalyzer.HasAccessibleParameterlessConstructor(sourceType);
            var hasAccessibleSetters = TypeAnalyzer.AllPropertiesHaveAccessibleSetters(sourceType, members);

            if (!hasAccessibleConstructor || !hasAccessibleSetters)
            {
                // Cannot generate ToSource - disable it silently
                // Note: Users can still manually write their own ToSource method if needed
                generateToSource = false;
            }
        }

        // Collect base class member names to avoid generating duplicate properties
        var baseClassMemberNames = GetBaseClassMemberNames(targetSymbol);

        // Extract FlattenTo types for generating collection flattening methods
        var flattenToTypes = AttributeParser.ExtractFlattenToTypes(attribute);

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
            preserveReferences,
            baseClassMemberNames,
            flattenToTypes,
            beforeMapConfigurationTypeName,
            afterMapConfigurationTypeName);
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
        Dictionary<string, (List<string> conditions, string? defaultValue, bool includeInProjection)> mapWhenMappings,
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
                    mapWhenMappings,
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
        Dictionary<string, (List<string> conditions, string? defaultValue, bool includeInProjection)> mapWhenMappings,
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

        // Extract property initializer/default value from source
        // Skip initializers for:
        // 1. Nested facets - the type changes and the initializer won't be compatible
        // 2. NullableProperties = true - query DTOs should default to null, not the source initializer
        string? defaultValue = null;
        if (!isNestedFacet && !nullableProperties)
        {
            defaultValue = ExtractPropertyInitializer(property);
        }

        // Determine final member name and mapping properties
        var memberName = hasMapFrom ? mapFromInfo.targetName : property.Name;
        var mapFromSource = hasMapFrom ? mapFromInfo.source : null;
        var mapFromReversible = hasMapFrom ? mapFromInfo.reversible : true;
        var mapFromIncludeInProjection = hasMapFrom ? mapFromInfo.includeInProjection : true;
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
            isUserDeclared,
            mapWhenConditions,
            mapWhenDefault,
            mapWhenIncludeInProjection,
            attributeNamespaces,
            defaultValue));
        addedMembers.Add(memberName);
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
    /// </summary>
    private static Dictionary<string, (string targetName, string source, bool reversible, bool includeInProjection, string typeName)> ExtractMapFromMappings(
        INamedTypeSymbol targetSymbol,
        List<FacetMember> expressionMembers,
        bool nullableProperties)
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
                        // Get named arguments
                        var reversible = false;
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
                        if (nullableProperties)
                        {
                            typeName = GeneratorUtilities.MakeNullable(typeName);
                        }

                        // Check if this is an expression (contains operators or spaces) or a nested property path
                        if (IsExpression(source) || source.Contains("."))
                        {
                            // Expression-based member or nested property path - add directly to members list
                            // Nested paths bypass source property processing since multiple target properties
                            // can map from different nested paths under the same source property
                            expressionMembers.Add(new FacetMember(
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
                                null,  // sourceMemberTypeName
                                source, // mapFromSource
                                reversible,
                                includeInProjection,
                                property.Name, // sourcePropertyName (use target name as placeholder)
                                true)); // isUserDeclared
                        }
                        else
                        {
                            // Simple property rename - map to source property
                            mappings[source] = (property.Name, source, reversible, includeInProjection, typeName);
                        }
                    }
                    break;
                }
            }
        }

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

    #endregion
}
