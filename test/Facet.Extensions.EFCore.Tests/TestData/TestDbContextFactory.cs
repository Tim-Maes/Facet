using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Facet.Extensions.EFCore.Tests.TestData;

/// <summary>
/// Design-time factory for creating TestDbContext instances.
/// This allows the MSBuild task to create the DbContext without constructor parameters.
/// </summary>
public class TestDbContextFactory : IDesignTimeDbContextFactory<TestDbContext>
{
    public TestDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        
        // Use InMemory database for design-time operations
        optionsBuilder.UseInMemoryDatabase("DesignTimeTestDb");
        
        return new TestDbContext(optionsBuilder.Options);
    }
}