using Facet.Tests.ExternalLib;

namespace Facet.Tests.UnitTests.Core.Facet;

public class CrossAssemblyCopyDocsTests
{
    [Fact]
    public void Facet_ShouldCopyXmlDocs_FromExternalAssembly()
    {
        var source = LoadGeneratedSource(typeof(FacetOfExternal).FullName!);
        source.Should().NotBeEmpty("generated file should exist");
        source.Should().Contain("This is an external property.");
        source.Should().Contain("The external identifier.");
    }

    [Fact]
    public void Facet_ShouldCopyXmlDocs_FromBothInternalAndExternal()
    {
        var source = LoadGeneratedSource(typeof(FacetOfBoth).FullName!);
        source.Should().NotBeEmpty("generated file should exist");
        source.Should().Contain("This is an external property.");
        source.Should().Contain("This is an internal property.");
    }

    [Fact]
    public void Facet_ShouldCopyXmlDocs_FromInternalSource()
    {
        var source = LoadGeneratedSource(typeof(FacetOfInternal).FullName!);
        source.Should().NotBeEmpty("generated file should exist");
        source.Should().Contain("This is an internal property.");
    }

    private static string LoadGeneratedSource(string typeFullName)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var dir = Path.Combine(projectRoot, "obj", "Generated", "Facet", "Facet.Generators.FacetGenerator");
        var propertiesPath = Path.Combine(dir, $"{typeFullName}.Properties.g.cs");
        var combinedPath = Path.Combine(dir, $"{typeFullName}.g.cs");
        try { return File.ReadAllText(File.Exists(propertiesPath) ? propertiesPath : combinedPath); }
        catch (FileNotFoundException) { return string.Empty; }
    }
}

public class InternalSource
{
    /// <summary>
    /// This is an internal property.
    /// </summary>
    public string InternalProperty { get; set; } = string.Empty;
}

[Facet(typeof(ExternalSource), CopyDocs = true)]
public partial class FacetOfExternal
{
}

[Facet(typeof(InternalSource), CopyDocs = true)]
public partial class FacetOfInternal
{
}

[Facet(typeof(ExternalSource), CopyDocs = true)]
[Facet(typeof(InternalSource), CopyDocs = true)]
public partial class FacetOfBoth
{
}
