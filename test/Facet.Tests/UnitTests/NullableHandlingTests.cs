using Facet.Tests.TestModels;
namespace Facet.Tests.UnitTests;

public class NullableHandlingTests
{
    [Fact]
    public void ToFacet_ShouldPreserveNullableStringTypes_WhenMappingToFacet()
    {
        // Arrange
        var testEntity = new NullableTestEntity
        {
            Test1 = true,
            Test2 = false,
            Test3 = "Non-nullable string",
            Test4 = null
        };

        // Act
        var dto = testEntity.ToFacet<NullableTestEntity, NullableTestDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.Test1.Should().Be(true);
        dto.Test2.Should().Be(false);
        dto.Test3.Should().Be("Non-nullable string");
        dto.Test4.Should().BeNull();
    }

    [Fact]
    public void NullableTestDto_ShouldHaveCorrectPropertyTypes()
    {
        // Arrange & Act
        var dtoType = typeof(NullableTestDto);
        
        // Assert
        var test1Property = dtoType.GetProperty("Test1");
        var test2Property = dtoType.GetProperty("Test2");
        var test3Property = dtoType.GetProperty("Test3");
        var test4Property = dtoType.GetProperty("Test4");

        test1Property.Should().NotBeNull();
        test1Property!.PropertyType.Should().Be<bool>();

        test2Property.Should().NotBeNull();
        test2Property!.PropertyType.Should().Be(typeof(bool?));

        test3Property.Should().NotBeNull();
        test3Property!.PropertyType.Should().Be<string>();

        test4Property.Should().NotBeNull();
        test4Property!.PropertyType.Should().Be<string>();
        
        // In C# 8+ with nullable reference types enabled, 
        // we need to check the nullable annotation context
        var nullabilityContext = new System.Reflection.NullabilityInfoContext();
        var test4NullabilityInfo = nullabilityContext.Create(test4Property);
        
        // Test4 should allow null values
        test4NullabilityInfo.ReadState.Should().Be(System.Reflection.NullabilityState.Nullable);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Some value")]
    public void ToFacet_ShouldHandleNullableStringAssignment_Correctly(string? testValue)
    {
        // Arrange
        var testEntity = new NullableTestEntity
        {
            Test1 = false,
            Test2 = null,
            Test3 = "Always has value",
            Test4 = testValue
        };

        // Act
        var dto = testEntity.ToFacet<NullableTestEntity, NullableTestDto>();

        // Assert
        dto.Test4.Should().Be(testValue);
    }
}