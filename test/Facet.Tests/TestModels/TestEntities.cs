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

public abstract class BaseEntity<TPkKey>
{
    public TPkKey Id { get; set; } = default!;
}

public class Category : BaseEntity<uint>
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class EntityWithStaticMembers
{
    public const string AConst = "A";
    public static readonly string AStaticReadonly = "A";
    public string AProperty { get; set; } = "A";
    public static string AStaticProperty => "A";
}

internal sealed class TaggedItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public IList<string> Tags { get; set; } = [];
}

[Facet(typeof(TaggedItem), GenerateEquality = true, GenerateToSource = true)]
internal partial record TaggedItemFacet;

public partial class DDDSample
{
    private DDDSample() { }

    public static DDDSample Create(string aProperty, string aPrivateSetterProperty, string aInternalSetterProperty)
    {
        return new DDDSample
        {
            AProperty = aProperty,
            APrivateSetterProperty = aPrivateSetterProperty,
            AInternalSetterProperty = aInternalSetterProperty
        };
    }

    public string AProperty { get; set; } = default!;
    public string APrivateSetterProperty { get; private set; } = default!;
    public string AInternalSetterProperty { get; internal set; } = default!;

    // Nested facets should have access to private constructor and private setters
    [Facet(typeof(DDDSample), GenerateToSource = true)]
    public partial record InsideFacetRecord;

    [Facet(typeof(DDDSample), GenerateToSource = true)]
    public partial class InsideFacetClass;
}

[Facet(typeof(DDDSample))]
public partial record OutsideFacetRecord;

[Facet(typeof(DDDSample))]
public partial class OutsideFacetClass;

public class DDDSampleInternal
{
    internal DDDSampleInternal() { }

    public static DDDSampleInternal Create(string aProperty, string aPrivateSetterProperty, string aInternalSetterProperty)
    {
        return new DDDSampleInternal
        {
            AProperty = aProperty,
            APrivateSetterProperty = aPrivateSetterProperty,
            AInternalSetterProperty = aInternalSetterProperty
        };
    }

    public string AProperty { get; set; } = default!;
    public string APrivateSetterProperty { get; private set; } = default!;
    public string AInternalSetterProperty { get; internal set; } = default!;
}

[Facet(typeof(DDDSampleInternal))]
public partial class OutsideFacetInternalCtorClass;

[Facet(typeof(DDDSampleInternal), "APrivateSetterProperty", GenerateToSource = true)]
public partial class OutsideFacetInternalCtorWithToSource;

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

[Facet(typeof(UserModel))]
public partial class UserModelDto;

public class InitOnlyWithInitializers
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

[Facet(typeof(InitOnlyWithInitializers))]
public partial class InitOnlyWithInitializersDto;

public class ModelWithListProperty
{
    public List<string> Tags { get; set; } = [];
}

[Facet(typeof(ModelWithListProperty))]
public partial record RecordWithListDefault;

[Facet(typeof(ModelWithListProperty), GenerateParameterlessConstructor = false)]
public partial record RecordWithListNoParameterless;

[Facet(typeof(ModelWithListProperty), GenerateProjection = false)]
public partial record RecordWithListNoProjection;

public class ModelWithMultipleProperties
{
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public int Count { get; set; }
}

[Facet(typeof(ModelWithMultipleProperties))]
public partial record RecordWithMultipleProperties;

public class ModelWithNullableList
{
    public List<string>? Tags { get; set; }
}

[Facet(typeof(ModelWithNullableList))]
public partial record RecordWithNullableList;

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
        Value = 100;
        Initialized = true;
    }
}

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

[Facet(typeof(UserModelWithRequiredSettings), PreserveRequiredProperties = true, NestedFacets = [typeof(UserSettingsFacet)])]
public partial class UserWithRequiredSettingsFacet
{
    public int ProcessedTicks => Settings.StopTick - Settings.StartTick;
}

public class UserModelWithOptionalSettings
{
    public int Id { get; set; }
    public UserSettingsModelForNested Settings { get; set; } = new();
}

[Facet(typeof(UserModelWithOptionalSettings), NestedFacets = [typeof(UserSettingsFacet)])]
public partial class UserWithOptionalSettingsFacet;

public class TeamModelWithRequiredMembers
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public required List<UserSettingsModelForNested> Members { get; set; }
}

[Facet(typeof(TeamModelWithRequiredMembers), PreserveRequiredProperties = true, NestedFacets = [typeof(UserSettingsFacet)])]
public partial class TeamWithRequiredMembersFacet;

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

[Facet(typeof(EntityWithNonNullableProperties), PreserveRequiredProperties = false)]
public partial class NonNullablePropertyFacetNoRequired;

public class PersonForCopyAndEquality
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime? BirthDate { get; set; }
}

[Facet(typeof(PersonForCopyAndEquality), GenerateCopyConstructor = true)]
public partial class PersonWithCopyConstructorDto;

[Facet(typeof(PersonForCopyAndEquality), GenerateEquality = true)]
public partial class PersonWithEqualityDto;

[Facet(typeof(PersonForCopyAndEquality), GenerateCopyConstructor = true, GenerateEquality = true)]
public partial class PersonWithCopyAndEqualityDto;

[Facet(typeof(PersonForCopyAndEquality), GenerateEquality = true)]
public partial record PersonRecordWithEquality;

[Facet(typeof(PersonForCopyAndEquality), GenerateCopyConstructor = true, GenerateEquality = true)]
public partial struct PersonStructWithCopyAndEquality;

[Facet(typeof(User))]
public partial record UserRecordWithConstructor();

public class JsonStoredEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
}

/// <summary>Source type A for multi-source facet tests.</summary>
public class MultiSourceEntityA
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OnlyInA { get; set; } = string.Empty;
}

/// <summary>Source type B for multi-source facet tests.</summary>
public class MultiSourceEntityB
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OnlyInB { get; set; } = string.Empty;
}

/// <summary>Source type C (record) for multi-source facet tests.</summary>
public record MultiSourceEntityC
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}

/// <summary>Unit DTO - one of the sources for the multi-source nested facet.</summary>
public class UnitDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ValidationResult { get; set; } = string.Empty;
}

/// <summary>Unit entity - another source for the multi-source nested facet.</summary>
public class UnitEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ValidationResult { get; set; } = string.Empty;
}

/// <summary>Order line entity - contains a UnitEntity as a nested property.</summary>
public class OrderLineBaseEntity
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public UnitEntity? AssignedToUnit { get; set; }
}

/// <summary>A custom struct used to test excluded struct properties in ToSource.</summary>
public struct GeoLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

/// <summary>Source class with a required struct property for ToSource struct-default testing.</summary>
public class LocationEntity
{
    public required string Name { get; set; }
    public required GeoLocation Location { get; set; }
    public required string Description { get; set; }
}

    public class ImmutableEntity
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

