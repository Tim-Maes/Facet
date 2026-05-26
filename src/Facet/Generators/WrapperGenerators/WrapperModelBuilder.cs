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

        var excluded = AttributeParser.ExtractExcludedMembers(attribute);
        var (included, isIncludeMode) = AttributeParser.ExtractIncludedMembers(attribute);

        var includeFields = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.IncludeFields, false);
        var copyAttributes = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.CopyAttributes, false);
        var useFullName = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.UseFullName, false);
        var readOnly = AttributeParser.GetNamedArg(attribute.NamedArguments, FacetConstants.AttributeNames.ReadOnly, false);

        var nestedWrapperMappings = AttributeParser.ExtractNestedWrapperMappings(attribute);

        var (typeKind, isRecord) = TypeAnalyzer.InferTypeKind(targetSymbol);

        var externalDocProvider = new ExternalXmlDocProvider(context.SemanticModel.Compilation);

        var typeXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(sourceType, false, externalDocProvider);

        var members = ExtractMembers(
            sourceType,
            excluded,
            included,
            isIncludeMode,
            includeFields,
            copyAttributes,
            nestedWrapperMappings,
            externalDocProvider,
            token);

        string fullName = useFullName
            ? targetSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).GetSafeName()
            : targetSymbol.Name;

        var ns = targetSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : targetSymbol.ContainingNamespace.ToDisplayString();

        var containingTypes = TypeAnalyzer.GetContainingTypes(targetSymbol);

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
        ExternalXmlDocProvider? externalDocProvider,
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
                ProcessProperty(property, copyAttributes, nestedWrapperMappings, externalDocProvider, members, addedMembers);
            }
            else if (includeFields && member is IFieldSymbol field && field.DeclaredAccessibility == Accessibility.Public)
            {
                ProcessField(field, copyAttributes, nestedWrapperMappings, externalDocProvider, members, addedMembers);
            }
        }

        return members.ToImmutableArray();
    }

    private static void ProcessProperty(
        IPropertySymbol property,
        bool copyAttributes,
        Dictionary<string, (string childWrapperTypeName, string sourceTypeName)> nestedWrapperMappings,
        ExternalXmlDocProvider? externalDocProvider,
        List<FacetMember> members,
        HashSet<string> addedMembers)
    {
        var memberXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(property, false, externalDocProvider);
        var typeName = GeneratorUtilities.GetTypeNameWithNullability(property.Type);
        var propertyTypeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        bool isNestedWrapper = false;
        string? nestedWrapperSourceTypeName = null;

        if (nestedWrapperMappings.TryGetValue(propertyTypeName, out var nestedMapping))
        {
            bool isNullable = property.Type.NullableAnnotation == NullableAnnotation.Annotated;
            typeName = isNullable ? nestedMapping.childWrapperTypeName + "?" : nestedMapping.childWrapperTypeName;
            isNestedWrapper = true;
            nestedWrapperSourceTypeName = nestedMapping.sourceTypeName;
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

        members.Add(new FacetMember(
            property.Name,
            typeName,
            FacetMemberKind.Property,
            property.Type.IsValueType,
            false, 
            false, 
            false, 
            memberXmlDocumentation,
            isNestedWrapper,
            nestedWrapperSourceTypeName,
            attributes,
            false, 
            null,  
            null,  
            GeneratorUtilities.GetTypeNameWithNullability(property.Type), 
            null,  
            false, 
            true,  
            null,  
            false, 
            null,  
            null,  
            true,  
            attributeNamespaces));
        addedMembers.Add(property.Name);
    }

    private static void ProcessField(
        IFieldSymbol field,
        bool copyAttributes,
        Dictionary<string, (string childWrapperTypeName, string sourceTypeName)> nestedWrapperMappings,
        ExternalXmlDocProvider? externalDocProvider,
        List<FacetMember> members,
        HashSet<string> addedMembers)
    {
        var memberXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(field, false, externalDocProvider);
        var typeName = GeneratorUtilities.GetTypeNameWithNullability(field.Type);
        var fieldTypeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        bool isNestedWrapper = false;
        string? nestedWrapperSourceTypeName = null;

        if (nestedWrapperMappings.TryGetValue(fieldTypeName, out var nestedMapping))
        {
            bool isNullable = field.Type.NullableAnnotation == NullableAnnotation.Annotated;
            typeName = isNullable ? nestedMapping.childWrapperTypeName + "?" : nestedMapping.childWrapperTypeName;
            isNestedWrapper = true;
            nestedWrapperSourceTypeName = nestedMapping.sourceTypeName;
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

        members.Add(new FacetMember(
            field.Name,
            typeName,
            FacetMemberKind.Field,
            field.Type.IsValueType,
            false, 
            false, 
            field.IsReadOnly,
            memberXmlDocumentation,
            isNestedWrapper,
            nestedWrapperSourceTypeName,
            attributes,
            false, 
            null,  
            null,  
            GeneratorUtilities.GetTypeNameWithNullability(field.Type),
            null,  
            false, 
            true,  
            null,  
            false, 
            null,  
            null,  
            true,  
            attributeNamespaces));
        addedMembers.Add(field.Name);
    }

    #endregion
}
