using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Core.Facet;

public class ApplyToSourceTests
{
    [Fact]
    public void ApplyToSource_ShouldUpdateExistingInstance_WithMappedProperties()
    {
        var original = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var dto = new UserDto(original);
        dto.FirstName = "Jane";
        dto.Email = "jane@example.com";

        var target = TestDataFactory.CreateUser("John", "Doe", "john@example.com");

        dto.ApplyToSource(target);

        target.FirstName.Should().Be("Jane");
        target.Email.Should().Be("jane@example.com");
        target.LastName.Should().Be("Doe");
    }

    [Fact]
    public void ApplyToSource_ShouldOnlyUpdateIncludedProperties_LeavingExcludedPropertiesUnchanged()
    {
        var original = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var dto = new UserDto(original);

        var target = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var originalPassword = target.Password;
        var originalCreatedAt = target.CreatedAt;

        dto.ApplyToSource(target);

        target.Password.Should().Be(originalPassword);
        target.CreatedAt.Should().Be(originalCreatedAt);
    }

    [Fact]
    public void ApplyToSource_ShouldMutateInstance_NotCreateNewOne()
    {
        var original = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var dto = new UserDto(original);
        dto.FirstName = "Alice";

        var target = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var sameRef = target;

        dto.ApplyToSource(target);

        object.ReferenceEquals(target, sameRef).Should().BeTrue();
        target.FirstName.Should().Be("Alice");
    }

    [Fact]
    public void ApplyToSource_ShouldHandleInclude_UpdatesOnlyIncludedFields()
    {
        var original = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var dto = new UserIncludeDto(original);
        dto.FirstName = "Bob";
        dto.LastName = "Builder";
        dto.Email = "bob@example.com";

        var target = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var originalId = target.Id;

        dto.ApplyToSource(target);

        target.FirstName.Should().Be("Bob");
        target.LastName.Should().Be("Builder");
        target.Email.Should().Be("bob@example.com");
        
        target.Id.Should().Be(originalId);
    }

    [Fact]
    public void ApplyToSource_ShouldRoundtrip_WhenValuesAreModifiedOnDto()
    {
        var entity = TestDataFactory.CreateUser("Alice", "Wonderland", "alice@example.com");
        entity.IsActive = true;
        var dto = new UserDto(entity);

        dto.FirstName = "Alicia";
        dto.IsActive = false;

        var existingEntity = TestDataFactory.CreateUser("Alice", "Wonderland", "alice@example.com");
        existingEntity.IsActive = true;

        dto.ApplyToSource(existingEntity);

        existingEntity.FirstName.Should().Be("Alicia");
        existingEntity.IsActive.Should().BeFalse();
        existingEntity.LastName.Should().Be("Wonderland");
    }

    [Fact]
    public void ApplyToSource_WithNullableProperty_ShouldSetNullOnTarget()
    {
        var original = TestDataFactory.CreateUser();
        original.LastLoginAt = DateTime.UtcNow;
        var dto = new UserDto(original);
        dto.LastLoginAt = null;

        var target = TestDataFactory.CreateUser();
        target.LastLoginAt = DateTime.UtcNow;

        dto.ApplyToSource(target);

        target.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void ApplyToSource_WithEmployee_ShouldUpdateInheritedProperties()
    {
        var original = TestDataFactory.CreateEmployee("Jane", "Smith");
        var dto = new EmployeeDto(original);
        dto.FirstName = "Janet";
        dto.Department = "Finance";

        var target = TestDataFactory.CreateEmployee("Jane", "Smith");

        dto.ApplyToSource(target);

        target.FirstName.Should().Be("Janet");
        target.Department.Should().Be("Finance");
        target.LastName.Should().Be("Smith");
    }

    [Fact]
    public void ClassicUserDto_ShouldNotHaveApplyToSource_BecauseSourceIsPositionalRecord()
    {
        var type = typeof(ClassicUserDto);
        var method = type.GetMethod("ApplyToSource");

        method.Should().BeNull("positional record sources do not support mutation via ApplyToSource");
    }

    [Fact]
    public void ProductDto_ShouldHaveApplyToSource_BecauseProductIsAMutableClass()
    {
        var type = typeof(ProductDto);
        var method = type.GetMethod("ApplyToSource");

        method.Should().NotBeNull("Product is a mutable class, so ApplyToSource should be generated");
    }
}
