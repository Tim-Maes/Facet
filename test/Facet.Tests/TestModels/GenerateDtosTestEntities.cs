using Facet;

namespace Facet.Tests.TestModels;

/// <summary>
/// Test entities for GenerateDtos functionality with all DTO types
/// </summary>
[GenerateDtos(Types = DtoTypes.All, ExcludeProperties = new[] { "Password" })]
public class TestUser
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // This will be excluded
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Test entity with specific DTO types and record output
/// </summary>
[GenerateDtos(
    Types = DtoTypes.Response | DtoTypes.Create, 
    OutputType = OutputType.Record,
    ExcludeProperties = new[] { "InternalNotes" })]
public class TestProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string InternalNotes { get; set; } = string.Empty; // Excluded
}

/// <summary>
/// Test entity with custom namespace and prefix/suffix
/// </summary>
[GenerateDtos(
    Types = DtoTypes.Response,
    OutputType = OutputType.Class,
    Namespace = "Facet.Tests.TestModels.Dtos",
    Prefix = "Api",
    Suffix = "Model")]
public class TestOrder
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime OrderDate { get; set; }
    public bool IsPaid { get; set; }
}

/// <summary>
/// Test entity for testing regular class DTOs
/// </summary>
[GenerateDtos(Types = DtoTypes.Response, OutputType = OutputType.Class)]
public class TestEventLog
{
    public string Id { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Message { get; set; }
    public string? UserId { get; set; }
}

/// <summary>
/// Test entity for multiple GenerateDtos attributes
/// </summary>
[GenerateDtos(Types = DtoTypes.Response, ExcludeProperties = new[] { "Password", "InternalNotes" })]
[GenerateDtos(Types = DtoTypes.Update, ExcludeProperties = new[] { "Password" })]
public class TestAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // Excluded from both
    public bool IsActive { get; set; }
    public string InternalNotes { get; set; } = string.Empty; // Only excluded from Response
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Simple entity for basic ToFacet demonstration
/// </summary>
[GenerateDtos(Types = DtoTypes.Response, ExcludeProperties = new[] { "Password" })]
public class SimpleUser
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Entity with fields for testing field inclusion
/// </summary>
[GenerateDtos(Types = DtoTypes.Response, IncludeFields = true, ExcludeProperties = new[] { "Secret" })]
public class TestEntityWithFields
{
    public int Id;
    public string Name = string.Empty;
    public int Age;
    public string Email { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty; // Excluded
}

/// <summary>
/// Entity for testing record struct output type
/// </summary>
[GenerateDtos(Types = DtoTypes.Response, OutputType = OutputType.RecordStruct)]
public class TestValueObject
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Label { get; set; } = string.Empty;
}