using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for Patch DTO generation and functionality.
/// Verifies that generated Patch DTOs work correctly for partial updates.
/// </summary>
public class GenerateDtosPatchTests
{
    [Fact]
    public void PatchDto_ApplyTo_ShouldOnlyUpdateSpecifiedProperties()
    {
        var entity = new PatchTestEntity
        {
            Id = 1,
            Name = "Original",
            Email = "original@example.com",
            IsActive = true,
            Price = 100m
        };

        var patch = new PatchTestEntityPatch
        {
            Name = new Optional<string>("Updated"),
            Price = new Optional<decimal>(200m)
            
        };

        patch.ApplyTo(entity);

        entity.Name.Should().Be("Updated", "Name was specified in patch");
        entity.Price.Should().Be(200m, "Price was specified in patch");
        entity.Email.Should().Be("original@example.com", "Email was not specified in patch");
        entity.IsActive.Should().BeTrue("IsActive was not specified in patch");
        entity.Id.Should().Be(1, "Id was not specified in patch");
    }

    [Fact]
    public void PatchDto_ApplyTo_ShouldHandleExplicitNullValues()
    {
        var entity = new PatchTestEntity
        {
            Id = 1,
            Name = "Test",
            Email = "test@example.com",
            LastLoginAt = DateTime.Now
        };

        var patch = new PatchTestEntityPatch
        {
            Email = new Optional<string?>(null),  
            LastLoginAt = new Optional<DateTime?>(null)  
        };

        patch.ApplyTo(entity);

        entity.Email.Should().BeNull("Email was explicitly set to null");
        entity.LastLoginAt.Should().BeNull("LastLoginAt was explicitly set to null");
        entity.Name.Should().Be("Test", "Name was not modified");
    }

    [Fact]
    public void PatchDto_ApplyTo_ShouldNotUpdateUnspecifiedProperties()
    {
        var originalDate = DateTime.Now;
        var entity = new PatchTestEntity
        {
            Id = 1,
            Name = "Original",
            Email = "original@example.com",
            IsActive = true,
            LastLoginAt = originalDate,
            Price = 100m
        };

        var patch = new PatchTestEntityPatch();
        
        patch.ApplyTo(entity);

        entity.Id.Should().Be(1);
        entity.Name.Should().Be("Original");
        entity.Email.Should().Be("original@example.com");
        entity.IsActive.Should().BeTrue();
        entity.LastLoginAt.Should().Be(originalDate);
        entity.Price.Should().Be(100m);
    }

    [Fact]
    public void PatchDto_ApplyTo_ShouldHandleValueTypes()
    {
        var entity = new PatchTestEntity
        {
            Id = 1,
            IsActive = false,
            Price = 100m
        };

        var patch = new PatchTestEntityPatch
        {
            IsActive = new Optional<bool>(true),
            Price = new Optional<decimal>(250.50m),
            Id = new Optional<int>(42)
        };

        patch.ApplyTo(entity);

        entity.IsActive.Should().BeTrue("IsActive was updated to true");
        entity.Price.Should().Be(250.50m, "Price was updated");
        entity.Id.Should().Be(42, "Id was updated");
    }

