using System.Text.Json;
using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for the generated System.Text.Json wire format of Patch DTOs: RFC 7396
/// (JSON Merge Patch) semantics via <see cref="Optional{T}"/> — an absent property is
/// unspecified ("don't touch"), an explicit null is a specified null ("set to null",
/// rejected for non-nullable value types), and a value is a specified value.
/// </summary>
public class GenerateDtosPatchWireFormatTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void AbsentProperties_DeserializeAsUnspecified_AndApplyToTouchesNothing()
    {
        var patch = JsonSerializer.Deserialize<PatchTestEntityPatch>("{}", Web)!;

        patch.Name.HasValue.Should().BeFalse();
        patch.Email.HasValue.Should().BeFalse();
        patch.IsActive.HasValue.Should().BeFalse();

        var entity = new PatchTestEntity { Id = 1, Name = "original", Email = "a@b.c", IsActive = true, Price = 9.99m };
        patch.ApplyTo(entity);

        entity.Name.Should().Be("original");
        entity.Email.Should().Be("a@b.c");
        entity.IsActive.Should().BeTrue();
        entity.Price.Should().Be(9.99m);
    }

    [Fact]
    public void ExplicitNull_OnNullableProperty_IsASpecifiedNull()
    {
        var patch = JsonSerializer.Deserialize<PatchTestEntityPatch>("""{"email":null}""", Web)!;

        patch.Email.HasValue.Should().BeTrue("null was explicitly sent — that is 'set to null', not 'don't touch'");
        patch.Email.Value.Should().BeNull();

        var entity = new PatchTestEntity { Email = "a@b.c" };
        patch.ApplyTo(entity);
        entity.Email.Should().BeNull();
    }

    [Fact]
    public void ExplicitNull_OnNonNullableValueType_ThrowsJsonException()
    {
        // ASP.NET Core surfaces this as an HTTP 400 — "null into a non-nullable
        // property errors" instead of being silently reinterpreted.
        var act = () => JsonSerializer.Deserialize<PatchTestEntityPatch>("""{"isActive":null}""", Web);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void PresentValue_IsSpecified_AndApplyToSetsOnlyThat()
    {
        var patch = JsonSerializer.Deserialize<PatchTestEntityPatch>("""{"name":"renamed"}""", Web)!;

        patch.Name.HasValue.Should().BeTrue();
        patch.Name.Value.Should().Be("renamed");

        var entity = new PatchTestEntity { Name = "original", Email = "keep@me.com", IsActive = true };
        patch.ApplyTo(entity);

        entity.Name.Should().Be("renamed");
        entity.Email.Should().Be("keep@me.com");
        entity.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Serialization_SkipsUnspecifiedProperties()
    {
        var patch = new PatchTestEntityPatch
        {
            Name = new Optional<string>("renamed"),
        };

        var json = JsonSerializer.Serialize(patch, Web);

        json.Should().Be("""{"name":"renamed"}""",
            "unspecified properties must not appear on the wire, or a round-trip would clobber them");
    }

    [Fact]
    public void Serialization_WritesSpecifiedNull()
    {
        var patch = new PatchTestEntityPatch
        {
            Email = new Optional<string?>(null),
        };

        var json = JsonSerializer.Serialize(patch, Web);

        json.Should().Be("""{"email":null}""");
    }
}
