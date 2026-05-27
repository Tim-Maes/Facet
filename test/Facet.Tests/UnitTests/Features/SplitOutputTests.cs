using System.IO;
using System.Reflection;

namespace Facet.Tests.UnitTests.Features;

public class SplitOutputEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public interface ISplitOutputMarker { }

public abstract class SplitOutputBase
{
    public string BaseProp { get; set; } = string.Empty;
}

[Facet(typeof(SplitOutputEntity))]
public partial class SplitOutputDto;

[Facet(typeof(SplitOutputEntity))]
public partial class SplitOutputWithInheritanceDto : SplitOutputBase, ISplitOutputMarker;

public class SplitOutputTests
{
    [Fact]
    public void SplitOutput_Constructor_MapsAllProperties()
    {
        var entity = new SplitOutputEntity { Id = 42, Name = "Test", Description = "Desc" };

        var dto = new SplitOutputDto(entity);

        dto.Id.Should().Be(42);
        dto.Name.Should().Be("Test");
        dto.Description.Should().Be("Desc");
    }

    [Fact]
    public void SplitOutput_PropertiesFile_Exists()
    {
        var generatedDir = GetGeneratedFilesDir();
        var propsFile = Path.Combine(generatedDir, "Facet.Tests.UnitTests.Features.SplitOutputDto.Properties.g.cs");

        File.Exists(propsFile).Should().BeTrue($"Expected Properties.g.cs at {propsFile}");
    }

    [Fact]
    public void SplitOutput_MappingsFile_Exists()
    {
        var generatedDir = GetGeneratedFilesDir();
        var mapsFile = Path.Combine(generatedDir, "Facet.Tests.UnitTests.Features.SplitOutputDto.Mappings.g.cs");

        File.Exists(mapsFile).Should().BeTrue($"Expected Mappings.g.cs at {mapsFile}");
    }

    [Fact]
    public void SplitOutput_PropertiesFile_ContainsOnlyProperties()
    {
        var generatedDir = GetGeneratedFilesDir();
        var propsFile = Path.Combine(generatedDir, "Facet.Tests.UnitTests.Features.SplitOutputDto.Properties.g.cs");

        if (!File.Exists(propsFile))
            return;

        var content = File.ReadAllText(propsFile);

        content.Should().Contain("public int Id");
        content.Should().Contain("public string Name");
        content.Should().NotContain("public SplitOutputDto(");
        content.Should().NotContain("public static");
    }

    [Fact]
    public void SplitOutput_MappingsFile_ContainsConstructor()
    {
        var generatedDir = GetGeneratedFilesDir();
        var mapsFile = Path.Combine(generatedDir, "Facet.Tests.UnitTests.Features.SplitOutputDto.Mappings.g.cs");

        if (!File.Exists(mapsFile))
            return;

        var content = File.ReadAllText(mapsFile);

        content.Should().Contain("public SplitOutputDto(");
        content.Should().NotContain("public int Id { get;");
    }

    [Fact]
    public void SplitOutput_CombinedFile_DoesNotExist()
    {
        var generatedDir = GetGeneratedFilesDir();
        var combinedFile = Path.Combine(generatedDir, "Facet.Tests.UnitTests.Features.SplitOutputDto.g.cs");

        File.Exists(combinedFile).Should().BeFalse(
            "SplitOutput = true should not emit a combined .g.cs file");
    }

    [Fact]
    public void SplitOutput_PropertiesFile_NoMappingUsings()
    {
        var generatedDir = GetGeneratedFilesDir();
        var propsFile = Path.Combine(generatedDir, "Facet.Tests.UnitTests.Features.SplitOutputDto.Properties.g.cs");

        if (!File.Exists(propsFile))
            return;

        var content = File.ReadAllText(propsFile);

        content.Should().NotContain("using System.Linq;");
        content.Should().NotContain("using System.Linq.Expressions;");
    }

    [Fact]
    public void SplitOutput_PropertiesFile_ContainsInheritance()
    {
        var generatedDir = GetGeneratedFilesDir();
        var propsFile = Path.Combine(generatedDir, "Facet.Tests.UnitTests.Features.SplitOutputWithInheritanceDto.Properties.g.cs");

        if (!File.Exists(propsFile))
            return;

        var content = File.ReadAllText(propsFile);

        content.Should().Contain("SplitOutputBase");
        content.Should().Contain("ISplitOutputMarker");
    }

    [Fact]
    public void SplitOutput_MappingsFile_NoInheritanceFromProperties()
    {
        var generatedDir = GetGeneratedFilesDir();
        var mapsFile = Path.Combine(generatedDir, "Facet.Tests.UnitTests.Features.SplitOutputWithInheritanceDto.Mappings.g.cs");

        if (!File.Exists(mapsFile))
            return;

        var content = File.ReadAllText(mapsFile);

        // Base class and interface are in Properties file only
        content.Should().NotContain("SplitOutputBase");
        content.Should().NotContain("ISplitOutputMarker");
    }

    private static string GetGeneratedFilesDir()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        // Assembly lives in bin/{Config}/net10.0 — 3 levels up is the project root (Facet.Tests/).
        var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyDir)!))!;

        return Path.Combine(projectRoot, "obj", "Generated", "Facet", "Facet.Generators.FacetGenerator");
    }
}
