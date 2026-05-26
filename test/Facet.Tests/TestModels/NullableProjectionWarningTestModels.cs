using Facet;
using Facet.Mapping;
using System.Linq.Expressions;

namespace Facet.Tests.TestModels.NullableProjectionWarning;

public class InventoryEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}

[Facet(typeof(InventoryEntity),
    Include = [nameof(InventoryEntity.Id), nameof(InventoryEntity.Code)])]
public partial class InventoryPackagingDto
{
}

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
        builder.Map(target => target.Packaging, InventoryPackagingDto.ProjectionFromInventoryEntity);
        builder.Map(target => target.IsArchived, source => source.IsArchived);
    }
}
