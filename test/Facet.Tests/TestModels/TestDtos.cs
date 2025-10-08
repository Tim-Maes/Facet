using Facet.Tests.TestModels;
using Facet.Mapping;

namespace Facet.Tests.TestModels;

[Facet(typeof(User), "Password", "CreatedAt")]
public partial class UserDto 
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

[Facet(typeof(Product), "InternalNotes")]
public partial record ProductDto;

[Facet(typeof(Employee), "Password", "Salary", "CreatedAt")]
public partial class EmployeeDto;

[Facet(typeof(Manager), "Password", "Salary", "Budget", "CreatedAt")]
public partial class ManagerDto;

[Facet(typeof(ClassicUser))]
public partial record ClassicUserDto;

[Facet(typeof(ModernUser), "PasswordHash", "Bio")]
public partial record ModernUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

[Facet(typeof(UserWithEnum))]
public partial class UserWithEnumDto;

[Facet(typeof(User), "Password", "CreatedAt")]
public partial record struct UserSummary;

[Facet(typeof(Product), "InternalNotes", "CreatedAt")]
public partial struct ProductSummary;

[Facet(typeof(EventLog), "Source")]
public partial class EventLogDto;

// Include functionality test DTOs
[Facet(typeof(User), Include = new[] { "FirstName", "LastName", "Email" })]
public partial class UserIncludeDto;

[Facet(typeof(User), Include = new[] { "FirstName" })]
public partial class UserSingleIncludeDto;

[Facet(typeof(Product), Include = new[] { "Name", "Price" })]
public partial class ProductIncludeDto;

[Facet(typeof(Employee), Include = new[] { "FirstName", "LastName", "Department" })]
public partial class EmployeeIncludeDto;

[Facet(typeof(User), Include = new[] { "FirstName", "LastName" })]
public partial class UserIncludeWithCustomDto
{
    public string FullName { get; set; } = string.Empty;
}

[Facet(typeof(ModernUser), Include = new[] { "FirstName", "LastName" })]
public partial record ModernUserIncludeDto;

[Facet(typeof(EntityWithFields), Include = new[] { "Name", "Age" }, IncludeFields = true)]
public partial class EntityWithFieldsIncludeDto;

[Facet(typeof(EntityWithFields), Include = new[] { "Email", "Name", "Age" }, IncludeFields = false)]
public partial class EntityWithFieldsIncludeNoFieldsDto;

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

[Facet(typeof(User), "Password", "CreatedAt", Configuration = typeof(UserDtoWithMappingMapper))]
public partial class UserDtoWithMapping 
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

// Async mapping test classes - using existing UserDto
public class UserDtoAsyncMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        // Simulate async work
        await Task.Delay(10, cancellationToken);
        
        // Set the custom properties that UserDto has
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

[Facet(typeof(User), "Password", "CreatedAt")]
public partial class UserAsyncDto 
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string ProfileData { get; set; } = string.Empty;
}

public class ProductDtoAsyncMapper : IFacetMapConfigurationAsync<Product, ProductDto>
{
    public static async Task MapAsync(Product source, ProductDto target, CancellationToken cancellationToken = default)
    {
        await Task.Delay(5, cancellationToken);
        
        // ProductDto has different properties - let's set what it actually has
        // For this simple test, we'll just ensure the basic properties are copied by the constructor
        // and we can add any additional logic here if needed
    }
}

[Facet(typeof(Product), "InternalNotes")]
public partial class ProductAsyncDto 
{
    public string DisplayName { get; set; } = string.Empty;
    public string FormattedPrice { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
}

public class UserDtoHybridMapper : IFacetMapConfigurationHybrid<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        // Sync mapping
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.Age = CalculateAge(source.DateOfBirth);
    }

    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        // Async mapping - for this simple test, just add some delay
        await Task.Delay(8, cancellationToken);
        // UserDto doesn't have AsyncComputedField, so we'll just modify existing properties
        target.FullName += " (Hybrid)";
    }

    private static int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }
}

[Facet(typeof(User), "Password", "CreatedAt")]
public partial class UserHybridDto 
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string AsyncComputedField { get; set; } = string.Empty;
}

[Facet(typeof(NullableTestEntity))]
public partial class NullableTestDto
{
}

// NullableProperties functionality test DTOs
[Facet(typeof(Product), "InternalNotes", "CreatedAt", NullableProperties = true, GenerateBackTo = false)]
public partial class ProductQueryDto;

[Facet(typeof(User), "Password", "CreatedAt", NullableProperties = true, GenerateBackTo = false)]
public partial record UserQueryDto;

[Facet(typeof(UserWithEnum), NullableProperties = true, GenerateBackTo = false)]
public partial class UserWithEnumQueryDto;