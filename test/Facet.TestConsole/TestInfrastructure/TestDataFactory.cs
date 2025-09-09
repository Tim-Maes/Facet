using System;
using System.Collections.Generic;

namespace Facet.TestConsole.TestInfrastructure;

public static class TestDataFactory
{
    public static List<User> CreateSampleUsers()
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

    public static List<Product> CreateSampleProducts()
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

    public static List<Employee> CreateSampleEmployees()
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

    public static List<Manager> CreateSampleManagers()
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

    public static List<ModernUser> CreateSampleModernUsers()
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
}