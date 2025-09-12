using System.Collections.Generic;

namespace Facet.Extensions.EFCore.Generators.Shared;

/// <summary>
/// Root model containing EF contexts and entities.
/// </summary>
public class ModelRoot
{
    public List<ContextModel> Contexts { get; set; } = new();
}

/// <summary>
/// Represents an EF context with its entities.
/// </summary>
public class ContextModel
{
    public string Context { get; set; } = string.Empty;
    public List<EntityModel> Entities { get; set; } = new();
}

/// <summary>
/// Represents an entity with its properties and navigations.
/// </summary>
public class EntityModel
{
    public string Name { get; set; } = string.Empty;
    public string? Clr { get; set; }
    public List<PropertyModel> Properties { get; set; } = new();
    public List<NavigationModel> Navigations { get; set; } = new();
}

/// <summary>
/// Represents a property on an entity.
/// </summary>
public class PropertyModel
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
}

/// <summary>
/// Represents a navigation property on an entity.
/// </summary>
public class NavigationModel
{
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool IsCollection { get; set; }
}