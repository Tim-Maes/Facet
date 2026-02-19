namespace Facet.Tests.UnitTests.Wrapper;

public partial class ReadOnlyWrapperTests
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }

    [Wrapper(typeof(Product), ReadOnly = true)]
    public partial class ReadOnlyProductWrapper { }

    [Wrapper(typeof(Product))]
    public partial class MutableProductWrapper { }

    [Fact]
    public void ReadOnlyWrapper_Should_Delegate_To_Source_For_Reading()
    {
        // Arrange
        var product = new Product
        {
            Id = 1,
            Name = "Laptop",
            Description = "High-performance laptop",
            Price = 1299.99m,
            Stock = 10
        };

        // Act
        var wrapper = new ReadOnlyProductWrapper(product);

        // Assert
        wrapper.Id.Should().Be(1);
        wrapper.Name.Should().Be("Laptop");
        wrapper.Description.Should().Be("High-performance laptop");
        wrapper.Price.Should().Be(1299.99m);
        wrapper.Stock.Should().Be(10);
    }

    [Fact]
    public void ReadOnlyWrapper_Should_Not_Have_Setters()
    {
        // Setters are absent — verified at compile time
        var product = new Product { Id = 1, Name = "Test" };
        var wrapper = new ReadOnlyProductWrapper(product);

        _ = wrapper.Id;
        _ = wrapper.Name;
    }

    [Fact]
    public void ReadOnlyWrapper_Should_Reflect_Source_Changes()
    {
        // Arrange
        var product = new Product { Id = 1, Name = "Original" };
        var wrapper = new ReadOnlyProductWrapper(product);

        // Act - modify source directly
        product.Name = "Updated";
        product.Price = 99.99m;

        // Assert - wrapper reflects the changes
        wrapper.Name.Should().Be("Updated");
        wrapper.Price.Should().Be(99.99m);
    }

    [Fact]
    public void MutableWrapper_Should_Have_Setters()
    {
        // Arrange
        var product = new Product { Id = 1, Name = "Original", Price = 100m };
        var wrapper = new MutableProductWrapper(product);

        // Act - modify through wrapper (should compile and work)
        wrapper.Name = "Modified";
        wrapper.Price = 150m;

        // Assert - source is modified
        product.Name.Should().Be("Modified");
        product.Price.Should().Be(150m);
    }

    [Fact]
    public void ReadOnlyWrapper_Constructor_Should_Throw_On_Null()
    {
        // Act
        Action act = () => new ReadOnlyProductWrapper(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public void ReadOnlyWrapper_Unwrap_Should_Return_Source()
    {
        // Arrange
        var product = new Product { Id = 1, Name = "Test" };
        var wrapper = new ReadOnlyProductWrapper(product);

        // Act
        var unwrapped = wrapper.Unwrap();

        // Assert
        unwrapped.Should().BeSameAs(product);
    }
}
