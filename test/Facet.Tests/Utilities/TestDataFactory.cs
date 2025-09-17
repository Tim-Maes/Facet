using Facet.Tests.TestModels;

namespace Facet.Tests.Utilities;

public static class TestDataFactory
{
    public static User CreateUser(
        string firstName = "John",
        string lastName = "Doe",
        string email = "john.doe@example.com",
        DateTime? dateOfBirth = null,
        bool isActive = true)
    {
        return new User
        {
            Id = Random.Shared.Next(1, 1000),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            DateOfBirth = dateOfBirth ?? new DateTime(1990, 5, 15),
            Password = "hashed_password_123",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            LastLoginAt = DateTime.UtcNow.AddHours(-2)
        };
    }

    public static List<User> CreateUsers(int count = 3)
    {
        return new List<User>
        {
            CreateUser("Alice", "Johnson", "alice.johnson@example.com", new DateTime(1985, 3, 22), true),
            CreateUser("Bob", "Smith", "bob.smith@example.com", new DateTime(1992, 8, 10), true),
            CreateUser("Charlie", "Brown", "charlie.brown@example.com", new DateTime(1988, 12, 5), false)
        };
    }

    public static Product CreateProduct(
        string name = "Test Product",
        decimal price = 29.99m,
        bool isAvailable = true)
    {
        return new Product
        {
            Id = Random.Shared.Next(1, 1000),
            Name = name,
            Description = $"Description for {name}",
            Price = price,
            CategoryId = 1,
            IsAvailable = isAvailable,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            InternalNotes = "Internal notes - should be excluded"
        };
    }

    public static Employee CreateEmployee(
        string firstName = "Jane",
        string lastName = "Employee",
        string department = "Engineering")
    {
        return new Employee
        {
            Id = Random.Shared.Next(1, 1000),
            FirstName = firstName,
            LastName = lastName,
            Email = $"{firstName.ToLower()}.{lastName.ToLower()}@company.com",
            DateOfBirth = new DateTime(1985, 6, 20),
            Password = "hashed_password_456",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-60),
            LastLoginAt = DateTime.UtcNow.AddHours(-1),
            EmployeeId = $"EMP{Random.Shared.Next(1000, 9999)}",
            Department = department,
            Salary = 75000m,
            HireDate = DateTime.UtcNow.AddDays(-365)
        };
    }

    public static Manager CreateManager(
        string firstName = "Mike",
        string lastName = "Manager",
        string teamName = "Development Team")
    {
        return new Manager
        {
            Id = Random.Shared.Next(1, 1000),
            FirstName = firstName,
            LastName = lastName,
            Email = $"{firstName.ToLower()}.{lastName.ToLower()}@company.com",
            DateOfBirth = new DateTime(1980, 4, 15),
            Password = "hashed_password_789",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-90),
            LastLoginAt = DateTime.UtcNow.AddMinutes(-30),
            EmployeeId = $"MGR{Random.Shared.Next(1000, 9999)}",
            Department = "Engineering",
            Salary = 95000m,
            HireDate = DateTime.UtcNow.AddDays(-730),
            TeamName = teamName,
            TeamSize = 8,
            Budget = 500000m
        };
    }

    public static ModernUser CreateModernUser(
        string firstName = "Modern",
        string lastName = "User")
    {
        return new ModernUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = firstName,
            LastName = lastName,
            Email = $"{firstName.ToLower()}.{lastName.ToLower()}@modern.com",
            CreatedAt = DateTime.UtcNow,
            Bio = "This is a bio that should be excluded",
            PasswordHash = "hashed_password_modern"
        };
    }
    
    public static ClassicUser CreateClassicUser(
        string firstName = "Classic",
        string lastName = "User")
    {
        return new ClassicUser(Guid.NewGuid().ToString(), firstName, lastName, $"{firstName.ToLower()}.{lastName.ToLower()}@modern.com");
    }

    public static UserWithEnum CreateUserWithEnum(
        string name = "Test User",
        UserStatus status = UserStatus.Active)
    {
        return new UserWithEnum
        {
            Id = Random.Shared.Next(1, 1000),
            Name = name,
            Status = status,
            Email = $"{name.Replace(" ", ".").ToLower()}@example.com"
        };
    }
}