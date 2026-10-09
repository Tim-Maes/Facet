using Facet;

namespace Facet.Tests.ExternalLib;

// This project doubles as a contract assembly: Facet_GenerateMappings is false, so the facets
// below carry property declarations and nothing that names the type they were shaped from.
// ContractAssemblyReferenceTests in Facet.Tests asserts that against the emitted metadata.

public class ExternalAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class ExternalOrder
{
    public Guid Reference { get; set; }
    public decimal Total { get; set; }
}

public class ExternalCustomer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ExternalAddress? Address { get; set; }
    public List<ExternalOrder> Orders { get; set; } = new();
}

[Facet(typeof(ExternalAddress))]
public partial class ExternalAddressContract;

[Facet(typeof(ExternalOrder))]
public partial class ExternalOrderContract;

/// <summary>
/// NestedFacets rewrites Address and Orders to the contract types. Without it they would be
/// generated as the source-layer types and this assembly would reference that layer through the
/// property types alone, which is what FAC116 reports.
/// </summary>
[Facet(typeof(ExternalCustomer),
    NestedFacets = new[] { typeof(ExternalAddressContract), typeof(ExternalOrderContract) })]
public partial class ExternalCustomerContract;

// Contracts over the two entities this project already had, so the mapper tests can cover an
// exclusion and a reverse mapping without inventing more source types.

[Facet(typeof(ExternalSource))]
public partial class ExternalSourceContract;

[Facet(typeof(ExternalDomainEntity))]
public partial class ExternalEntityContract;
