# Facet.Dashboard

A Swagger-like dashboard for visualizing all Facet source types and their generated facets in your application.

## Features

- **Visual Overview**: See all source types and their generated facets at a glance
- **Property Inspection**: View source type properties and facet members with type information
- **Feature Indicators**: Quickly see which features are enabled (Constructor, Projection, ToSource)
- **Search & Filter**: Easily find specific types in large projects
- **Dark Mode**: Automatic dark mode support based on system preferences
- **JSON API**: Optional JSON endpoint for programmatic access
- **Authentication Support**: Optional authentication configuration

## Installation

```bash
dotnet add package Facet.Dashboard
```

## Quick Start

### 1. Add the Dashboard to Your Application

```csharp
using Facet.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Add Facet Dashboard services
builder.Services.AddFacetDashboard();

var app = builder.Build();

// Map the dashboard endpoint
app.MapFacetDashboard();

app.Run();
```

### 2. Navigate to the Dashboard

Open your browser and navigate to:
```
https://localhost:5001/facets
```

## Configuration

### Basic Configuration

```csharp
builder.Services.AddFacetDashboard(options =>
{
    options.RoutePrefix = "/facets";  // Default: "/facets"
    options.Title = "My API Facets";  // Default: "Facet Dashboard"
    options.AccentColor = "#3b82f6";  // Default: "#6366f1" (Indigo)
});
```

### Authentication

```csharp
builder.Services.AddFacetDashboard(options =>
{
    options.RequireAuthentication = true;
    options.AuthenticationPolicy = "AdminOnly";  // Optional: specific policy
});
```

### Additional Assemblies

By default, the dashboard scans the entry assembly and its references. You can add additional assemblies:

```csharp
builder.Services.AddFacetDashboard(options =>
{
    options.AdditionalAssemblies.Add(typeof(MyDtoClass).Assembly);
});
```

### Disable JSON API

```csharp
builder.Services.AddFacetDashboard(options =>
{
    options.EnableJsonApi = false;  // Default: true
});
```

## Available Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /facets` | HTML dashboard page |
| `GET /facets/api/facets` | JSON API (if enabled) |

## JSON API Response

The JSON API returns all facet mappings in a structured format:

```json
[
  {
    "sourceTypeName": "MyApp.Models.User",
    "sourceTypeSimpleName": "User",
    "sourceTypeNamespace": "MyApp.Models",
    "sourceMembers": [
      {
        "name": "Id",
        "typeName": "int",
        "isProperty": true,
        "isNullable": false,
        "isRequired": false
      }
    ],
    "facets": [
      {
        "facetTypeName": "MyApp.DTOs.UserDto",
        "facetTypeSimpleName": "UserDto",
        "typeKind": "record",
        "hasConstructor": true,
        "hasProjection": true,
        "hasToSource": false,
        "excludedProperties": ["Password"],
        "members": [...]
      }
    ]
  }
]
```

## Dashboard Features

### Source Type Cards

Each source type is displayed as an expandable card showing:
- Type name and namespace
- Number of facets and properties
- Click to expand and see details

### Source Properties Table

Shows all public properties of the source type with:
- Property name
- Type (with friendly names like `string`, `int?`, `List<T>`)
- Modifiers (Nullable, Required, Init, Collection, Nested Facet)

### Facet Cards

Each generated facet shows:
- Facet name and type kind (class, record, struct)
- Feature indicators (Constructor, Projection, ToSource)
- Excluded/Included properties
- Member count

### Search

Use the search bar to filter source types and facets by name.

## Example

Given these types in your application:

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public List<Order> Orders { get; set; }
}

[Facet(typeof(User), nameof(User.Password))]
public partial record UserDto;

[Facet(typeof(User), Include = new[] { nameof(User.Id), nameof(User.Name) })]
public partial record UserSummaryDto;
```

The dashboard will show:
- **User** source type with 5 properties
- Two facets: `UserDto` (excludes Password) and `UserSummaryDto` (includes only Id, Name)

## Requirements

- .NET 8.0 or later
- ASP.NET Core application

## Related Packages

- [Facet](https://www.nuget.org/packages/Facet) - Core source generator
- [Facet.Extensions](https://www.nuget.org/packages/Facet.Extensions) - Mapping extension methods
- [Facet.Extensions.EFCore](https://www.nuget.org/packages/Facet.Extensions.EFCore) - Entity Framework Core support
