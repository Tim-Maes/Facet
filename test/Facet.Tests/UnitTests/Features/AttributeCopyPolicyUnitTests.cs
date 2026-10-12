using Facet.Generators.Shared;

namespace Facet.Tests.UnitTests.Features;

/// <summary>
/// Unit tests for the attribute-copy policy's pattern matching and list parsing.
/// </summary>
/// <remarks>
/// The list parsing has its own tests because the separator was a real defect: a
/// <c>CompilerVisibleProperty</c> value reaches the generator through a generated
/// <c>.editorconfig</c>, where <c>;</c> begins a comment — so a semicolon-separated value arrived
/// truncated at its first entry, with no error. Facet.targets normalises to a comma, and the parser
/// accepts both.
/// </remarks>
public class AttributeCopyPolicyUnitTests
{
    [Theory]
    [InlineData("A;B;C")]
    [InlineData("A,B,C")]
    [InlineData("A; B , C")]
    public void ParsePatternList_AcceptsEitherSeparator(string value)
    {
        AttributeCopyPolicy.ParsePatternList(value).Should().Equal("A", "B", "C");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParsePatternList_TreatsAnEmptyValueAsNoPatterns(string? value)
    {
        AttributeCopyPolicy.ParsePatternList(value).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.PrecisionAttribute")]
    [InlineData("MongoDB.Bson.Serialization.Attributes.BsonIdAttribute")]
    [InlineData("CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute")]
    public void Default_ExcludesOnlyWhatItAlwaysExcluded(string attribute)
    {
        // Preserve keeps every attribute it kept before the mode existed; only the compiler
        // plumbing and the other generator's trigger attributes are unconditional.
        var alwaysExcluded = attribute.StartsWith("CommunityToolkit.Mvvm.")
            || attribute.StartsWith("System.Runtime.CompilerServices.");

        AttributeCopyPolicy.Default.ShouldCopy(attribute).Should().Be(!alwaysExcluded);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.PrecisionAttribute")]
    [InlineData("MongoDB.Bson.Serialization.Attributes.BsonIdAttribute")]
    [InlineData("LinqToDB.Mapping.ColumnAttribute")]
    public void ContractSafe_ExcludesPackageAttributes(string attribute)
    {
        ContractSafe().ShouldCopy(attribute).Should().BeFalse();
    }

    [Theory]
    [InlineData("System.ComponentModel.DataAnnotations.RequiredAttribute")]
    [InlineData("System.ComponentModel.DataAnnotations.Schema.ColumnAttribute")]
    [InlineData("System.ComponentModel.DataAnnotations.KeyAttribute")]
    public void ContractSafe_KeepsFrameworkResidentAttributes(string attribute)
    {
        ContractSafe().ShouldCopy(attribute).Should().BeTrue();
    }

    [Fact]
    public void CommunityToolkit_StaysExcludedWhateverTheConfigurationSays()
    {
        // Issue #277: copying [ObservableProperty] onto a facet produces analyzer errors, so it is
        // not something a project can opt back in to by accident — or at all.
        var policy = AttributeCopyPolicy.Create(
            AttributeCopyMode.Preserve,
            globalExcludes: null,
            globalIncludes: new[] { "CommunityToolkit.Mvvm.*", "ObservableProperty" });

        policy.ShouldCopy("CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute")
            .Should().BeFalse();
    }

    private static AttributeCopyPolicy ContractSafe() =>
        AttributeCopyPolicy.Create(AttributeCopyMode.ContractSafe, null, null);

    [Theory]
    [InlineData("System.ComponentModel.DataAnnotations.RequiredAttribute")]
    [InlineData("System.ComponentModel.DataAnnotations.StringLengthAttribute")]
    [InlineData("System.Text.Json.Serialization.JsonPropertyNameAttribute")]
    [InlineData("System.ComponentModel.DisplayNameAttribute")]
    [InlineData("Acme.Validation.NotRudeAttribute")]
    // Framework-resident mapping attributes: copying them pins no extra reference, so the default
    // leaves them alone rather than changing what existing projects generate.
    [InlineData("System.ComponentModel.DataAnnotations.Schema.ColumnAttribute")]
    [InlineData("System.ComponentModel.DataAnnotations.KeyAttribute")]
    [InlineData("System.ComponentModel.DataAnnotations.TimestampAttribute")]
    public void Default_CopiesContractAttributes(string attribute)
    {
        AttributeCopyPolicy.Default.ShouldCopy(attribute).Should().BeTrue();
    }

    [Fact]
    public void PatternMatching_IgnoresTheGlobalPrefix()
    {
        ContractSafe()
            .ShouldCopy("global::Microsoft.EntityFrameworkCore.PrecisionAttribute")
            .Should().BeFalse();
    }

    [Theory]
    // a namespace wildcard, a fully qualified name, and a bare name with and without the suffix
    [InlineData("Acme.Infra.*")]
    [InlineData("Acme.Infra.AuditedAttribute")]
    [InlineData("Audited")]
    [InlineData("AuditedAttribute")]
    public void Exclude_MatchesEveryPatternForm(string pattern)
    {
        var policy = AttributeCopyPolicy.Create(AttributeCopyMode.Preserve, new[] { pattern }, null);

        policy.ShouldCopy("Acme.Infra.AuditedAttribute").Should().BeFalse();
    }

    [Fact]
    public void Exclude_DoesNotMatchANeighbouringAttribute()
    {
        var policy = AttributeCopyPolicy.Create(AttributeCopyMode.Preserve, new[] { "Audited" }, null);

        policy.ShouldCopy("Acme.Infra.ApprovedAttribute").Should().BeTrue();
    }

    [Fact]
    public void Include_OverridesABuiltInExclusion()
    {
        var policy = AttributeCopyPolicy.Create(AttributeCopyMode.ContractSafe, null, new[] { "Precision" });

        policy.ShouldCopy("Microsoft.EntityFrameworkCore.PrecisionAttribute").Should().BeTrue();
    }

    [Fact]
    public void Include_OverridesAConfiguredExclusion()
    {
        var policy = AttributeCopyPolicy.Create(
            AttributeCopyMode.Preserve,
            new[] { "Acme.Infra.*" },
            new[] { "Acme.Infra.AuditedAttribute" });

        policy.ShouldCopy("Acme.Infra.AuditedAttribute").Should().BeTrue();
        policy.ShouldCopy("Acme.Infra.OtherAttribute").Should().BeFalse();
    }

    [Fact]
    public void PerTypePatterns_AddToTheProjectWideOnes()
    {
        var policy = AttributeCopyPolicy.Create(
            AttributeCopyMode.Preserve,
            globalExcludes: new[] { "Acme.Infra.*" },
            globalIncludes: null,
            typeExcludes: new[] { "DisplayName" },
            typeIncludes: null);

        policy.ShouldCopy("Acme.Infra.AuditedAttribute").Should().BeFalse("the project-wide rule still applies");
        policy.ShouldCopy("System.ComponentModel.DisplayNameAttribute").Should().BeFalse("the per-type rule applies too");
    }
}
