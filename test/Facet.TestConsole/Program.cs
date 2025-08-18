using System;
using System.Collections.Generic;
using System.Linq;
using Facet;
using Facet.Extensions;
using Facet.Mapping;

namespace Facet.TestConsole;

// Base classes to test inheritance
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
    
    // Override the base property
    public override string DisplayName => $"{FirstName} {LastName} ({EmployeeId})";
}

public class Manager : Employee
{
    public string TeamName { get; set; } = string.Empty;
    public int TeamSize { get; set; }
    public decimal Budget { get; set; }
    
    // Override again
    public override string DisplayName => $"Manager {FirstName} {LastName} - {TeamName}";
}

// Sample domain models
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Password { get; set; } = string.Empty; // This will be excluded in DTOs
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
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
    public string InternalNotes { get; set; } = string.Empty; // This will be excluded
}

// Custom mapping configuration
public class UserDtoWithMappingMapper : IFacetMapConfiguration<User, UserDtoWithMapping>
{
    public static void Map(User source, UserDtoWithMapping target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.Age = CalculateAge(source.DateOfBirth);
    }

    private static int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }
}

// Inheritance test DTOs
[Facet(typeof(Employee), "Salary", "CreatedBy")]
public partial class EmployeeDto;

[Facet(typeof(Manager), "Salary", "Budget", "CreatedBy")]
public partial class ManagerDto;

// Basic DTO - excludes sensitive information like Password
[Facet(typeof(User), "Password", "CreatedAt")]
public partial class UserDto 
{
    // Custom properties that will be set by the mapper
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

// DTO with custom mapping
[Facet(typeof(User), "Password", "CreatedAt", Configuration = typeof(UserDtoWithMappingMapper))]
public partial class UserDtoWithMapping 
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

// Record DTO
[Facet(typeof(Product), "InternalNotes", Kind = FacetKind.Record)]
public partial record ProductDto;

// Struct DTO
[Facet(typeof(Product), "InternalNotes", "CreatedAt", Kind = FacetKind.Struct)]
public partial struct ProductSummary;

// Record struct DTO
[Facet(typeof(User), "Password", "CreatedAt", "LastLoginAt", Kind = FacetKind.RecordStruct)]
public partial record struct UserSummary;

// Modern record types with init-only and required properties
public record ModernUser
{
    public required string Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? Bio { get; set; }
    public string? PasswordHash { get; init; } // Sensitive field to exclude
}

public record struct CompactUser(string Id, string Name, DateTime CreatedAt);

// Test auto-detection and modern record features - no custom mapping for now
[Facet(typeof(ModernUser), "PasswordHash", "Bio")]
public partial record ModernUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

// Test auto-detection of record struct - preserves modifiers automatically!
[Facet(typeof(CompactUser))]
public partial record struct CompactUserDto;

// Simplify the class test to avoid issues with required init-only properties
[Facet(typeof(ModernUser), "PasswordHash", "Bio", "Email")]
public partial class ModernUserClass
{
    public string? AdditionalInfo { get; set; }
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Facet Generator Test Console ===\n");

        // Create sample data
        var users = CreateSampleUsers();
        var products = CreateSampleProducts();
        var employees = CreateSampleEmployees();
        var managers = CreateSampleManagers();
        var modernUsers = CreateSampleModernUsers();

        // Test inheritance support
        TestInheritanceSupport(employees, managers);

        // Test modern record features
        TestModernRecordFeatures(modernUsers);

        // Test basic DTO mapping
        TestBasicDtoMapping(users);

        // Test DTO with custom mapping
        TestCustomMappingDto(users);

        // Test different facet kinds
        TestDifferentFacetKinds(users, products);

        // Test LINQ projections
        TestLinqProjections(users, products);

