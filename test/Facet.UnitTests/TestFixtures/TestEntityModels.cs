using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Facet.UnitTests.TestFixtures;

/// <summary>
/// Test entity models for unit tests. These match the TestConsole entities
/// but are isolated in the unit test project for better test control.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class Person : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public virtual string DisplayName => $"{FirstName} {LastName}";
}

public class Employee : Person
{
    public string EmployeeId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime HireDate { get; set; }
    
    public override string DisplayName => $"{FirstName} {LastName} ({EmployeeId})";
}

public class Manager : Employee
{
    public string TeamName { get; set; } = string.Empty;
    public int TeamSize { get; set; }
    public decimal Budget { get; set; }
    
    public override string DisplayName => $"Manager {FirstName} {LastName} - {TeamName}";
}

/// <summary>
/// Standard User entity for testing basic Facet generation.
/// Contains both public and sensitive data (Password should be excluded).
/// </summary>
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Password { get; set; } = string.Empty; // Should be excluded
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string? Bio { get; set; }
}

/// <summary>
/// Product entity for testing different facet kinds.
/// </summary>
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public string InternalNotes { get; set; } = string.Empty; // Should be excluded
    
    // Navigation property for testing
    public Category? Category { get; set; }
}

/// <summary>
/// Category entity for testing navigation properties.
/// </summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}

/// <summary>
/// Modern record with init-only and required properties for testing record features.
/// </summary>
public record ModernUser
{
    public required string Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? Bio { get; set; }
    public string? PasswordHash { get; init; } // Should be excluded
}

/// <summary>
/// Compact record struct for testing record struct features.
/// </summary>
public record struct CompactUser(string Id, string Name, DateTime CreatedAt);

// Test DTOs - these will be used to define what should be generated
[Facet(typeof(User), "Password", "CreatedAt")]
public partial class UserDto 
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

[Facet(typeof(Product), "InternalNotes", Kind = FacetKind.Record)]
public partial record ProductDto;

[Facet(typeof(Product), "InternalNotes", "CreatedAt", Kind = FacetKind.Struct)]
public partial struct ProductSummary;

[Facet(typeof(User), "Password", "CreatedAt", "LastLoginAt", Kind = FacetKind.RecordStruct)]
public partial record struct UserSummary;

[Facet(typeof(Employee), "Salary", "CreatedBy")]
public partial class EmployeeDto;

[Facet(typeof(Manager), "Salary", "Budget", "CreatedBy")]
public partial class ManagerDto;

[Facet(typeof(ModernUser), "PasswordHash", "Bio")]
public partial record ModernUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

[Facet(typeof(CompactUser))]
public partial record struct CompactUserDto;