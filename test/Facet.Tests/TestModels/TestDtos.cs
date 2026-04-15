namespace Facet.Tests.TestModels;

[Facet(typeof(User), "Password", "CreatedAt", GenerateToSource = true, SourceSignature = "a83684c8")]
public partial class UserDto
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

[Facet(typeof(Product), "InternalNotes", GenerateToSource = true)]
public partial record ProductDto;

[Facet(typeof(Employee), "Password", "Salary", "CreatedAt", GenerateToSource = true)]
public partial class EmployeeDto;

[Facet(typeof(Manager), "Password", "Salary", "Budget", "CreatedAt", GenerateToSource = true)]
public partial class ManagerDto;

[Facet(typeof(ClassicUser), GenerateToSource = true)]
public partial record ClassicUserDto;

[Facet(typeof(ModernUser), "PasswordHash", "Bio", GenerateToSource = true)]
public partial record ModernUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

[Facet(typeof(UserWithEnum), GenerateToSource = true)]
public partial class UserWithEnumDto;

// Exact scenario from issue #298: plain record facet with required properties, no () workaround
[Facet(typeof(ModernUser), "PasswordHash", "Bio", PreserveRequiredProperties = true)]
public partial record ModernUserRequiredDto;

// Issue #300: static properties should not be included in facets
[Facet(typeof(EntityWithStaticMembers))]
public partial record StaticMemberTestDto;

[Facet(typeof(User), "Password", "CreatedAt")]
public partial record struct UserSummary;

[Facet(typeof(Product), "InternalNotes", "CreatedAt")]
public partial struct ProductSummary;

[Facet(typeof(EventLog), "Source", GenerateToSource = true)]
public partial class EventLogDto;

// Include functionality test DTOs
[Facet(typeof(User), Include = new[] { "FirstName", "LastName", "Email" }, GenerateToSource = true)]
public partial class UserIncludeDto;

[Facet(typeof(User), Include = new[] { "FirstName" })]
public partial class UserSingleIncludeDto;

[Facet(typeof(User), Include = new[] { "DateOfBirth" })]
public partial record UserSingleObjectIncludeDto;

[Facet(typeof(Tenant))]
public partial record TenantSingleObjectIncludeDto;

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

// Test for GitHub issue: Source type with NO nullable properties but facet has nullable user-defined property
[Facet(typeof(Dummy), exclude: [nameof(Dummy.Age)])]
public partial record DummyDto
{
    /// <summary>User-defined nullable property</summary>
    public string? NameInUpperCase { get; init; }
}

// Test for GitHub issue #194: Nested facet inside another facet
[Facet(typeof(UserForNestedFacet), Include = [
    nameof(UserForNestedFacet.Id),
    nameof(UserForNestedFacet.Name),
    nameof(UserForNestedFacet.Address)
], NestedFacets = [typeof(UserDetailResponse.UserAddressItem)])]
public partial class UserDetailResponse
{
    [Facet(typeof(UserAddressForNestedFacet), Include = [
        nameof(UserAddressForNestedFacet.FormattedAddress)
    ])]
    public partial class UserAddressItem;
}

// NullableProperties functionality test DTOs
[Facet(typeof(Product), "InternalNotes", "CreatedAt", NullableProperties = true, GenerateToSource = false)]
public partial class ProductQueryDto;

[Facet(typeof(User), "Password", "CreatedAt", NullableProperties = true, GenerateToSource = false)]
public partial record UserQueryDto;

[Facet(typeof(UserWithEnum), NullableProperties = true, GenerateToSource = false)]
public partial class UserWithEnumQueryDto;

// Test for excluding inherited property from base class
[Facet(typeof(Category), "Id")]
public partial record UpdateCategoryViewModel;

// ConvertEnumsTo functionality test DTOs
[Facet(typeof(UserWithEnum), ConvertEnumsTo = typeof(string), GenerateToSource = true)]
public partial class UserWithEnumToStringDto;

[Facet(typeof(UserWithEnum), ConvertEnumsTo = typeof(int), GenerateToSource = true)]
public partial class UserWithEnumToIntDto;

// Test with nullable enum property
public class EntityWithNullableEnum
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public UserStatus? Status { get; set; }
    public UserStatus NonNullableStatus { get; set; }
}

