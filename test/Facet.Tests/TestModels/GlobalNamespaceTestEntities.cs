using Facet;

[GenerateDtos(Types = DtoTypes.All, OutputType = OutputType.Record)]
public class TestGlobalNamespaceEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

[GenerateDtos(Types = DtoTypes.All, OutputType = OutputType.Class, ExcludeAuditFields = true)]
public class TestGlobalAuditableEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
