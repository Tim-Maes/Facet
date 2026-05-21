namespace Facet.Tests.TestModels.ProjectionForNewKeyword;

public class TagEntity409
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
}

public class BaseEntity409
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public TagEntity409? Tag { get; set; }
}

public class MiddleEntity409 : BaseEntity409
{
    public int Weight { get; set; }
}

public class ChildEntity409 : MiddleEntity409
{
    public TagEntity409? SecondTag { get; set; }
}

[Facet(typeof(TagEntity409))]
public partial class TagDto409;

public class BaseDto409Config : IFacetProjectionMapConfiguration<BaseEntity409, BaseDto409>
{
    public static void ConfigureProjection(IFacetProjectionBuilder<BaseEntity409, BaseDto409> builder)
    {
        builder.Map(d => d.Name, s => s.Name + "!");
    }
}

[Facet(typeof(BaseEntity409),
    Configuration = typeof(BaseDto409Config),
    Include = [nameof(BaseEntity409.Tag)],
    NestedFacets = [typeof(TagDto409)])]
public partial class BaseDto409
{
    public string Name { get; set; } = "";
}

[Facet(typeof(MiddleEntity409),
    Include = [nameof(MiddleEntity409.Weight)])]
public partial class MiddleDto409 : BaseDto409;

public class ChildDto409Config : IFacetProjectionMapConfiguration<ChildEntity409, ChildDto409>
{
    public static void ConfigureProjection(IFacetProjectionBuilder<ChildEntity409, ChildDto409> builder)
    {
        builder.Map(d => d.SecondTagLabel, s => s.SecondTag != null ? s.SecondTag.Label : "");
    }
}

[Facet(typeof(ChildEntity409),
    Configuration = typeof(ChildDto409Config),
    Include = [nameof(ChildEntity409.SecondTag)],
    NestedFacets = [typeof(TagDto409)])]
public partial class ChildDto409 : MiddleDto409
{
    public string SecondTagLabel { get; set; } = "";
}

public class UnitEntity409B
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class OrderHeaderEntity409B
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    public UnitEntity409B? AssignedToUnit { get; set; }
}

public class OrderLineWeightEntity409B : OrderHeaderEntity409B
{
    public bool OrderedByWeight { get; set; }
    public int OrderedCount { get; set; }
}

public class PackingModelHeaderEntity409B
{
    public int Id { get; set; }
    public string ModelCode { get; set; } = "";
    public UnitEntity409B? OwnerUnit { get; set; }
}

public class OrderLinePackingEntity409B : OrderLineWeightEntity409B
{
    public PackingModelHeaderEntity409B? PackingModelHeader { get; set; }
}

[Facet(typeof(UnitEntity409B))]
public partial class UnitDto409B;

[Facet(typeof(OrderHeaderEntity409B),
    Include = [nameof(OrderHeaderEntity409B.AssignedToUnit)],
    NestedFacets = [typeof(UnitDto409B)])]
public partial class OrderLineBaseDto409B;

[Facet(typeof(OrderLineWeightEntity409B),
    Include = [
        nameof(OrderLineWeightEntity409B.OrderedByWeight),
        nameof(OrderLineWeightEntity409B.OrderedCount)
    ])]
public partial class OrderLineWeightDto409B : OrderLineBaseDto409B;

public class PackingModelHeaderDto409BConfig
    : IFacetProjectionMapConfiguration<PackingModelHeaderEntity409B, PackingModelHeaderDto409B>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<PackingModelHeaderEntity409B, PackingModelHeaderDto409B> builder)
    {
        builder.Map(d => d.ModelCode, s => "PKG-" + s.ModelCode);
    }
}

[Facet(typeof(PackingModelHeaderEntity409B),
    Configuration = typeof(PackingModelHeaderDto409BConfig),
    Include = [nameof(PackingModelHeaderEntity409B.OwnerUnit)],
    NestedFacets = [typeof(UnitDto409B)])]
public partial class PackingModelHeaderDto409B
{
    public string ModelCode { get; set; } = "";
}

[Facet(typeof(OrderLinePackingEntity409B),
    Include = [nameof(OrderLinePackingEntity409B.PackingModelHeader)],
    NestedFacets = [typeof(PackingModelHeaderDto409B)])]
public partial class OrderLinePackingDto409B : OrderLineWeightDto409B;

