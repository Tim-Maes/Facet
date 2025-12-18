namespace Facet.Dashboard;

/// <summary>
/// Represents information about a property or field on a type.
/// </summary>
public sealed class FacetMemberInfo
{
    /// <summary>
    /// Gets the name of the member.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type name of the member.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets whether this is a property (true) or field (false).
    /// </summary>
    public bool IsProperty { get; }

    /// <summary>
    /// Gets whether the member is nullable.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets whether the member is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets whether the member has an init-only setter.
    /// </summary>
    public bool IsInitOnly { get; }

    /// <summary>
    /// Gets whether the member is read-only.
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// Gets the XML documentation summary if available.
    /// </summary>
    public string? XmlDocumentation { get; }

    /// <summary>
    /// Gets the attributes applied to this member.
    /// </summary>
    public IReadOnlyList<string> Attributes { get; }

    /// <summary>
    /// Gets whether this member maps to a nested facet.
    /// </summary>
    public bool IsNestedFacet { get; }

    /// <summary>
    /// Gets whether this member is a collection.
    /// </summary>
    public bool IsCollection { get; }

    /// <summary>
    /// Gets the source property name if mapped from a different name.
    /// </summary>
    public string? MappedFromProperty { get; }

    /// <summary>
    /// Creates a new instance of <see cref="FacetMemberInfo"/>.
    /// </summary>
    public FacetMemberInfo(
        string name,
        string typeName,
        bool isProperty,
        bool isNullable,
        bool isRequired,
        bool isInitOnly,
        bool isReadOnly,
        string? xmlDocumentation,
        IEnumerable<string>? attributes,
        bool isNestedFacet,
        bool isCollection,
        string? mappedFromProperty)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        IsProperty = isProperty;
        IsNullable = isNullable;
        IsRequired = isRequired;
        IsInitOnly = isInitOnly;
        IsReadOnly = isReadOnly;
        XmlDocumentation = xmlDocumentation;
        Attributes = attributes?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
        IsNestedFacet = isNestedFacet;
        IsCollection = isCollection;
        MappedFromProperty = mappedFromProperty;
    }
}
