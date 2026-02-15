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

public record Tenant
{
    public Guid Id { get; set; }
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

// Test entity with non-nullable reference type properties with initializers (GitHub issue)
public class UserModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public UserSettings Settings { get; set; } = new();
}

public class UserSettings
{
    public bool NotificationsEnabled { get; set; } = true;
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "en";
}

// Facet for testing property initializer preservation
[Facet(typeof(UserModel))]
public partial class UserModelDto;

// Test with init only properties that have initializers
public class InitOnlyWithInitializers
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

[Facet(typeof(InitOnlyWithInitializers))]
public partial class InitOnlyWithInitializersDto;

// Test entity for GitHub issue #251 - Nullable issues with object properties
// When a model has a single reference type property like List<string>,
// the generated record positional constructor can become ambiguous with
// the compiler-generated copy constructor
public class ModelWithListProperty
{
    public List<string> Tags { get; set; } = [];
}

// These tests verify that the ambiguity is resolved by using typed default values
// in the generated parameterless constructor

[Facet(typeof(ModelWithListProperty))]
public partial record RecordWithListDefault;

[Facet(typeof(ModelWithListProperty), GenerateParameterlessConstructor = false)]
public partial record RecordWithListNoParameterless;

[Facet(typeof(ModelWithListProperty), GenerateProjection = false)]
public partial record RecordWithListNoProjection;

// Test with multiple properties to ensure the fix doesn't break normal cases
public class ModelWithMultipleProperties
{
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public int Count { get; set; }
}

[Facet(typeof(ModelWithMultipleProperties))]
public partial record RecordWithMultipleProperties;

// Test with nullable reference type property
public class ModelWithNullableList
{
    public List<string>? Tags { get; set; }
}

[Facet(typeof(ModelWithNullableList))]
public partial record RecordWithNullableList;

// When user has initialization logic in their parameterless constructor,
// the generated constructor should chain to it
public class ModelTypeForChaining
{
    public int MaxValue { get; set; }
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(ModelTypeForChaining), GenerateParameterlessConstructor = false, ChainToParameterlessConstructor = true)]
public partial class ChainedConstructorDto
{
    public int Value { get; set; }
    public bool Initialized { get; set; }

    public ChainedConstructorDto()
    {
        // Custom initialization logic that should run when mapping
        Value = 100;
        Initialized = true;
    }
}

// Test without chaining (default behavior) for comparison
[Facet(typeof(ModelTypeForChaining), GenerateParameterlessConstructor = false)]
public partial class NonChainedConstructorDto
{
    public int Value { get; set; }
    public bool Initialized { get; set; }

    public NonChainedConstructorDto()
    {
        Value = 100;
        Initialized = true;
    }
}

// Test chaining with generated parameterless constructor disabled but still using the user's
[Facet(typeof(ModelTypeForChaining), GenerateParameterlessConstructor = false, ChainToParameterlessConstructor = true, MaxDepth = 0, PreserveReferences = false)]
public partial class ChainedConstructorNoDepthDto
{
    public int Value { get; set; }
    public bool Initialized { get; set; }

    public ChainedConstructorNoDepthDto()
    {
        Value = 200;
        Initialized = true;
    }
}

// When source has a required non-nullable nested property, 
// the generated facet should respect that nullability
public class UserModelWithRequiredSettings
{
    public int Id { get; set; }
    public int SettingsId { get; set; }
    public required UserSettingsModelForNested Settings { get; set; }
}

public class UserSettingsModelForNested
{
    public int Id { get; set; }
    public int StartTick { get; set; }
    public int StopTick { get; set; }
}

[Facet(typeof(UserSettingsModelForNested), nameof(UserSettingsModelForNested.Id))]
public partial class UserSettingsFacet;

// This should generate a non-nullable Settings property because:
// 1. Source property is marked as 'required'
// 2. Source property type has NotAnnotated nullable annotation (non-nullable)
[Facet(typeof(UserModelWithRequiredSettings), PreserveRequiredProperties = true, NestedFacets = [typeof(UserSettingsFacet)])]
public partial class UserWithRequiredSettingsFacet
{
    public int ProcessedTicks => Settings.StopTick - Settings.StartTick;
}

// Test without required - should be nullable for safety
public class UserModelWithOptionalSettings
{
    public int Id { get; set; }
    public UserSettingsModelForNested Settings { get; set; } = new();
}

[Facet(typeof(UserModelWithOptionalSettings), NestedFacets = [typeof(UserSettingsFacet)])]
public partial class UserWithOptionalSettingsFacet;

// Test entities for GitHub issue #258 - Required collection nested facets
// When source has a required non-nullable collection nested property
public class TeamModelWithRequiredMembers
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public required List<UserSettingsModelForNested> Members { get; set; }
}

[Facet(typeof(TeamModelWithRequiredMembers), PreserveRequiredProperties = true, NestedFacets = [typeof(UserSettingsFacet)])]
public partial class TeamWithRequiredMembersFacet;

// This entity has non-nullable string properties WITHOUT initializers
// The generated facet should not trigger CS8618 warnings
public class EntityWithNonNullableProperties
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string ComputedValue => $"{Name}-{Id}";
    public string? NullableField { get; set; }
    public int NumericValue { get; set; }
}

[Facet(typeof(EntityWithNonNullableProperties))]
public partial class NonNullablePropertyFacet;

// Also test with a required property - should not get default!
[Facet(typeof(EntityWithNonNullableProperties), PreserveRequiredProperties = false)]
public partial class NonNullablePropertyFacetNoRequired;
