using System;
using System.ComponentModel.DataAnnotations;

namespace Facet.TestConsole.GenerateDtosTests;

// Test model for the new GenerateDtos feature
[GenerateDtos(Types = DtoTypes.All, OutputType = OutputType.Record)]
public class TestUser
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public DateTime DateOfBirth { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Test model for auditable entity pattern
[GenerateAuditableDtos(Types = DtoTypes.Create | DtoTypes.Update | DtoTypes.Response, 
                       OutputType = OutputType.Record,
                       ExcludeProperties = new[] { "Password" })]
public class TestProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public string? Password { get; set; } // Should be excluded
    public DateTime CreatedAt { get; set; } // Should be excluded (auditable)
    public DateTime? UpdatedAt { get; set; } // Should be excluded (auditable)
    public string? CreatedBy { get; set; } // Should be excluded (auditable)
    public string? UpdatedBy { get; set; } // Should be excluded (auditable)
}

// Test model with custom namespace and naming
[GenerateDtos(Types = DtoTypes.Create | DtoTypes.Response,
              OutputType = OutputType.Class,
              Namespace = "Facet.TestConsole.CustomDtos",
              Prefix = "Custom",
              Suffix = "Dto")]
public class TestOrder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
}