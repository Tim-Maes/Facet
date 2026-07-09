namespace Facet.Tests.ExternalLib;

/// <summary>
/// An external source type with documented properties.
/// </summary>
public class ExternalSource
{
    /// <summary>
    /// This is an external property.
    /// </summary>
    public string ExternalProperty { get; set; } = string.Empty;

    /// <summary>
    /// The external identifier.
    /// </summary>
    public int ExternalId { get; set; }

    /// <summary>
    /// A description from the external assembly.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Entity deliberately WITHOUT any [GenerateDtos] attribute: DTOs for it are generated
/// cross-assembly by [assembly: GenerateDtosFor(typeof(ExternalDomainEntity), ...)] in
/// Facet.Tests — proving contract generation can live downstream of the domain project.
/// </summary>
public class ExternalDomainEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
