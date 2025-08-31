using System;
using Facet;

namespace Facet.TestConsole.Tests
{
    // Basic entity for testing all DTO types
    [GenerateDtos]
    public class CustomerEntity
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string UpdatedBy { get; set; } = string.Empty;
    }

    // Test custom configuration
    [GenerateDtos(
        Types = DtoTypes.Create | DtoTypes.Response,
        OutputType = FacetKind.Class,
        ExcludeProperties = new[] { "Description", "SKU" }
    )]
    public class ProductEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int CategoryId { get; set; }
        public string SKU { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Test auditable entity with automatic audit field exclusion
    [GenerateAuditableDtos]
    public class OrderEntity
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string UpdatedBy { get; set; } = string.Empty;
    }

    // Test namespace targeting
    [GenerateDtos(
        Types = DtoTypes.Create | DtoTypes.Response,
        Namespace = "Facet.TestConsole.DTOs"
    )]
    public class InvoiceEntity
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public bool IsPaid { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Test custom naming convention
    [GenerateDtos(
        Types = DtoTypes.Response,
        NamingConvention = DtoNamingConvention.Custom,
        CustomPrefix = "Api",
        CustomSuffix = "Model"
    )]
    public class PaymentEntity
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
        public bool IsSuccessful { get; set; }
    }

    // Test different output types
    [GenerateDtos(
        Types = DtoTypes.Create | DtoTypes.Response,
        OutputType = FacetKind.RecordStruct
    )]
    public class NotificationEntity
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Test specific DTO types only
    [GenerateDtos(
        Types = DtoTypes.Update | DtoTypes.Response
    )]
    public class CategoryEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Test complex entity with custom exclusions
    [GenerateDtos(
        ExcludeProperties = new[] { "Salary" } // Sensitive field excluded from Create/Update
    )]
    public class EmployeeEntity
    {
        public int Id { get; set; }
        public string EmployeeNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public decimal Salary { get; set; } // Sensitive - excluded
        public DateTime HireDate { get; set; }
        public int? ManagerId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Test modern record types with init-only properties
    [GenerateDtos(
        OutputType = FacetKind.Record,
        PreserveInitOnlyProperties = true,
        PreserveRequiredProperties = true
    )]
    public record UserProfileEntity
    {
        public required int Id { get; init; }
        public required string Username { get; init; }
        public required string Email { get; init; }
        public string? DisplayName { get; set; }
        public string? Bio { get; set; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
    }

    // Test with fields inclusion
    [GenerateDtos(
        Types = DtoTypes.Response,
        IncludeFields = true
    )]
    public class LegacyEntity
    {
        public int Id;
        public string Name = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Test simple custom naming
    [GenerateDtos(
        Types = DtoTypes.Response,
        NamingConvention = DtoNamingConvention.Custom,
        CustomPrefix = "Test",
        CustomSuffix = "Dto"
    )]
    public class SimpleEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}