        Console.WriteLine("\n=== All tests completed successfully! ===");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static List<ModernUser> CreateSampleModernUsers()
    {
        return new List<ModernUser>
        {
            new ModernUser
            {
                Id = "user_001",
                FirstName = "Alice",
                LastName = "Cooper",
                Email = "alice.cooper@example.com",
                Bio = "Software Engineer passionate about clean code",
                PasswordHash = "hashed_password_123"
            },
            new ModernUser
            {
                Id = "user_002", 
                FirstName = "Bob",
                LastName = "Dylan",
                Email = "bob.dylan@example.com",
                Bio = null,
                PasswordHash = "hashed_password_456"
            }
        };
    }

    static void TestInheritanceSupport(List<Employee> employees, List<Manager> managers)
    {
        Console.WriteLine("1. Testing Inheritance Support:");
        Console.WriteLine("=============================== cont'd");

        Console.WriteLine("Employee DTOs (inherits from Person -> BaseEntity):");
        foreach (var employee in employees)
        {
            var employeeDto = employee.ToFacet<Employee, EmployeeDto>();
            Console.WriteLine($"  {employeeDto.DisplayName}");
            Console.WriteLine($"    ID: {employeeDto.Id}, Employee ID: {employeeDto.EmployeeId}");
            Console.WriteLine($"    Department: {employeeDto.Department}, Hire Date: {employeeDto.HireDate:yyyy-MM-dd}");
            Console.WriteLine($"    Created: {employeeDto.CreatedAt:yyyy-MM-dd}, Updated: {employeeDto.UpdatedAt:yyyy-MM-dd}");
            Console.WriteLine();
        }

        Console.WriteLine("Manager DTOs (inherits from Employee -> Person -> BaseEntity):");
        foreach (var manager in managers)
        {
            var managerDto = manager.ToFacet<Manager, ManagerDto>();
            Console.WriteLine($"  {managerDto.DisplayName}");
            Console.WriteLine($"    ID: {managerDto.Id}, Employee ID: {managerDto.EmployeeId}");
            Console.WriteLine($"    Department: {managerDto.Department}, Team: {managerDto.TeamName} ({managerDto.TeamSize} members)");
            Console.WriteLine($"    Hire Date: {managerDto.HireDate:yyyy-MM-dd}");
            Console.WriteLine($"    Created: {managerDto.CreatedAt:yyyy-MM-dd}, Updated: {managerDto.UpdatedAt:yyyy-MM-dd}");
            Console.WriteLine();
        }
    }

