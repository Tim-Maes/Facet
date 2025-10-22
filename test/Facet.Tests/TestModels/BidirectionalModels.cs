namespace Facet.Tests.TestModels;

// Test entities with bidirectional relationship
public class Worker
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public Firm? Company { get; set; }
}

public class Firm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Worker> Workers { get; set; } = new List<Worker>();
}

// CORRECT approach for bidirectional relationships:
// 1. Define FacetWithoutCompany without the nested Company reference (breaks the cycle)
// 2. Define FirmFacet with nested WorkerFacetWithoutCompany
// 3. Define WorkerFacet with nested FirmFacet (includes Company but not the Workers collection)

// Worker facet WITHOUT the Company navigation property (breaks the cycle)
[Facet(typeof(Worker), "Company")]
public partial record WorkerFacetWithoutCompany;

// Firm facet with collection of workers (but workers don't have Company to avoid cycle)
[Facet(typeof(Firm), NestedFacets = [typeof(WorkerFacetWithoutCompany)])]
public partial record FirmFacet;

// ALTERNATIVE: Worker facet WITH Company (but Company doesn't have Workers to avoid cycle)
// Note: You need to define a FirmFacetWithoutWorkers for this to work
[Facet(typeof(Firm), "Workers")]
public partial record FirmFacetWithoutWorkers;

[Facet(typeof(Worker), NestedFacets = [typeof(FirmFacetWithoutWorkers)])]
public partial record WorkerFacet;