    [Fact]
    public void PatchDto_ApplyTo_WithNullTarget_ShouldThrow()
    {
        var patch = new PatchTestEntityPatch
        {
            Name = new Optional<string>("Test")
        };

        Action act = () => patch.ApplyTo(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PatchDto_ApplyTo_ShouldHandleAllPropertyUpdatesInSingleCall()
    {
        var entity = new PatchTestEntity
        {
            Id = 1,
            Name = "Old",
            Email = "old@example.com",
            IsActive = false,
            Price = 50m,
            LastLoginAt = null
        };

        var newLoginDate = DateTime.Now;
        var patch = new PatchTestEntityPatch
        {
            Id = new Optional<int>(42),
            Name = new Optional<string>("New"),
            Email = new Optional<string?>("new@example.com"),
            IsActive = new Optional<bool>(true),
            Price = new Optional<decimal>(150m),
            LastLoginAt = new Optional<DateTime?>(newLoginDate)
        };

        patch.ApplyTo(entity);

        entity.Id.Should().Be(42);
        entity.Name.Should().Be("New");
        entity.Email.Should().Be("new@example.com");
        entity.IsActive.Should().BeTrue();
        entity.Price.Should().Be(150m);
        entity.LastLoginAt.Should().Be(newLoginDate);
    }

    [Fact]
    public void PatchDto_ApplyTo_ShouldDistinguishBetweenNullAndUnspecified()
    {
        var entity1 = new PatchTestEntity
        {
            Id = 1,
            Email = "test1@example.com",
            LastLoginAt = DateTime.Now
        };

        var entity2 = new PatchTestEntity
        {
            Id = 2,
            Email = "test2@example.com",
            LastLoginAt = DateTime.Now
        };

        var patchWithNull = new PatchTestEntityPatch
        {
            Email = new Optional<string?>(null),  
            LastLoginAt = new Optional<DateTime?>(null)  
        };

        var patchWithUnspecified = new PatchTestEntityPatch();
        
        patchWithNull.ApplyTo(entity1);
        patchWithUnspecified.ApplyTo(entity2);

        entity1.Email.Should().BeNull("Email was explicitly set to null");
        entity1.LastLoginAt.Should().BeNull("LastLoginAt was explicitly set to null");

        entity2.Email.Should().Be("test2@example.com", "Email was unspecified, so it kept its original value");
        entity2.LastLoginAt.Should().NotBeNull("LastLoginAt was unspecified, so it kept its original value");
    }

    [Fact]
    public void PatchDto_ImplicitConversion_ShouldWork()
    {
        var entity = new PatchTestEntity
        {
            Name = "Original"
        };

        var patch = new PatchTestEntityPatch
        {
            Name = "Updated"  
        };
        patch.ApplyTo(entity);

        entity.Name.Should().Be("Updated");
    }

    [Fact]
    public void PatchDto_Properties_ShouldBeOptionalType()
    {
        var patch = new PatchTestEntityPatch();

        patch.Id.Should().BeOfType<Optional<int>>();
        patch.Name.Should().BeOfType<Optional<string>>();
        patch.Email.Should().BeOfType<Optional<string?>>();
        patch.IsActive.Should().BeOfType<Optional<bool>>();
        patch.LastLoginAt.Should().BeOfType<Optional<DateTime?>>();
        patch.Price.Should().BeOfType<Optional<decimal>>();
    }

    [Fact]
    public void PatchDto_DefaultState_ShouldHaveNoValues()
    {
        var patch = new PatchTestEntityPatch();

        patch.Id.HasValue.Should().BeFalse();
        patch.Name.HasValue.Should().BeFalse();
        patch.Email.HasValue.Should().BeFalse();
        patch.IsActive.HasValue.Should().BeFalse();
        patch.LastLoginAt.HasValue.Should().BeFalse();
        patch.Price.HasValue.Should().BeFalse();
    }

    [Fact]
    public void PatchDto_PartialUpdate_RealWorldScenario()
    {
        var existingUser = new PatchTestEntity
        {
            Id = 123,
            Name = "John Doe",
            Email = "john@example.com",
            IsActive = true,
            LastLoginAt = new DateTime(2024, 1, 1),
            Price = 99.99m
        };

        var patchFromClient = new PatchTestEntityPatch
        {
            Name = "Jane Doe",
            IsActive = false
            
        };

        patchFromClient.ApplyTo(existingUser);

        existingUser.Id.Should().Be(123, "Id unchanged");
        existingUser.Name.Should().Be("Jane Doe", "Name updated");
        existingUser.Email.Should().Be("john@example.com", "Email unchanged");
        existingUser.IsActive.Should().BeFalse("IsActive updated");
        existingUser.LastLoginAt.Should().Be(new DateTime(2024, 1, 1), "LastLoginAt unchanged");
        existingUser.Price.Should().Be(99.99m, "Price unchanged");
    }
}
