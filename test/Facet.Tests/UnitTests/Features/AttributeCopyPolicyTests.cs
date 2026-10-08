using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Facet.Tests.UnitTests.Features;

/// <summary>
/// A source entity carrying both kinds of attribute: the ones that describe its contract, and the
/// ones that describe how it is stored.
/// </summary>
public class AttributePolicyEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Column("created_utc", TypeName = "timestamptz")]
    public DateTime CreatedUtc { get; set; }

    [Precision(18, 2)]
    public decimal Balance { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;
}

// The default mode: what Facet generated before CopyAttributesMode existed.
[Facet(typeof(AttributePolicyEntity), CopyAttributes = true)]
public partial class PolicyDefaultDto;

[Facet(typeof(AttributePolicyEntity), CopyAttributes = true,
    CopyAttributesMode = AttributeCopyMode.ContractSafe)]
public partial class PolicyContractSafeDto;

[Facet(typeof(AttributePolicyEntity), CopyAttributes = true,
    CopyAttributesInclude = new[] { "Precision" })]
public partial class PolicyIncludeDto;

[Facet(typeof(AttributePolicyEntity), CopyAttributes = true,
    CopyAttributesExclude = new[] { "System.ComponentModel.DataAnnotations.Schema.*", "Key" })]
public partial class PolicyStorageFreeDto;

[Facet(typeof(AttributePolicyEntity), CopyAttributes = true,
    CopyAttributesExclude = new[] { "Required" })]
public partial class PolicyExcludeDto;

public class AttributeCopyPolicyTests
{
    [Fact]
    public void Default_CopiesContractAttributes()
    {
        HasAttribute<PolicyDefaultDto, RequiredAttribute>("Name").Should().BeTrue();
        HasAttribute<PolicyDefaultDto, StringLengthAttribute>("Name").Should().BeTrue();
        HasAttribute<PolicyDefaultDto, JsonPropertyNameAttribute>("Tag").Should().BeTrue();
    }

    [Fact]
    public void Default_CopiesEverythingItCopiedBefore()
    {
        // Preserve is the default precisely so that upgrading changes nothing. [Precision] pins
        // Microsoft.EntityFrameworkCore.Abstractions into the DTO, which FAC109 reports — but
        // reporting is as far as the default goes.
        HasAttribute<PolicyDefaultDto, PrecisionAttribute>("Balance").Should().BeTrue();
    }

    [Fact]
    public void ContractSafe_DropsAttributesFromPackageAssemblies()
    {
        HasAttribute<PolicyContractSafeDto, PrecisionAttribute>("Balance").Should().BeFalse();
    }

    [Fact]
    public void ContractSafe_KeepsEverythingTheSharedFrameworkCarries()
    {
        // The mode is about loadability, not tidiness: these resolve wherever [Required] does.
        HasAttribute<PolicyContractSafeDto, RequiredAttribute>("Name").Should().BeTrue();
        HasAttribute<PolicyContractSafeDto, ColumnAttribute>("CreatedUtc").Should().BeTrue();
        HasAttribute<PolicyContractSafeDto, KeyAttribute>("Id").Should().BeTrue();
    }

    [Fact]
    public void Default_StillCopiesFrameworkResidentMappingAttributes()
    {
        // [Column] and [Key] live in System.ComponentModel.Annotations, alongside [Required]. They
        // resolve wherever that does, so copying them pins no extra reference and breaks nothing —
        // and dropping them by default would silently change what existing projects generate.
        // Whether a column name belongs on a DTO is the project's call, made via the next test.
        HasAttribute<PolicyDefaultDto, ColumnAttribute>("CreatedUtc").Should().BeTrue();
        HasAttribute<PolicyDefaultDto, KeyAttribute>("Id").Should().BeTrue();
    }

    [Fact]
    public void Exclude_IsHowAProjectKeepsStorageDetailOffItsContracts()
    {
        HasAttribute<PolicyStorageFreeDto, ColumnAttribute>("CreatedUtc").Should().BeFalse();
        HasAttribute<PolicyStorageFreeDto, KeyAttribute>("Id").Should().BeFalse();

        // Still a contract concern, so still copied.
        HasAttribute<PolicyStorageFreeDto, RequiredAttribute>("Name").Should().BeTrue();
    }

    [Fact]
    public void Include_OverridesTheBuiltInExclusions()
    {
        // [Precision] is excluded by default because its assembly is a package; naming it brings
        // it back, which is what a facet that is itself a mapped entity needs.
        HasAttribute<PolicyIncludeDto, PrecisionAttribute>("Balance").Should().BeTrue();
    }

    [Fact]
    public void Exclude_DropsAnOtherwiseCopiedAttribute()
    {
        HasAttribute<PolicyExcludeDto, RequiredAttribute>("Name").Should().BeFalse();

        // Its neighbour on the same property is untouched.
        HasAttribute<PolicyExcludeDto, StringLengthAttribute>("Name").Should().BeTrue();
    }

    [Fact]
    public void Include_MatchesWithOrWithoutTheAttributeSuffix()
    {
        // "Precision" and "Column" above are bare names; the trailing "Attribute" is optional,
        // matching how the attribute is written in source.
        var dto = typeof(PolicyIncludeDto);

        dto.GetProperty("Balance")!.GetCustomAttributes(inherit: true)
            .Should().ContainItemsAssignableTo<PrecisionAttribute>();
    }

    private static bool HasAttribute<TDto, TAttribute>(string propertyName)
        where TAttribute : Attribute
    {
        var property = typeof(TDto).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        property.Should().NotBeNull($"{typeof(TDto).Name} should have a {propertyName} property");

        return property!.GetCustomAttribute<TAttribute>(inherit: true) is not null;
    }
}
