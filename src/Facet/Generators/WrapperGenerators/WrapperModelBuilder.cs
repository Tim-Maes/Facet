using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Facet.Generators;

/// <summary>
/// Builds WrapperTargetModel instances from attribute syntax contexts.
/// </summary>
internal static class WrapperModelBuilder
{
    /// <summary>
    /// Builds a WrapperTargetModel from the generator attribute syntax context.
    /// </summary>
    public static WrapperTargetModel? BuildModel(GeneratorAttributeSyntaxContext context, CancellationToken token)
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
        var copyAttributes = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.CopyAttributes, false);
        var useFullName = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.UseFullName, false);
        var readOnly = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.ReadOnly, false);

        // Extract nested wrapper mappings
        var nestedWrapperMappings = AttributeParser.ExtractNestedWrapperMappings(attribute, context.SemanticModel.Compilation);

        // Infer the type kind and whether it's a record from the target type declaration
        var (typeKind, isRecord) = TypeAnalyzer.InferTypeKind(targetSymbol);

        // Extract type-level XML documentation from the source type
        var typeXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(sourceType);

        // Build members
        var members = ExtractMembers(
            sourceType,
            excluded,
            included,
            isIncludeMode,
            includeFields,
            copyAttributes,
            nestedWrapperMappings,
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

        // Get containing types for the source type
        var sourceContainingTypes = TypeAnalyzer.GetContainingTypes(sourceType);

        return new WrapperTargetModel(
            targetSymbol.Name,
            ns,
            fullName,
            typeKind,
            isRecord,
            sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            sourceContainingTypes,
            members,
            typeXmlDocumentation,
            containingTypes,
            useFullName,
            copyAttributes,
            readOnly);
    }

    #region Private Helper Methods

    private static ImmutableArray<FacetMember> ExtractMembers(
        INamedTypeSymbol sourceType,
        HashSet<string> excluded,
        HashSet<string> included,
        bool isIncludeMode,
        bool includeFields,
        bool copyAttributes,
        Dictionary<string, (string childWrapperTypeName, string sourceTypeName)> nestedWrapperMappings,
        CancellationToken token)
    {
        var members = new List<FacetMember>();
        var addedMembers = new HashSet<string>();

        var allMembersWithModifiers = GeneratorUtilities.GetAllMembersWithModifiers(sourceType);

        foreach (var (member, isInitOnly, isRequired) in allMembersWithModifiers)
        {
            token.ThrowIfCancellationRequested();

            if (addedMembers.Contains(member.Name)) continue;

            bool shouldIncludeMember = isIncludeMode
                ? included.Contains(member.Name)
                : !excluded.Contains(member.Name);

            if (!shouldIncludeMember) continue;

            if (member is IPropertySymbol property && property.DeclaredAccessibility == Accessibility.Public)
            {
                ProcessProperty(property, copyAttributes, nestedWrapperMappings, members, addedMembers);
            }
            else if (includeFields && member is IFieldSymbol field && field.DeclaredAccessibility == Accessibility.Public)
            {
                ProcessField(field, copyAttributes, nestedWrapperMappings, members, addedMembers);
            }
        }

        return members.ToImmutableArray();
    }

    private static void ProcessProperty(
        IPropertySymbol property,
        bool copyAttributes,
        Dictionary<string, (string childWrapperTypeName, string sourceTypeName)> nestedWrapperMappings,
        List<FacetMember> members,
        HashSet<string> addedMembers)
    {
        var memberXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(property);
        var typeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);
        var propertyTypeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        bool isNestedWrapper = false;
        string? nestedWrapperSourceTypeName = null;

        // Check if this property's type matches a nested wrapper source type
        if (nestedWrapperMappings.TryGetValue(propertyTypeName, out var nestedMapping))
        {
            // Replace the type name with the nested wrapper type
            bool isNullable = property.Type.NullableAnnotation == NullableAnnotation.Annotated;
            typeName = isNullable ? nestedMapping.childWrapperTypeName + "?" : nestedMapping.childWrapperTypeName;
            isNestedWrapper = true;
            nestedWrapperSourceTypeName = nestedMapping.sourceTypeName;
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

        members.Add(new FacetMember(
            property.Name,
            typeName,
            FacetMemberKind.Property,
            property.Type.IsValueType,
            false, // isInitOnly
            false, // isRequired
            false, // isReadonly
            memberXmlDocumentation,
            isNestedWrapper,
            nestedWrapperSourceTypeName,
            attributes,
            false, // isCollection
            null,  // collectionWrapper
            GeneratorUtilities.GetTypeNameWithNullability(property.Type), // sourceMemberTypeName
            null,  // mapFromSource
            false, // mapFromReversible
            true,  // mapFromIncludeInProjection
            null,  // sourcePropertyName
            false, // isUserDeclared
            null,  // mapWhenConditions
            null,  // mapWhenDefault
            true,  // mapWhenIncludeInProjection
            attributeNamespaces));
        addedMembers.Add(property.Name);
    }

    private static void ProcessField(
        IFieldSymbol field,
        bool copyAttributes,
        Dictionary<string, (string childWrapperTypeName, string sourceTypeName)> nestedWrapperMappings,
        List<FacetMember> members,
        HashSet<string> addedMembers)
    {
        var memberXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(field);
        var typeName = GeneratorUtilities.GetTypeNameWithNullability(field.Type);
        var fieldTypeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        bool isNestedWrapper = false;
        string? nestedWrapperSourceTypeName = null;

        // Check if this field's type matches a nested wrapper source type
        if (nestedWrapperMappings.TryGetValue(fieldTypeName, out var nestedMapping))
        {
            // Replace the type name with the nested wrapper type
            bool isNullable = field.Type.NullableAnnotation == NullableAnnotation.Annotated;
            typeName = isNullable ? nestedMapping.childWrapperTypeName + "?" : nestedMapping.childWrapperTypeName;
            isNestedWrapper = true;
            nestedWrapperSourceTypeName = nestedMapping.sourceTypeName;
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

        members.Add(new FacetMember(
            field.Name,
            typeName,
            FacetMemberKind.Field,
            field.Type.IsValueType,
            false, // isInitOnly
            false, // isRequired
            field.IsReadOnly,
            memberXmlDocumentation,
            isNestedWrapper,
            nestedWrapperSourceTypeName,
            attributes,
            false, // isCollection
            null,
            GeneratorUtilities.GetTypeNameWithNullability(field.Type),
            null,  // mapFromSource
            false, // mapFromReversible
            true,  // mapFromIncludeInProjection
            null,  // sourcePropertyName
            false, // isUserDeclared
            null,  // mapWhenConditions
            null,  // mapWhenDefault
            true,  // mapWhenIncludeInProjection
            attributeNamespaces));
        addedMembers.Add(field.Name);
    }

    #endregion
}
