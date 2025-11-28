namespace Facet.Tests.TestModels;

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string AddedProperty { get; set; } = string.Empty;
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public string InternalNotes { get; set; } = string.Empty;
}

public class Employee : User
{
    public string EmployeeId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime HireDate { get; set; }
}

public class Manager : Employee
{
    public string TeamName { get; set; } = string.Empty;
    public int TeamSize { get; set; }
    public decimal Budget { get; set; }
}

public record ClassicUser(string Id, string FirstName, string LastName, string? Email);

public record ModernUser
{
    public required string Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? Bio { get; set; }
    public string? PasswordHash { get; init; }
}

public record EventLog
{
    public required string Id { get; init; }
    public required string EventType { get; init; }
    public required DateTime Timestamp { get; init; }
    public string? Message { get; init; }
    public string? UserId { get; init; }
    public required string Source { get; init; }
}

public enum UserStatus
{
    Active,
    Inactive,
    Pending,
    Suspended
}

public class UserWithEnum
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public UserStatus Status { get; set; }
    public string Email { get; set; } = string.Empty;
}

public sealed class NullableTestEntity
{
    public bool Test1 { get; set; }
    public bool? Test2 { get; set; }
    public string Test3 { get; set; } = string.Empty;
    public string? Test4 { get; set; } = null;
}

// Test entity with fields for include functionality testing
public class EntityWithFields
{
    public int Id;
    public string Name = string.Empty;
    public int Age;
    public string Email { get; set; } = string.Empty;
}

public record Dummy(string Name, int Age);

public class UserForNestedFacet
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public UserAddressForNestedFacet Address { get; set; } = new();
}

public class UserAddressForNestedFacet
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string FormattedAddress => $"{Street}, {City}";
}

// Test entities for inherited property exclusion
public abstract class BaseEntity<TPkKey>
{
    public TPkKey Id { get; set; } = default!;
}

public class Category : BaseEntity<uint>
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}
