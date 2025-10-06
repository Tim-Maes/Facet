using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Core.FacetKinds;

public class FacetKindsTests
{
    [Fact]
    public void ToFacet_ShouldGenerateRecord_WhenKindIsRecord()
    {
        // Arrange
        var product = TestDataFactory.CreateProduct("Record Product");

        // Act
        var dto = product.ToFacet<Product, ProductDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.GetType().IsClass.Should().BeTrue();
        
        // Records in C# are reference types (classes) but with value-like equality
        var dto2 = product.ToFacet<Product, ProductDto>();
        dto.Equals(dto2).Should().BeTrue("Records should have value equality");
    }

    [Fact]
    public void ToFacet_ShouldHandleDifferentSourceTypes()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("Multi", "Kind");
        var product = TestDataFactory.CreateProduct("Test Product");

        // Act
        var userDto = user.ToFacet<User, UserDto>(); // Class
        var productDto = product.ToFacet<Product, ProductDto>(); // Record

        // Assert
        userDto.GetType().IsClass.Should().BeTrue();
        userDto.FirstName.Should().Be("Multi");
        userDto.LastName.Should().Be("Kind");
        
        // ProductDto is a record, so it should support record equality
        var productDto2 = product.ToFacet<Product, ProductDto>();
        productDto.Equals(productDto2).Should().BeTrue("Records should have value equality");
    }

    [Theory]
    [InlineData(FacetKind.Class)]
    [InlineData(FacetKind.Record)]
    [InlineData(FacetKind.Struct)]
    [InlineData(FacetKind.RecordStruct)]
    public void FacetKind_ShouldHaveCorrectValues_ForAllSupportedKinds(FacetKind kind)
    {
        // Assert
        Enum.IsDefined(typeof(FacetKind), kind).Should().BeTrue($"{kind} should be a valid FacetKind");
    }

    [Fact]
    public void ToFacet_RecordType_ShouldSupportWithExpressions()
    {
        // Arrange
        var product = TestDataFactory.CreateProduct("Original Name", 100m);
        var dto = product.ToFacet<Product, ProductDto>();

        // Act
        var modifiedDto = dto with { Name = "Modified Name" };

        // Assert
        modifiedDto.Name.Should().Be("Modified Name");
        modifiedDto.Price.Should().Be(100m);
        dto.Name.Should().Be("Original Name");
    }

    [Fact]
    public void FacetKind_ShouldSupportValueTypes_EvenIfToFacetDoesNot()
    {
        // Note: While ToFacet extension method requires reference types,
        // the Facet generator can still generate value types (structs, record structs)
        // They would be used differently, without the ToFacet extension method
        
        // Assert
        typeof(ProductSummary).IsValueType.Should().BeTrue("ProductSummary should be a struct");
        typeof(UserSummary).IsValueType.Should().BeTrue("UserSummary should be a record struct");
    }
}