[Facet(typeof(EntityWithNullableEnum), ConvertEnumsTo = typeof(string), GenerateToSource = true)]
public partial class NullableEnumToStringDto;

[Facet(typeof(EntityWithNullableEnum), ConvertEnumsTo = typeof(int), GenerateToSource = true)]
public partial class NullableEnumToIntDto;

// Test ConvertEnumsTo with NullableProperties = true
[Facet(typeof(UserWithEnum), ConvertEnumsTo = typeof(string), NullableProperties = true)]
public partial class UserWithEnumToStringNullableDto;

// ToSourceConfiguration tests

/// <summary>
/// Simulates parsing/serialising a JSON metadata property on the round-trip.
/// Forward  (Entity > DTO):  MetadataJson string > OrderMetadata object
/// Reverse  (DTO > Entity):  OrderMetadata object > MetadataJson string
/// </summary>
public class OrderMetadata
{
    public string Tag { get; set; } = string.Empty;
    public int Priority { get; set; }

    public static OrderMetadata Parse(string json)
    {
        // Minimal hand-rolled parsing to avoid a JSON dependency in tests
        var tag = "";
        var priority = 0;
        foreach (var part in json.Trim('{', '}').Split(','))
        {
            var kv = part.Split(':');
            if (kv.Length == 2)
            {
                var key = kv[0].Trim().Trim('"');
                var val = kv[1].Trim().Trim('"');
                if (key == "tag") tag = val;
                if (key == "priority" && int.TryParse(val, out var p)) priority = p;
            }
        }
        return new OrderMetadata { Tag = tag, Priority = priority };
    }

    public string Serialize() => $"{{\"tag\":\"{Tag}\",\"priority\":{Priority}}}";
}

public class OrderDtoForwardMapper : IFacetMapConfiguration<JsonStoredEntity, OrderDto>
{
    public static void Map(JsonStoredEntity source, OrderDto target)
        => target.Metadata = OrderMetadata.Parse(source.MetadataJson);
}

public class OrderDtoToSourceMapper : IFacetToSourceConfiguration<OrderDto, JsonStoredEntity>
{
    public static void Map(OrderDto facet, JsonStoredEntity target)
        => target.MetadataJson = facet.Metadata?.Serialize() ?? "{}";
}

[Facet(typeof(JsonStoredEntity),
    nameof(JsonStoredEntity.MetadataJson),  // excluded, DTO uses parsed Metadata instead
    Configuration = typeof(OrderDtoForwardMapper),
    ToSourceConfiguration = typeof(OrderDtoToSourceMapper),
    GenerateToSource = true)]
public partial class OrderDto
{
    public OrderMetadata? Metadata { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────────
// Multi-source mapping test DTOs (GitHub issue: map different source types to the
// same target type).
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A DTO that can be constructed from either <see cref="MultiSourceEntityA"/> or
/// <see cref="MultiSourceEntityB"/> – both share Id and Name.
/// </summary>
[Facet(typeof(MultiSourceEntityA), Include = new[] { nameof(MultiSourceEntityA.Id), nameof(MultiSourceEntityA.Name) })]
[Facet(typeof(MultiSourceEntityB), Include = new[] { nameof(MultiSourceEntityB.Id), nameof(MultiSourceEntityB.Name) })]
public partial class MultiSourceDto;

/// <summary>
/// A DTO that maps from A (with ToSource) and from B (without ToSource),
/// used to verify per-source ToSource method generation.
/// </summary>
[Facet(typeof(MultiSourceEntityA), Include = new[] { nameof(MultiSourceEntityA.Id), nameof(MultiSourceEntityA.Name) }, GenerateToSource = true)]
[Facet(typeof(MultiSourceEntityB), Include = new[] { nameof(MultiSourceEntityB.Id), nameof(MultiSourceEntityB.Name) })]
public partial class MultiSourceWithToSourceDto;

/// <summary>
/// A DTO that maps from A and B including their exclusive properties, used to verify
/// the union-of-members behaviour.
/// </summary>
[Facet(typeof(MultiSourceEntityA))]
[Facet(typeof(MultiSourceEntityB))]
public partial class MultiSourceUnionDto;
