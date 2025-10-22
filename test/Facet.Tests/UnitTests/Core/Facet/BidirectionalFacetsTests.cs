using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class BidirectionalFacetsTests
{
    [Fact]
    public void ToFacet_ShouldHandleBidirectionalRelationship_WhenMappingWorkerWithFirmWithoutWorkers()
    {
        // Arrange - Worker with Company, but Company facet excludes Workers (breaks cycle)
        var firm = new Firm
        {
            Id = 1,
            Name = "Tech Corp",
            Workers = new List<Worker>()
        };

        var worker = new Worker
        {
            Id = 100,
            Name = "John Doe",
            CompanyId = 1,
            Company = firm
        };

        firm.Workers.Add(worker);

        // Act - this should not cause stack overflow because FirmFacetWithoutWorkers excludes Workers
        var workerFacet = new WorkerFacet(worker);

        // Assert
        workerFacet.Should().NotBeNull();
        workerFacet.Id.Should().Be(100);
        workerFacet.Name.Should().Be("John Doe");
        workerFacet.CompanyId.Should().Be(1);
        workerFacet.Company.Should().NotBeNull();
        workerFacet.Company!.Id.Should().Be(1);
        workerFacet.Company!.Name.Should().Be("Tech Corp");

        // Company facet should NOT have Workers property (it was excluded)
        var companyType = workerFacet.Company.GetType();
        companyType.GetProperty("Workers").Should().BeNull("Workers should be excluded from FirmFacetWithoutWorkers");
    }

    [Fact]
    public void ToFacet_ShouldHandleBidirectionalRelationship_WhenMappingFirmWithWorkersWithoutCompany()
    {
        // Arrange - Firm with Workers, but Worker facet excludes Company (breaks cycle)
        var firm = new Firm
        {
            Id = 1,
            Name = "Tech Corp",
            Workers = new List<Worker>()
        };

        var worker1 = new Worker
        {
            Id = 100,
            Name = "John Doe",
            CompanyId = 1,
            Company = firm
        };

        var worker2 = new Worker
        {
            Id = 101,
            Name = "Jane Smith",
            CompanyId = 1,
            Company = firm
        };

        firm.Workers.Add(worker1);
        firm.Workers.Add(worker2);

        // Act - this should not cause stack overflow because WorkerFacetWithoutCompany excludes Company
        var firmFacet = new FirmFacet(firm);

        // Assert
        firmFacet.Should().NotBeNull();
        firmFacet.Id.Should().Be(1);
        firmFacet.Name.Should().Be("Tech Corp");
        firmFacet.Workers.Should().NotBeNull();
        firmFacet.Workers.Should().HaveCount(2);
        firmFacet.Workers.Should().AllBeOfType<WorkerFacetWithoutCompany>();

        firmFacet.Workers.First().Name.Should().Be("John Doe");
        firmFacet.Workers.Last().Name.Should().Be("Jane Smith");

        // Worker facets should NOT have Company property (it was excluded)
        var workerType = firmFacet.Workers.First().GetType();
        workerType.GetProperty("Company").Should().BeNull("Company should be excluded from WorkerFacetWithoutCompany");
    }

    [Fact]
    public void BackTo_ShouldHandleBidirectionalRelationship_WhenMappingBack()
    {
        // Arrange
        var firm = new Firm
        {
            Id = 1,
            Name = "Tech Corp",
            Workers = new List<Worker>()
        };

        var worker = new Worker
        {
            Id = 100,
            Name = "John Doe",
            CompanyId = 1,
            Company = firm
        };

        firm.Workers.Add(worker);

        var workerFacet = new WorkerFacet(worker);

        // Act - map back to source
        var mappedWorker = workerFacet.BackTo();

        // Assert
        mappedWorker.Should().NotBeNull();
        mappedWorker.Id.Should().Be(100);
        mappedWorker.Name.Should().Be("John Doe");
        mappedWorker.CompanyId.Should().Be(1);
        mappedWorker.Company.Should().NotBeNull();
        mappedWorker.Company!.Id.Should().Be(1);
        mappedWorker.Company!.Name.Should().Be("Tech Corp");
    }

    [Fact]
    public void Projection_ShouldHandleBidirectionalRelationship_WithoutStackOverflow()
    {
        // Arrange
        var firms = new List<Firm>
        {
            new Firm
            {
                Id = 1,
                Name = "Tech Corp",
                Workers = new List<Worker>
                {
                    new Worker { Id = 100, Name = "John Doe", CompanyId = 1 },
                    new Worker { Id = 101, Name = "Jane Smith", CompanyId = 1 }
                }
            }
        };

        // Act - projection should compile and not cause stack overflow during generation
        // FirmFacet uses WorkerFacetWithoutCompany which excludes the Company property
        var projection = FirmFacet.Projection;
        var projected = firms.AsQueryable().Select(projection).ToList();

        // Assert
        projected.Should().NotBeNull();
        projected.Should().HaveCount(1);
        projected.First().Name.Should().Be("Tech Corp");
        projected.First().Workers.Should().HaveCount(2);
        projected.First().Workers.Should().AllBeOfType<WorkerFacetWithoutCompany>();
    }
}
