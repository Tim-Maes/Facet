using System.Collections.Generic;

namespace Facet.Extensions.EFCore.Generators;

/// <summary>
/// Root model containing all EF contexts and their entities.
/// </summary>
internal sealed class ModelRoot
{
    public List<ContextModel> Contexts { get; set; } = new();
}

/// <summary>
/// Represents an EF DbContext and its entities.
/// </summary>
internal sealed class ContextModel
{
    public string Context { get; set; } = string.Empty;
    public List<EntityModel> Entities { get; set; } = new();
}

/// <summary>
/// Represents an EF entity type with its properties and navigations.
/// </summary>
internal sealed class EntityModel
{
    public string Name { get; set; } = string.Empty;
    public string? Clr { get; set; }
    public List<string[]> Keys { get; set; } = new();
    public List<NavigationModel> Navigations { get; set; } = new();
}

/// <summary>
/// Represents a navigation property in an EF entity.
/// </summary>
internal sealed class NavigationModel
{
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool IsCollection { get; set; }
}