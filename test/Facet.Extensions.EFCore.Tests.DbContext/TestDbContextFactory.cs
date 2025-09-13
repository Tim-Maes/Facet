using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Facet.Extensions.EFCore.Tests.TestData;

// Provides a design-time factory so the ExportEfModelTask (and standard EF tooling)
// can create the DbContext without needing to guess providers or constructors.
public sealed class TestDbContextFactory : IDesignTimeDbContextFactory<TestDbContext>
{
    public TestDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("FacetEfExport")
            .EnableSensitiveDataLogging()
            .Options;

        return new TestDbContext(options);
    }
}
