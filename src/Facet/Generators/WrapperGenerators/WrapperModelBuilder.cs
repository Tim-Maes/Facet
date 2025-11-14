using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
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
                ProcessProperty(property, copyAttributes, members, addedMembers);
            }
            else if (includeFields && member is IFieldSymbol field && field.DeclaredAccessibility == Accessibility.Public)
            {
                ProcessField(field, copyAttributes, members, addedMembers);
            }
        }

        return members.ToImmutableArray();
    }

    private static void ProcessProperty(
        IPropertySymbol property,
        bool copyAttributes,
        List<FacetMember> members,
        HashSet<string> addedMembers)
    {
        var memberXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(property);
        var typeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);

        // Extract copiable attributes if requested
        var attributes = copyAttributes
            ? AttributeProcessor.ExtractCopiableAttributes(property, FacetMemberKind.Property)
            : new List<string>();

        // Wrappers always have get/set properties (no init-only or required for V1 POC)
        members.Add(new FacetMember(
            property.Name,
            typeName,
            FacetMemberKind.Property,
            property.Type.IsValueType,
            false, // isInitOnly - wrappers are mutable
            false, // isRequired - skip for V1
            false, // isReadonly
            memberXmlDocumentation,
            false, // isNestedFacet - skip for V1
            null,  // nestedFacetSourceTypeName
            attributes,
            false, // isCollection - skip for V1
            null,  // collectionWrapper
            typeName)); // sourceMemberTypeName
        addedMembers.Add(property.Name);
    }

    private static void ProcessField(
        IFieldSymbol field,
        bool copyAttributes,
        List<FacetMember> members,
        HashSet<string> addedMembers)
    {
        var memberXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(field);
        var typeName = GeneratorUtilities.GetTypeNameWithNullability(field.Type);

        // Extract copiable attributes if requested
        var attributes = copyAttributes
            ? AttributeProcessor.ExtractCopiableAttributes(field, FacetMemberKind.Field)
            : new List<string>();

        members.Add(new FacetMember(
            field.Name,
            typeName,
            FacetMemberKind.Field,
            field.Type.IsValueType,
            false, // isInitOnly
            false, // isRequired - skip for V1
            field.IsReadOnly,
            memberXmlDocumentation,
            false, // isNestedFacet
            null,
            attributes,
            false, // isCollection
            null,
            typeName));
        addedMembers.Add(field.Name);
    }

    #endregion
}
