using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Facet.Generators.FlattenGenerators;

/// <summary>
/// Represents a flattened target type model.
/// </summary>
internal sealed class FlattenTargetModel
{
    public FlattenTargetModel(
        string name,
        string? ns,
        string fullName,
        TypeKind typeKind,
        bool isRecord,
        bool generateParameterlessConstructor,
        bool generateProjection,
        string sourceTypeName,
        ImmutableArray<FlattenProperty> properties,
        string? typeXmlDocumentation,
        ImmutableArray<string> containingTypes,
        bool useFullName,
        FlattenNamingStrategy namingStrategy,
        int maxDepth)
    {
        Name = name;
        Namespace = ns;
        FullName = fullName;
        TypeKind = typeKind;
        IsRecord = isRecord;
        GenerateParameterlessConstructor = generateParameterlessConstructor;
        GenerateProjection = generateProjection;
        SourceTypeName = sourceTypeName;
        Properties = properties;
        TypeXmlDocumentation = typeXmlDocumentation;
        ContainingTypes = containingTypes;
        UseFullName = useFullName;
        NamingStrategy = namingStrategy;
        MaxDepth = maxDepth;
    }

    public string Name { get; }
    public string? Namespace { get; }
    public string FullName { get; }
    public TypeKind TypeKind { get; }
    public bool IsRecord { get; }
    public bool GenerateParameterlessConstructor { get; }
    public bool GenerateProjection { get; }
    public string SourceTypeName { get; }
    public ImmutableArray<FlattenProperty> Properties { get; }
    public string? TypeXmlDocumentation { get; }
    public ImmutableArray<string> ContainingTypes { get; }
    public bool UseFullName { get; }
    public FlattenNamingStrategy NamingStrategy { get; }
    public int MaxDepth { get; }
}

/// <summary>
/// Represents a property in a flattened target type.
/// </summary>
internal sealed class FlattenProperty
{
    public FlattenProperty(
        string name,
        string typeName,
        string sourcePath,
        ImmutableArray<string> pathSegments,
        bool isValueType,
        string? xmlDocumentation)
    {
        Name = name;
        TypeName = typeName;
        SourcePath = sourcePath;
        PathSegments = pathSegments;
        IsValueType = isValueType;
        XmlDocumentation = xmlDocumentation;
    }

    /// <summary>
    /// The name of the flattened property (e.g., "AddressStreet").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The fully qualified type name of the property.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// The source path to this property (e.g., "Address.Street").
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// The path segments as an array (e.g., ["Address", "Street"]).
    /// </summary>
    public ImmutableArray<string> PathSegments { get; }

    /// <summary>
    /// Whether this property is a value type.
    /// </summary>
    public bool IsValueType { get; }

    /// <summary>
    /// XML documentation for this property, if available.
    /// </summary>
    public string? XmlDocumentation { get; }
}