    static List<User> CreateSampleUsers()
    {
        return new List<User>
        {
            new User
            {
                Id = 1,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                DateOfBirth = new DateTime(1990, 5, 15),
                Password = "secret123",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-30),
                LastLoginAt = DateTime.Now.AddHours(-2)
            },
            new User
            {
                Id = 2,
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane.smith@example.com",
                DateOfBirth = new DateTime(1985, 12, 3),
                Password = "password456",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-60),
                LastLoginAt = DateTime.Now.AddDays(-1)
            },
            new User
            {
                Id = 3,
                FirstName = "Bob",
                LastName = "Johnson",
                Email = "bob.johnson@example.com",
                DateOfBirth = new DateTime(1992, 8, 22),
                Password = "mypassword",
                IsActive = false,
                CreatedAt = DateTime.Now.AddDays(-90),
                LastLoginAt = null
            }
        };
    }

    static List<Product> CreateSampleProducts()
    {
        return new List<Product>
        {
            new Product
            {
                Id = 1,
                Name = "Laptop",
                Description = "High-performance laptop for professionals",
                Price = 1299.99m,
                CategoryId = 1,
                IsAvailable = true,
                CreatedAt = DateTime.Now.AddDays(-20),
                InternalNotes = "Supplier: TechCorp, Margin: 25%"
            },
            new Product
            {
                Id = 2,
                Name = "Smartphone",
                Description = "Latest smartphone with advanced features",
                Price = 899.99m,
                CategoryId = 2,
                IsAvailable = true,
                CreatedAt = DateTime.Now.AddDays(-15),
                InternalNotes = "Supplier: MobileTech, Margin: 30%"
            },
            new Product
            {
                Id = 3,
                Name = "Tablet",
                Description = "Lightweight tablet for entertainment",
                Price = 449.99m,
                CategoryId = 2,
                IsAvailable = false,
                CreatedAt = DateTime.Now.AddDays(-10),
                InternalNotes = "Supplier: TabletInc, Margin: 20%"
            }
        };
    }

    static List<Employee> CreateSampleEmployees()
    {
        return new List<Employee>
        {
            new Employee
            {
                Id = 1,
                FirstName = "Alice",
                LastName = "Johnson",
                EmployeeId = "EMP001",
                Department = "Engineering",
                Salary = 85000m,
                HireDate = new DateTime(2020, 3, 15),
                CreatedAt = DateTime.Now.AddDays(-365),
                UpdatedAt = DateTime.Now.AddDays(-10),
                CreatedBy = "HR System"
            },
            new Employee
            {
                Id = 2,
                FirstName = "Bob",
                LastName = "Wilson",
                EmployeeId = "EMP002",
                Department = "Marketing",
                Salary = 72000m,
                HireDate = new DateTime(2019, 8, 22),
                CreatedAt = DateTime.Now.AddDays(-400),
                UpdatedAt = DateTime.Now.AddDays(-5),
                CreatedBy = "HR System"
            }
        };
    }

    static List<Manager> CreateSampleManagers()
    {
        return new List<Manager>
        {
            new Manager
            {
                Id = 3,
                FirstName = "Carol",
                LastName = "Davis",
                EmployeeId = "MGR001",
                Department = "Engineering",
                Salary = 120000m,
                HireDate = new DateTime(2018, 1, 10),
                TeamName = "Backend Team",
                TeamSize = 8,
                Budget = 500000m,
                CreatedAt = DateTime.Now.AddDays(-500),
                UpdatedAt = DateTime.Now.AddDays(-2),
                CreatedBy = "HR System"
            },
            new Manager
            {
                Id = 4,
                FirstName = "David",
                LastName = "Brown",
                EmployeeId = "MGR002",
                Department = "Sales",
                Salary = 110000m,
                HireDate = new DateTime(2017, 6, 5),
                TeamName = "Regional Sales",
                TeamSize = 12,
                Budget = 750000m,
                CreatedAt = DateTime.Now.AddDays(-600),
                UpdatedAt = DateTime.Now.AddDays(-1),
                CreatedBy = "HR System"
            }
        };
    }

    static void TestBasicDtoMapping(List<User> users)
    {
        Console.WriteLine("2. Testing Basic DTO Mapping:");
        Console.WriteLine("==============================");

        foreach (var user in users)
        {
            var userDto = user.ToFacet<User, UserDto>();
            Console.WriteLine($"User: {userDto.FirstName} {userDto.LastName} ({userDto.Email})");
            Console.WriteLine($"  Active: {userDto.IsActive}, DOB: {userDto.DateOfBirth:yyyy-MM-dd}");
            Console.WriteLine($"  Last Login: {userDto.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never"}");
            Console.WriteLine();
        }
    }

    static void TestCustomMappingDto(List<User> users)
    {
        Console.WriteLine("3. Testing DTO with Custom Mapping:");
        Console.WriteLine("====================================");

        foreach (var user in users)
        {
            var userDto = user.ToFacet<User, UserDtoWithMapping>();
            Console.WriteLine($"User: {userDto.FullName} (Age: {userDto.Age})");
            Console.WriteLine($"  Email: {userDto.Email}, Active: {userDto.IsActive}");
            Console.WriteLine();
        }
    }

    static void TestDifferentFacetKinds(List<User> users, List<Product> products)
    {
        Console.WriteLine("4. Testing Different Facet Kinds:");
        Console.WriteLine("==================================");

        // Record DTO
        Console.WriteLine("Record DTOs:");
        foreach (var product in products)
        {
            var productDto = product.ToFacet<Product, ProductDto>();
            Console.WriteLine($"  {productDto.Name}: ${productDto.Price} (Available: {productDto.IsAvailable})");
        }
        Console.WriteLine();

        // Struct DTO - need to use constructor directly since ToFacet requires reference types
        Console.WriteLine("Struct DTOs:");
        foreach (var product in products)
        {
            var productSummary = new ProductSummary(product);
            Console.WriteLine($"  {productSummary.Name}: ${productSummary.Price}");
        }
        Console.WriteLine();

        // Record Struct DTO - need to use constructor directly since ToFacet requires reference types
        Console.WriteLine("Record Struct DTOs:");
        foreach (var user in users)
        {
            var userSummary = new UserSummary(user);
            Console.WriteLine($"  {userSummary.FirstName} {userSummary.LastName} ({userSummary.Email})");
        }
        Console.WriteLine();
    }

    static void TestLinqProjections(List<User> users, List<Product> products)
    {
        Console.WriteLine("5. Testing LINQ Projections:");
        Console.WriteLine("=============================");

        // Test enumerable projections
        Console.WriteLine("Active users (via SelectFacets):");
        var activeUserDtos = users
            .Where(u => u.IsActive)
            .SelectFacets<User, UserDtoWithMapping>()
            .ToList();

        foreach (var dto in activeUserDtos)
        {
            Console.WriteLine($"  {dto.FullName} (Age: {dto.Age}) - {dto.Email}");
        }
        Console.WriteLine();

        // Test queryable projections (simulated)
        Console.WriteLine("Available products (via SelectFacet):");
        var availableProducts = products
            .AsQueryable()
            .Where(p => p.IsAvailable)
            .SelectFacet<Product, ProductDto>()
            .ToList();

        foreach (var dto in availableProducts)
        {
            Console.WriteLine($"  {dto.Name}: ${dto.Price} - {dto.Description}");
        }
        Console.WriteLine();
    }

    static void TestModernRecordFeatures(List<ModernUser> modernUsers)
    {
        Console.WriteLine("1.5 Testing Modern Record Features:");
        Console.WriteLine("===================================");

        Console.WriteLine("Modern User DTOs (with auto-detected record, init-only & required properties):");
        foreach (var user in modernUsers)
        {
            try
            {
                var userDto = user.ToFacet<ModernUser, ModernUserDto>();
                Console.WriteLine($"  {userDto.FirstName} {userDto.LastName}");
                Console.WriteLine($"    ID: {userDto.Id} (required init-only)");
                Console.WriteLine($"    Email: {userDto.Email ?? "N/A"}");
                Console.WriteLine($"    Created: {userDto.CreatedAt:yyyy-MM-dd}");
                Console.WriteLine($"    Full Name: {userDto.FullName}");
                Console.WriteLine($"    Display: {userDto.DisplayName}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error mapping {user.FirstName} {user.LastName}: {ex.Message}");
                Console.WriteLine();
            }
        }

        Console.WriteLine("Compact User DTOs (auto-detected record struct - using constructor):");
        var compactUsers = modernUsers.Select(u => new CompactUser(u.Id, $"{u.FirstName} {u.LastName}", u.CreatedAt)).ToList();
        foreach (var compact in compactUsers)
        {
            try
            {
                // Use constructor directly for record structs since ToFacet requires reference types
                var compactDto = new CompactUserDto(compact);
                Console.WriteLine($"  {compactDto.Name} (ID: {compactDto.Id}, Created: {compactDto.CreatedAt:yyyy-MM-dd})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error mapping compact user: {ex.Message}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("Modern User Classes (mutable by default for classes):");
        foreach (var user in modernUsers)
        {
            try
            {
                var userClass = user.ToFacet<ModernUser, ModernUserClass>();
                Console.WriteLine($"  {userClass.FirstName} {userClass.LastName}");
                Console.WriteLine($"    ID: {userClass.Id} (mutable in class)");
                Console.WriteLine($"    Created: {userClass.CreatedAt:yyyy-MM-dd}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error mapping {user.FirstName} {user.LastName} to class: {ex.Message}");
                Console.WriteLine();
            }
        }
    }
}