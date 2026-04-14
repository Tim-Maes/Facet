namespace Facet.Tests.TestModels.NonFacetBaseNewKeyword;

public interface IModifiedByBase
{
    long ModifiedByUnitId { get; set; }
    long ModifiedByUserId { get; set; }
}

public interface IOrderLineBase
{
    string Number { get; set; }
    int Status { get; set; }
}

public class UpdateStatusDto
{
    public int Id { get; set; }
    public string UpdatedBy { get; set; } = "";
}

public class ModifiedByBaseDto312 : UpdateStatusDto, IModifiedByBase
{
    public long ModifiedByUnitId { get; set; }
    public long ModifiedByUserId { get; set; }
}

public class OrderLineBaseEntity
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int Status { get; set; }
    public string UpdatedBy { get; set; } = "";
    public long ModifiedByUnitId { get; set; }
    public long ModifiedByUserId { get; set; }
}

[Facet(typeof(OrderLineBaseEntity),
    Include = [
        nameof(OrderLineBaseEntity.Number),
        nameof(OrderLineBaseEntity.Status)
    ],
    GenerateToSource = true)]
public partial class OrderLineBaseDto312 : ModifiedByBaseDto312, IOrderLineBase;
