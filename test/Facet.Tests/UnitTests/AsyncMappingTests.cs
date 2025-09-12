using Facet.Tests.TestModels;
using Facet.Tests.Utilities;
using Facet.Mapping;

namespace Facet.Tests.UnitTests;

public class AsyncMappingTests
{
    #region ToFacetAsync<TTarget, TMapper> Tests

    [Fact]
    public async Task ToFacetAsync_ShouldMapSingleInstance()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com", new DateTime(1990, 1, 1));

        // Act
        var result = await user.ToFacetAsync<UserDto, UserDtoAsyncMapper>();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Email.Should().Be("john@example.com");
        result.FullName.Should().Be("John Doe");
        result.Age.Should().BeGreaterThan(30);
    }

    [Fact]
    public async Task ToFacetAsync_ShouldHandleCancellation()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var act = () => user.ToFacetAsync<UserDto, UserDtoAsyncMapper>(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ToFacetAsync_ShouldCalculateAgeCorrectly()
    {
        // Arrange
        var birthDate = DateTime.Today.AddYears(-25);
        var user = TestDataFactory.CreateUser("Jane", "Smith", dateOfBirth: birthDate);

        // Act
        var result = await user.ToFacetAsync<UserDto, UserDtoAsyncMapper>();

        // Assert
        result.Age.Should().Be(25);
        result.FullName.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task ToFacetAsync_ShouldWorkWithDifferentSourceTypes()
    {
        // Arrange
        var product = new Product 
        { 
            Id = 1, 
            Name = "Test Product", 
            Price = 99.99m, 
            CategoryId = 5,
            IsAvailable = true
        };

        // Act
        var result = await product.ToFacetAsync<ProductDto, ProductDtoAsyncMapper>();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("Test Product");
        result.Price.Should().Be(99.99m);
    }

    #endregion

    #region ToFacetsAsync<TTarget, TMapper> Tests

    [Fact]
    public async Task ToFacetsAsync_ShouldMapCollection()
    {
        // Arrange
        var users = new List<User>
        {
            TestDataFactory.CreateUser("John", "Doe", "john@example.com"),
            TestDataFactory.CreateUser("Jane", "Smith", "jane@example.com"),
            TestDataFactory.CreateUser("Bob", "Johnson", "bob@example.com")
        };

        // Act
        var results = await users.ToFacetsAsync<UserDto, UserDtoAsyncMapper>();

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(3);
        
        var first = results[0];
        first.FirstName.Should().Be("John");
        first.LastName.Should().Be("Doe");
        first.Email.Should().Be("john@example.com");
        first.FullName.Should().Be("John Doe");

        var second = results[1];
        second.FirstName.Should().Be("Jane");
        second.FullName.Should().Be("Jane Smith");
        
        var third = results[2];
        third.FirstName.Should().Be("Bob");
        third.FullName.Should().Be("Bob Johnson");
    }

    [Fact]
    public async Task ToFacetsAsync_ShouldHandleEmptyCollection()
    {
        // Arrange
        var emptyUsers = new List<User>();

        // Act
        var results = await emptyUsers.ToFacetsAsync<UserDto, UserDtoAsyncMapper>();

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ToFacetsAsync_ShouldHandleCancellation()
    {
        // Arrange
        var users = new List<User>
        {
            TestDataFactory.CreateUser("Test1", "User1"),
            TestDataFactory.CreateUser("Test2", "User2")
        };
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(5)); // Cancel quickly

        // Act & Assert
        var act = () => users.ToFacetsAsync<UserDto, UserDtoAsyncMapper>(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region ToFacetsParallelAsync<TTarget, TMapper> Tests

    [Fact]
    public async Task ToFacetsParallelAsync_ShouldMapCollectionInParallel()
    {
        // Arrange
        var users = new List<User>
        {
            TestDataFactory.CreateUser("John", "Doe", "john@example.com"),
            TestDataFactory.CreateUser("Jane", "Smith", "jane@example.com"),
            TestDataFactory.CreateUser("Bob", "Johnson", "bob@example.com"),
            TestDataFactory.CreateUser("Alice", "Williams", "alice@example.com")
        };

        // Act
        var results = await users.ToFacetsParallelAsync<UserDto, UserDtoAsyncMapper>(
            maxDegreeOfParallelism: 2);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(4);
        
        // Verify all users are mapped correctly (order might change due to parallel processing)
        results.Should().Contain(r => r.FirstName == "John" && r.FullName == "John Doe");
        results.Should().Contain(r => r.FirstName == "Jane" && r.FullName == "Jane Smith");
        results.Should().Contain(r => r.FirstName == "Bob" && r.FullName == "Bob Johnson");
        results.Should().Contain(r => r.FirstName == "Alice" && r.FullName == "Alice Williams");
    }

    [Fact]
    public async Task ToFacetsParallelAsync_ShouldUseDefaultParallelism()
    {
        // Arrange
        var users = new List<User>
        {
            TestDataFactory.CreateUser("User1", "Test1"),
            TestDataFactory.CreateUser("User2", "Test2")
        };

        // Act
        var results = await users.ToFacetsParallelAsync<UserDto, UserDtoAsyncMapper>();

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(2);
        results.All(r => !string.IsNullOrEmpty(r.FullName)).Should().BeTrue();
    }

    [Fact]
    public async Task ToFacetsParallelAsync_ShouldHandleCancellation()
    {
        // Arrange
        var users = new List<User>
        {
            TestDataFactory.CreateUser("Test1", "User1"),
            TestDataFactory.CreateUser("Test2", "User2")
        };
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var act = () => users.ToFacetsParallelAsync<UserDto, UserDtoAsyncMapper>(
            cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region ToFacetHybridAsync<TTarget, TMapper> Tests

    [Fact]
    public async Task ToFacetHybridAsync_ShouldApplyBothSyncAndAsyncMapping()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com", new DateTime(1990, 1, 1));

        // Act
        var result = await user.ToFacetHybridAsync<UserDto, UserDtoHybridMapper>();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Email.Should().Be("john@example.com");
        
        // Sync mapping results with async modification
        result.FullName.Should().Be("John Doe (Hybrid)"); // Modified by async part
        result.Age.Should().BeGreaterThan(30);
    }

    [Fact]
    public async Task ToFacetHybridAsync_ShouldHandleCancellation()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var act = () => user.ToFacetHybridAsync<UserDto, UserDtoHybridMapper>(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ToFacetHybridAsync_ShouldCalculateCorrectAge()
    {
        // Arrange
        var birthDate = DateTime.Today.AddYears(-30).AddDays(-100); // 30+ years old
        var user = TestDataFactory.CreateUser("Alice", "Johnson", dateOfBirth: birthDate);

        // Act
        var result = await user.ToFacetHybridAsync<UserDto, UserDtoHybridMapper>();

        // Assert
        result.Age.Should().Be(30);
        result.FullName.Should().Be("Alice Johnson (Hybrid)");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ToFacetAsync_ShouldThrowWhenSourceIsNull()
    {
        // Arrange
        User nullUser = null!;

        // Act & Assert
        var act = () => nullUser.ToFacetAsync<UserDto, UserDtoAsyncMapper>();
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*source*");
    }

    [Fact]
    public async Task ToFacetsAsync_ShouldThrowWhenSourceIsNull()
    {
        // Arrange
        System.Collections.IEnumerable nullUsers = null!;

        // Act & Assert
        var act = () => nullUsers.ToFacetsAsync<UserDto, UserDtoAsyncMapper>();
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*source*");
    }

    [Fact]
    public async Task ToFacetsParallelAsync_ShouldThrowWhenSourceIsNull()
    {
        // Arrange
        System.Collections.IEnumerable nullUsers = null!;

        // Act & Assert
        var act = () => nullUsers.ToFacetsParallelAsync<UserDto, UserDtoAsyncMapper>();
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*source*");
    }

    [Fact]
    public async Task ToFacetHybridAsync_ShouldThrowWhenSourceIsNull()
    {
        // Arrange
        User nullUser = null!;

        // Act & Assert
        var act = () => nullUser.ToFacetHybridAsync<UserDto, UserDtoHybridMapper>();
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*source*");
    }

    #endregion

    #region Performance Comparison Tests

    [Fact]
    public async Task SimplifiedSyntax_ShouldProduceEquivalentResults_ToExplicitSyntax()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");

        // Act - Compare both syntaxes
        var explicitResult = await user.ToFacetAsync<User, UserDto, UserDtoAsyncMapper>();
        var simplifiedResult = await user.ToFacetAsync<UserDto, UserDtoAsyncMapper>();

        // Assert
        explicitResult.Should().BeEquivalentTo(simplifiedResult);
        
        // Check that both have the expected computed fields
        explicitResult.FullName.Should().Be("John Doe");
        simplifiedResult.FullName.Should().Be("John Doe");
    }

    #endregion
}