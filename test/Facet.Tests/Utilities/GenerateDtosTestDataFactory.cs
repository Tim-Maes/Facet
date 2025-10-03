using Facet.Tests.TestModels;

namespace Facet.Tests.Utilities;

/// <summary>
/// Extended test data factory for GenerateDtos test entities
/// </summary>
public static class GenerateDtosTestDataFactory
{
    public static TestUser CreateTestUser(
        string firstName = "Test",
        string lastName = "User",
        string email = "test@example.com",
        bool isActive = true)
    {
        return new TestUser
        {
            Id = Random.Shared.Next(1, 1000),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Password = "test_password_123", // Will be excluded from DTOs
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
    }

    public static TestProduct CreateTestProduct(
        string name = "Test Product",
        decimal price = 19.99m,
        bool isAvailable = true)
    {
        return new TestProduct
        {
            Id = Random.Shared.Next(1, 1000),
            Name = name,
            Description = $"Description for {name}",
            Price = price,
            IsAvailable = isAvailable,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            InternalNotes = "Internal notes - should be excluded from DTOs"
        };
    }

    public static TestOrder CreateTestOrder(
        string orderNumber = "ORD-001",
        decimal amount = 99.99m,
        bool isPaid = false)
    {
        return new TestOrder
        {
            Id = Random.Shared.Next(1, 1000),
            OrderNumber = orderNumber,
            Amount = amount,
            OrderDate = DateTime.UtcNow.AddDays(-1),
            IsPaid = isPaid
        };
    }

    public static TestEventLog CreateTestEventLog(
        string eventType = "UserLogin",
        string? message = null)
    {
        return new TestEventLog
        {
            Id = Guid.NewGuid().ToString(),
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            Message = message ?? $"Test event: {eventType}",
            UserId = Guid.NewGuid().ToString()
        };
    }

    public static TestAccount CreateTestAccount(
        string username = "testuser",
        string email = "test@account.com",
        bool isActive = true)
    {
        return new TestAccount
        {
            Id = Random.Shared.Next(1, 1000),
            Username = username,
            Email = email,
            Password = "account_password_456", // Will be excluded from DTOs
            IsActive = isActive,
            InternalNotes = "Internal account notes", // Excluded from Response DTO only
            CreatedAt = DateTime.UtcNow.AddDays(-60)
        };
    }

    public static SimpleUser CreateSimpleUser(
        string firstName = "Simple",
        string lastName = "User",
        bool isActive = true)
    {
        return new SimpleUser
        {
            Id = Random.Shared.Next(1, 1000),
            FirstName = firstName,
            LastName = lastName,
            Email = $"{firstName.ToLower()}.{lastName.ToLower()}@simple.com",
            Password = "simple_password", // Will be excluded from DTOs
            IsActive = isActive
        };
    }

    public static TestEntityWithFields CreateTestEntityWithFields(
        string name = "Field Entity",
        int age = 30)
    {
        return new TestEntityWithFields
        {
            Id = Random.Shared.Next(1, 1000),
            Name = name,
            Age = age,
            Email = $"{name.Replace(" ", ".").ToLower()}@fields.com",
            Secret = "Top secret information" // Will be excluded from DTOs
        };
    }

    public static TestValueObject CreateTestValueObject(
        int x = 10,
        int y = 20,
        string label = "Test Point")
    {
        return new TestValueObject
        {
            X = x,
            Y = y,
            Label = label
        };
    }

    /// <summary>
    /// Creates multiple test users for collection testing
    /// </summary>
    public static List<TestUser> CreateTestUsers(int count = 3)
    {
        var users = new List<TestUser>();
        
        for (int i = 1; i <= count; i++)
        {
            users.Add(new TestUser
            {
                Id = i,
                FirstName = $"User{i}",
                LastName = $"LastName{i}",
                Email = $"user{i}@test.com",
                Password = $"password{i}",
                IsActive = i % 2 == 1, // Alternate active/inactive
                CreatedAt = DateTime.UtcNow.AddDays(-i),
                UpdatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }
        
        return users;
    }

    /// <summary>
    /// Creates multiple test products for collection testing
    /// </summary>
    public static List<TestProduct> CreateTestProducts(int count = 3)
    {
        var products = new List<TestProduct>();
        
        for (int i = 1; i <= count; i++)
        {
            products.Add(new TestProduct
            {
                Id = i,
                Name = $"Product {i}",
                Description = $"Description for Product {i}",
                Price = 10.00m * i,
                IsAvailable = i % 2 == 1, // Alternate available/unavailable
                CreatedAt = DateTime.UtcNow.AddDays(-i),
                InternalNotes = $"Internal notes for Product {i}"
            });
        }
        
        return products;
    }
}