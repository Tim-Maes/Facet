namespace Facet.Tests.DiagnosticTests;

public class SourceWithReadOnlyProperties
{
    public int Id { get; }
    public string Name { get; }
    
    public SourceWithReadOnlyProperties(int id, string name)
    {
        Id = id;
        Name = name;
    }
}

[Facet(typeof(SourceWithReadOnlyProperties), GenerateToSource = true)]
public partial class FacetWithNoSetters
{
}

public class SourceWithPrivateConstructor
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    private SourceWithPrivateConstructor()
    {
        Id = 0;
        Name = string.Empty;
    }
    
    public static SourceWithPrivateConstructor Create() => new SourceWithPrivateConstructor();
}

[Facet(typeof(SourceWithPrivateConstructor), GenerateToSource = true)]
public partial class FacetWithPrivateConstructor
{
}

public class SourceWithPrivateSetters
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    
    public SourceWithPrivateSetters()
    {
        Id = 0;
        Name = string.Empty;
    }
}

[Facet(typeof(SourceWithPrivateSetters), GenerateToSource = true)]
public partial class FacetWithPrivateSetters
{
}

public class SourceWithPublicSetters
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    public SourceWithPublicSetters()
    {
        Id = 0;
        Name = string.Empty;
    }
}

[Facet(typeof(SourceWithPublicSetters), GenerateToSource = true)]
public partial class FacetWithPublicSetters
{
}

public record SourceRecord(int Id, string Name);

[Facet(typeof(SourceRecord), GenerateToSource = true)]
public partial class FacetFromRecord
{
}

public class SourceWithImplicitConstructor
{
    public int Id { get; set; }
    public string Name { get; set; }
}

[Facet(typeof(SourceWithImplicitConstructor), GenerateToSource = true)]
public partial class FacetWithImplicitConstructor
{
}
