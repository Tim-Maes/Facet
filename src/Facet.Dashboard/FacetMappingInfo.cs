namespace Facet.Dashboard;

/// <summary>
/// Represents information about a source type and all its associated facets.
/// </summary>
public sealed class FacetMappingInfo
{
    /// <summary>
    /// Gets the source type from which facets are generated.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// Gets the fully qualified name of the source type.
    /// </summary>
    public string SourceTypeName => SourceType.FullName ?? SourceType.Name;

    /// <summary>
    /// Gets the simple name of the source type.
    /// </summary>
    public string SourceTypeSimpleName => SourceType.Name;

    /// <summary>
    /// Gets the namespace of the source type.
    /// </summary>
    public string? SourceTypeNamespace => SourceType.Namespace;

    /// <summary>
    /// Gets the collection of facets generated from this source type.
    /// </summary>
    public IReadOnlyList<FacetTypeInfo> Facets { get; }

    /// <summary>
    /// Gets the collection of properties on the source type.
    /// </summary>
    public IReadOnlyList<FacetMemberInfo> SourceMembers { get; }

    /// <summary>
    /// Creates a new instance of <see cref="FacetMappingInfo"/>.
    /// </summary>
    public FacetMappingInfo(Type sourceType, IEnumerable<FacetTypeInfo> facets, IEnumerable<FacetMemberInfo> sourceMembers)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        Facets = facets?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(facets));
        SourceMembers = sourceMembers?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(sourceMembers));
    }
}

/// <summary>
/// Represents information about a single facet type.
/// </summary>
public sealed class FacetTypeInfo
{
    /// <summary>
    /// Gets the facet type.
    /// </summary>
    public Type FacetType { get; }

    /// <summary>
    /// Gets the fully qualified name of the facet type.
    /// </summary>
    public string FacetTypeName => FacetType.FullName ?? FacetType.Name;

    /// <summary>
    /// Gets the simple name of the facet type.
    /// </summary>
    public string FacetTypeSimpleName => FacetType.Name;

    /// <summary>
    /// Gets the namespace of the facet type.
    /// </summary>
    public string? FacetTypeNamespace => FacetType.Namespace;

    /// <summary>
    /// Gets whether this facet generates a constructor from the source type.
    /// </summary>
    public bool HasConstructor { get; }

    /// <summary>
    /// Gets whether this facet has a Projection expression.
    /// </summary>
    public bool HasProjection { get; }

    /// <summary>
    /// Gets whether this facet can map back to the source type.
    /// </summary>
    public bool HasToSource { get; }

    /// <summary>
    /// Gets the list of excluded property names.
    /// </summary>
    public IReadOnlyList<string> ExcludedProperties { get; }

    /// <summary>
    /// Gets the list of included property names (null if using exclude mode).
    /// </summary>
    public IReadOnlyList<string>? IncludedProperties { get; }

    /// <summary>
    /// Gets the members of this facet type.
    /// </summary>
    public IReadOnlyList<FacetMemberInfo> Members { get; }

    /// <summary>
    /// Gets the nested facet types used by this facet.
    /// </summary>
    public IReadOnlyList<Type> NestedFacets { get; }

    /// <summary>
    /// Gets the type kind (class, record, struct, record struct).
    /// </summary>
    public string TypeKind { get; }

    /// <summary>
    /// Gets whether all properties are made nullable.
    /// </summary>
    public bool NullableProperties { get; }

    /// <summary>
    /// Gets whether attributes are copied from source.
    /// </summary>
    public bool CopyAttributes { get; }

    /// <summary>
    /// Gets the configuration type name if specified.
    /// </summary>
    public string? ConfigurationTypeName { get; }

    /// <summary>
    /// Creates a new instance of <see cref="FacetTypeInfo"/>.
    /// </summary>
    public FacetTypeInfo(
        Type facetType,
        bool hasConstructor,
        bool hasProjection,
        bool hasToSource,
        IEnumerable<string> excludedProperties,
        IEnumerable<string>? includedProperties,
        IEnumerable<FacetMemberInfo> members,
        IEnumerable<Type> nestedFacets,
        string typeKind,
        bool nullableProperties,
        bool copyAttributes,
        string? configurationTypeName)
    {
        FacetType = facetType ?? throw new ArgumentNullException(nameof(facetType));
        HasConstructor = hasConstructor;
        HasProjection = hasProjection;
        HasToSource = hasToSource;
        ExcludedProperties = excludedProperties?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
        IncludedProperties = includedProperties?.ToList().AsReadOnly();
        Members = members?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(members));
        NestedFacets = nestedFacets?.ToList().AsReadOnly() ?? new List<Type>().AsReadOnly();
        TypeKind = typeKind;
        NullableProperties = nullableProperties;
        CopyAttributes = copyAttributes;
        ConfigurationTypeName = configurationTypeName;
    }
}
