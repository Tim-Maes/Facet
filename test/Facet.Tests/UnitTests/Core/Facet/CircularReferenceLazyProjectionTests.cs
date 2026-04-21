using System.Linq.Expressions;

namespace Facet.Tests.UnitTests.Core.Facet;

// Entities with circular references: Project <-> Task
public class ProjectEntityCR
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<TaskEntityCR> Tasks { get; set; } = new();
}

public class TaskEntityCR
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ProjectEntityCR? Project { get; set; }
}

// Facets with Configuration (triggers lazy projection path) and circular nested facets
[Facet(typeof(ProjectEntityCR),
       Configuration = typeof(ProjectDtoCRMapConfig),
       NestedFacets = [typeof(TaskDtoCR)],
       MaxDepth = 2)]
public partial class ProjectDtoCR
{
}

public class ProjectDtoCRMapConfig
    : IFacetProjectionMapConfiguration<ProjectEntityCR, ProjectDtoCR>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<ProjectEntityCR, ProjectDtoCR> builder)
    {
        builder.Map(d => d.Name, s => "PRJ-" + s.Name);
    }
}

[Facet(typeof(TaskEntityCR),
       Configuration = typeof(TaskDtoCRMapConfig),
       NestedFacets = [typeof(ProjectDtoCR)],
       MaxDepth = 2)]
public partial class TaskDtoCR
{
}

public class TaskDtoCRMapConfig
    : IFacetProjectionMapConfiguration<TaskEntityCR, TaskDtoCR>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<TaskEntityCR, TaskDtoCR> builder)
    {
        builder.Map(d => d.Title, s => "TASK-" + s.Title);
    }
}

public class CircularReferenceLazyProjectionTests
{
    [Fact]
    public void Projection_WithCircularNestedFacets_ShouldNotStackOverflow()
    {
        // This test verifies that accessing Projection on facets with circular
        // nested references does not cause a StackOverflowException.
        // Before the fix, ProjectDtoCR.Projection would trigger TaskDtoCR.Projection
        // which would trigger ProjectDtoCR.Projection again, causing infinite recursion.

        // Act - should not throw StackOverflowException
        var projection = ProjectDtoCR.Projection;

        // Assert - projection was built successfully
        projection.Should().NotBeNull();
        projection.Should().BeAssignableTo<Expression<Func<ProjectEntityCR, ProjectDtoCR>>>();
    }

    [Fact]
    public void Projection_WithCircularNestedFacets_ShouldMapScalarProperties()
    {
        // Arrange
        var entity = new ProjectEntityCR
        {
            Id = 1,
            Name = "Alpha",
            Tasks = new List<TaskEntityCR>
            {
                new TaskEntityCR { Id = 10, Title = "Design" }
            }
        };

        // Act
        var compiled = ProjectDtoCR.Projection.Compile();
        var dto = compiled(entity);

        // Assert - scalar properties and ConfigureProjection overrides should work
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("PRJ-Alpha");
    }

    [Fact]
    public void Projection_WithCircularNestedFacets_BothDirections_ShouldNotStackOverflow()
    {
        // Access projection from the other direction too
        var taskProjection = TaskDtoCR.Projection;

        taskProjection.Should().NotBeNull();
        taskProjection.Should().BeAssignableTo<Expression<Func<TaskEntityCR, TaskDtoCR>>>();

        var entity = new TaskEntityCR
        {
            Id = 10,
            Title = "Design",
            Project = new ProjectEntityCR { Id = 1, Name = "Alpha" }
        };

        var compiled = taskProjection.Compile();
        var dto = compiled(entity);

        dto.Id.Should().Be(10);
        dto.Title.Should().Be("TASK-Design");
    }
}
