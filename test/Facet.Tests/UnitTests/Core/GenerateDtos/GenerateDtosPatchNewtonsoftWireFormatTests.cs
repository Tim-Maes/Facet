using Facet.Tests.TestModels;
using Newtonsoft.Json;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// The Json.NET counterpart of <see cref="GenerateDtosPatchWireFormatTests"/>: apps using
/// ASP.NET Core's AddNewtonsoftJson bind request bodies through Json.NET, so Patch DTOs
/// carry Newtonsoft attributes too. All calls here use plain JsonConvert with NO settings —
/// the per-property attributes are the whole registration story.
/// </summary>
public class GenerateDtosPatchNewtonsoftWireFormatTests
{
    [Fact]
    public void AbsentProperties_DeserializeAsUnspecified()
    {
        var patch = JsonConvert.DeserializeObject<PatchTestEntityPatch>("{}")!;

        patch.Name.HasValue.Should().BeFalse();
        patch.Email.HasValue.Should().BeFalse();
        patch.IsActive.HasValue.Should().BeFalse();
    }

    [Fact]
    public void ExplicitNull_OnNullableProperty_IsASpecifiedNull()
    {
        var patch = JsonConvert.DeserializeObject<PatchTestEntityPatch>("""{"Email":null}""")!;

        patch.Email.HasValue.Should().BeTrue();
        patch.Email.Value.Should().BeNull();
    }

    [Fact]
    public void ExplicitNull_OnNonNullableValueType_Throws()
    {
        var act = () => JsonConvert.DeserializeObject<PatchTestEntityPatch>("""{"IsActive":null}""");

        act.Should().Throw<JsonSerializationException>();
    }

    [Fact]
    public void PresentValue_IsSpecified()
    {
        var patch = JsonConvert.DeserializeObject<PatchTestEntityPatch>("""{"Name":"renamed"}""")!;

        patch.Name.HasValue.Should().BeTrue();
        patch.Name.Value.Should().Be("renamed");
        patch.Email.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Serialization_SkipsUnspecifiedProperties()
    {
        var patch = new PatchTestEntityPatch
        {
            Name = new Optional<string>("renamed"),
        };

        var json = JsonConvert.SerializeObject(patch);

        json.Should().Be("""{"Name":"renamed"}""",
            "unspecified properties must not appear on the wire, or a round-trip would clobber them");
    }
}
