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
