using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Extensions.Mapping;

public class AsyncMappingTests
{
    [Fact]
    public async Task ToFacetAsync_ShouldMapSingleInstance()
    {
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com", new DateTime(1990, 1, 1));

        var result = await user.ToFacetAsync<UserDto, UserDtoAsyncMapper>();

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
        var user = TestDataFactory.CreateUser();
        var cts = new CancellationTokenSource();
        cts.Cancel(); 

        var act = () => user.ToFacetAsync<UserDto, UserDtoAsyncMapper>(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ToFacetAsync_ShouldCalculateAgeCorrectly()
    {
        var birthDate = DateTime.Today.AddYears(-25);
        var user = TestDataFactory.CreateUser("Jane", "Smith", dateOfBirth: birthDate);

        var result = await user.ToFacetAsync<UserDto, UserDtoAsyncMapper>();

        result.Age.Should().Be(25);
        result.FullName.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task ToFacetAsync_ShouldWorkWithDifferentSourceTypes()
    {
        var product = new Product 
        { 
            Id = 1, 
            Name = "Test Product", 
            Price = 99.99m, 
            CategoryId = 5,
            IsAvailable = true
        };

        var result = await product.ToFacetAsync<ProductDto, ProductDtoAsyncMapper>();

        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("Test Product");
        result.Price.Should().Be(99.99m);
    }

    [Fact]
    public async Task ToFacetsAsync_ShouldMapCollection()
    {
        var users = new List<User>
        {
            TestDataFactory.CreateUser("John", "Doe", "john@example.com"),
            TestDataFactory.CreateUser("Jane", "Smith", "jane@example.com"),
            TestDataFactory.CreateUser("Bob", "Johnson", "bob@example.com")
        };

        var results = await users.ToFacetsAsync<UserDto, UserDtoAsyncMapper>();

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
        var emptyUsers = new List<User>();

        var results = await emptyUsers.ToFacetsAsync<UserDto, UserDtoAsyncMapper>();

        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ToFacetsAsync_ShouldHandleCancellation()
    {
        var users = new List<User>
        {
            TestDataFactory.CreateUser("Test1", "User1"),
            TestDataFactory.CreateUser("Test2", "User2")
        };
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(5)); 

        var act = () => users.ToFacetsAsync<UserDto, UserDtoAsyncMapper>(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ToFacetsParallelAsync_ShouldMapCollectionInParallel()
    {
        var users = new List<User>
        {
            TestDataFactory.CreateUser("John", "Doe", "john@example.com"),
            TestDataFactory.CreateUser("Jane", "Smith", "jane@example.com"),
            TestDataFactory.CreateUser("Bob", "Johnson", "bob@example.com"),
            TestDataFactory.CreateUser("Alice", "Williams", "alice@example.com")
        };

        var results = await users.ToFacetsParallelAsync<UserDto, UserDtoAsyncMapper>(
            maxDegreeOfParallelism: 2);

        results.Should().NotBeNull();
        results.Should().HaveCount(4);
        
        results.Should().Contain(r => r.FirstName == "John" && r.FullName == "John Doe");
        results.Should().Contain(r => r.FirstName == "Jane" && r.FullName == "Jane Smith");
        results.Should().Contain(r => r.FirstName == "Bob" && r.FullName == "Bob Johnson");
        results.Should().Contain(r => r.FirstName == "Alice" && r.FullName == "Alice Williams");
    }

    [Fact]
    public async Task ToFacetsParallelAsync_ShouldUseDefaultParallelism()
    {
        var users = new List<User>
        {
            TestDataFactory.CreateUser("User1", "Test1"),
            TestDataFactory.CreateUser("User2", "Test2")
        };

        var results = await users.ToFacetsParallelAsync<UserDto, UserDtoAsyncMapper>();

        results.Should().NotBeNull();
        results.Should().HaveCount(2);
        results.All(r => !string.IsNullOrEmpty(r.FullName)).Should().BeTrue();
    }

    [Fact]
    public async Task ToFacetsParallelAsync_ShouldHandleCancellation()
    {
        var users = new List<User>
        {
            TestDataFactory.CreateUser("Test1", "User1"),
            TestDataFactory.CreateUser("Test2", "User2")
        };
        var cts = new CancellationTokenSource();
        cts.Cancel(); 

        var act = () => users.ToFacetsParallelAsync<UserDto, UserDtoAsyncMapper>(
            cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ToFacetHybridAsync_ShouldApplyBothSyncAndAsyncMapping()
    {
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com", new DateTime(1990, 1, 1));

        var result = await user.ToFacetHybridAsync<UserDto, UserDtoHybridMapper>();

        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Email.Should().Be("john@example.com");
        
        result.FullName.Should().Be("John Doe (Hybrid)"); 
        result.Age.Should().BeGreaterThan(30);
    }

    [Fact]
    public async Task ToFacetHybridAsync_ShouldHandleCancellation()
    {
        var user = TestDataFactory.CreateUser();
        var cts = new CancellationTokenSource();
        cts.Cancel(); 

        var act = () => user.ToFacetHybridAsync<UserDto, UserDtoHybridMapper>(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ToFacetHybridAsync_ShouldCalculateCorrectAge()
    {
        var birthDate = DateTime.Today.AddYears(-30).AddDays(-100); 
        var user = TestDataFactory.CreateUser("Alice", "Johnson", dateOfBirth: birthDate);

        var result = await user.ToFacetHybridAsync<UserDto, UserDtoHybridMapper>();

        result.Age.Should().Be(30);
        result.FullName.Should().Be("Alice Johnson (Hybrid)");
    }

    [Fact]
    public async Task ToFacetAsync_ShouldThrowWhenSourceIsNull()
    {
        User nullUser = null!;

        var act = () => nullUser.ToFacetAsync<UserDto, UserDtoAsyncMapper>();
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*source*");
    }

    [Fact]
    public async Task ToFacetsAsync_ShouldThrowWhenSourceIsNull()
    {
        System.Collections.IEnumerable nullUsers = null!;

        var act = () => nullUsers.ToFacetsAsync<UserDto, UserDtoAsyncMapper>();
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*source*");
    }

    [Fact]
    public async Task ToFacetsParallelAsync_ShouldThrowWhenSourceIsNull()
    {
        System.Collections.IEnumerable nullUsers = null!;

        var act = () => nullUsers.ToFacetsParallelAsync<UserDto, UserDtoAsyncMapper>();
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*source*");
    }

    [Fact]
    public async Task ToFacetHybridAsync_ShouldThrowWhenSourceIsNull()
    {
        User nullUser = null!;

        var act = () => nullUser.ToFacetHybridAsync<UserDto, UserDtoHybridMapper>();
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*source*");
    }

    [Fact]
    public async Task SimplifiedSyntax_ShouldProduceEquivalentResults_ToExplicitSyntax()
    {
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");

        var explicitResult = await user.ToFacetAsync<User, UserDto, UserDtoAsyncMapper>();
        var simplifiedResult = await user.ToFacetAsync<UserDto, UserDtoAsyncMapper>();

        explicitResult.Should().BeEquivalentTo(simplifiedResult);
        
        explicitResult.FullName.Should().Be("John Doe");
        simplifiedResult.FullName.Should().Be("John Doe");
    }
}
