namespace Facet.Tests.DiagnosticTests;

// Test models to verify FAC023 diagnostic for GenerateToSource

// Source class with readonly properties (no setters)
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

// This should trigger FAC023 warning because source has no setters
[Facet(typeof(SourceWithReadOnlyProperties), GenerateToSource = true)]
public partial class FacetWithNoSetters
{
}

// Source class with private constructor
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

// This should trigger FAC023 warning because source has private constructor
[Facet(typeof(SourceWithPrivateConstructor), GenerateToSource = true)]
public partial class FacetWithPrivateConstructor
{
}

// Source class with private setters
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

// This should trigger FAC023 warning because source has private setters
[Facet(typeof(SourceWithPrivateSetters), GenerateToSource = true)]
public partial class FacetWithPrivateSetters
{
}

// Source class with valid properties (should not trigger warning)
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

// This should NOT trigger FAC023 warning - source has public setters and parameterless constructor
[Facet(typeof(SourceWithPublicSetters), GenerateToSource = true)]
public partial class FacetWithPublicSetters
{
}

// Source record with positional constructor (should not trigger warning)
public record SourceRecord(int Id, string Name);

// This should NOT trigger FAC023 warning - positional records are supported
[Facet(typeof(SourceRecord), GenerateToSource = true)]
public partial class FacetFromRecord
{
}

// Source class with no constructor (implicit parameterless constructor)
public class SourceWithImplicitConstructor
{
    public int Id { get; set; }
    public string Name { get; set; }
}

// This should NOT trigger FAC023 warning - implicit parameterless constructor exists
[Facet(typeof(SourceWithImplicitConstructor), GenerateToSource = true)]
public partial class FacetWithImplicitConstructor
{
}
