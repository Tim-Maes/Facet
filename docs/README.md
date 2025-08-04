# Facet Documentation Index

Welcome to the Facet documentation! This index will help you navigate all available guides and references for using Facet and its extensions.

## Table of Contents

- [Facetting](01_Facetting.md): Introduction to Facetting
- [Quick Start](02_QuickStart.md): Quick Start Guide
- [Attribute Reference](03_AttributeReference.md): Facet Attribute Reference
- [Custom Mapping](04_CustomMapping.md): Custom Mapping with IFacetMapConfiguration & Async Support
- [Extension Methods](05_Extensions.md): Extension Methods (LINQ, EF Core, etc.)
- [Advanced Scenarios](06_AdvancedScenarios.md): Advanced Usage Scenarios
- [What is Being Generated?](07_WhatIsBeingGenerated.md): Before/After Examples
- [Async Mapping Guide](08_AsyncMapping.md): Asynchronous Mapping with Facet.Mapping
- [Facet.Extensions.EFCore](../src/Facet.Extensions.EFCore/README.md): EF Core Async Extension Methods
- [Facet.Mapping Reference](../src/Facet.Mapping/README.md): Complete Facet.Mapping Documentation

## Quick Reference

### Basic Usage
```csharp
[Facet(typeof(User))]
public partial class UserDto { }

var userDto = user.ToFacet<User, UserDto>();
```

### Custom Sync Mapping
```csharp
public class UserMapper : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}
```

### Async Mapping
```csharp
public class UserAsyncMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        target.ProfilePicture = await GetProfilePictureAsync(source.Id, cancellationToken);
    }
}

var userDto = await user.ToFacetAsync<User, UserDto, UserAsyncMapper>();
