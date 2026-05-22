using Facet;
using Facet.Mapping;
using System.Linq.Expressions;

namespace Facet.Tests.TestModels.NullableProjectionWarning;

// Source entity — both main and nested facets use the same source
public class InventoryEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}

// Nested facet DTO — also sourced from InventoryEntity (same as main DTO)
[Facet(typeof(InventoryEntity),
    Include = [nameof(InventoryEntity.Id), nameof(InventoryEntity.Code)])]
public partial class InventoryPackagingDto
{
}

// Main DTO with Configuration + nullable NestedFacet property
// This mirrors the user's pattern: Configuration maps a nullable nested facet
// via the nested DTO's ProjectionFrom... property
[Facet(typeof(InventoryEntity),
    Configuration = typeof(InventoryRetrievalMapConfig),
    Include = [nameof(InventoryEntity.Id)],
    NestedFacets = [typeof(InventoryPackagingDto)])]
public partial class InventoryRetrieval
{
    public InventoryPackagingDto? Packaging { get; set; }
    public bool IsArchived { get; set; }
}

public class InventoryRetrievalMapConfig : IFacetProjectionMapConfiguration<InventoryEntity, InventoryRetrieval>
{
    public static void ConfigureProjection(IFacetProjectionBuilder<InventoryEntity, InventoryRetrieval> builder)
    {
        // This line triggers a nullable warning:
        // Expression<Func<InventoryEntity, InventoryPackagingDto>> is passed where
        // Expression<Func<InventoryEntity, InventoryPackagingDto?>> is expected
        // because target.Packaging is InventoryPackagingDto? (nullable)
        builder.Map(target => target.Packaging, InventoryPackagingDto.ProjectionFromInventoryEntity);
        builder.Map(target => target.IsArchived, source => source.IsArchived);
    }
}
