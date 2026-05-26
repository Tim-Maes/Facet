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

[Facet(typeof(ModernUser), "PasswordHash", "Bio", PreserveRequiredProperties = true)]
public partial record ModernUserRequiredDto;

[Facet(typeof(EntityWithStaticMembers))]
public partial record StaticMemberTestDto;

[Facet(typeof(User), "Password", "CreatedAt")]
public partial record struct UserSummary;

[Facet(typeof(Product), "InternalNotes", "CreatedAt")]
public partial struct ProductSummary;

[Facet(typeof(EventLog), "Source", GenerateToSource = true)]
public partial class EventLogDto;

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

public class UserDtoAsyncMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        
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
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.Age = CalculateAge(source.DateOfBirth);
    }

    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        await Task.Delay(8, cancellationToken);
        
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

[Facet(typeof(Dummy), exclude: [nameof(Dummy.Age)])]
public partial record DummyDto
{
    /// <summary>User-defined nullable property</summary>
    public string? NameInUpperCase { get; init; }
}

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

[Facet(typeof(Product), "InternalNotes", "CreatedAt", NullableProperties = true, GenerateToSource = false)]
public partial class ProductQueryDto;

[Facet(typeof(User), "Password", "CreatedAt", NullableProperties = true, GenerateToSource = false)]
public partial record UserQueryDto;

[Facet(typeof(UserWithEnum), NullableProperties = true, GenerateToSource = false)]
public partial class UserWithEnumQueryDto;

[Facet(typeof(Category), "Id")]
public partial record UpdateCategoryViewModel;

[Facet(typeof(UserWithEnum), ConvertEnumsTo = typeof(string), GenerateToSource = true)]
public partial class UserWithEnumToStringDto;

[Facet(typeof(UserWithEnum), ConvertEnumsTo = typeof(int), GenerateToSource = true)]
public partial class UserWithEnumToIntDto;

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

[Facet(typeof(UserWithEnum), ConvertEnumsTo = typeof(string), NullableProperties = true)]
public partial class UserWithEnumToStringNullableDto;

public class EntityWithEnumCollection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<UserStatus> Statuses { get; set; } = new();
}

[Facet(typeof(EntityWithEnumCollection), ConvertEnumsTo = typeof(string), GenerateToSource = true)]
public partial class EntityWithEnumCollectionToStringDto;

[Facet(typeof(EntityWithEnumCollection), ConvertEnumsTo = typeof(int), GenerateToSource = true)]
public partial class EntityWithEnumCollectionToIntDto;

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
    nameof(JsonStoredEntity.MetadataJson),  
    Configuration = typeof(OrderDtoForwardMapper),
    ToSourceConfiguration = typeof(OrderDtoToSourceMapper),
    GenerateToSource = true)]
public partial class OrderDto
{
    public OrderMetadata? Metadata { get; set; }
}

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

/// <summary>
/// Multi-source nested facet that can map from either UnitDto or UnitEntity.
/// This is used as a nested property in OrderLineBaseUpsertDto.
/// </summary>
[Facet(typeof(UnitDto),
       GenerateToSource = true,
       Include = new[] { nameof(UnitDto.Name), nameof(UnitDto.ValidationResult) })]
[Facet(typeof(UnitEntity),
       GenerateToSource = true,
       Include = new[] { nameof(UnitEntity.Name), nameof(UnitEntity.ValidationResult) })]
public partial class UnitDropDownDto;

/// <summary>
/// Parent facet that uses UnitDropDownDto (a multi-source facet) as a nested property.
/// Tests that ToSource() correctly calls the appropriate ToSource method on the nested facet.
/// </summary>
[Facet(typeof(OrderLineBaseEntity),
       GenerateToSource = true,
       Include = new[] { nameof(OrderLineBaseEntity.AssignedToUnit), nameof(OrderLineBaseEntity.Number) },
       NestedFacets = new[] { typeof(UnitDropDownDto) })]
public partial class OrderLineBaseUpsertDto;

/// <summary>
/// Base class with a single-source Facet.
/// Generates standard members: Projection, ToSource, BackTo.
/// </summary>
[Facet(typeof(UnitDto),
       GenerateToSource = true,
       Include = new[] { nameof(UnitDto.Name) })]
public partial class UnitBaseFacet;

/// <summary>
/// Derived class with multiple [Facet] attributes.
/// Generates custom-named members: ProjectionFromUnitDto, ProjectionFromUnitEntity,
/// ToUnitDto, ToUnitEntity. These must NOT have 'new' since they don't hide
/// the base class's Projection/ToSource/BackTo.
/// </summary>
[Facet(typeof(UnitDto),
       GenerateToSource = true,
       Include = new[] { nameof(UnitDto.Name), nameof(UnitDto.ValidationResult) })]
[Facet(typeof(UnitEntity),
       GenerateToSource = true,
       Include = new[] { nameof(UnitEntity.Name), nameof(UnitEntity.ValidationResult) })]
public partial class UnitMultiSourceInheritedFacet : UnitBaseFacet;

/// <summary>Facet that excludes a struct property (Location) from LocationEntity to test ToSource generates default(T) not null.</summary>
[Facet(typeof(LocationEntity), nameof(LocationEntity.Location), GenerateToSource = true)]
public partial class LocationDto;

[Facet(typeof(User), "Password", "CreatedAt", SetAccessor = PropertySetAccessor.Init)]
public partial class UserImmutableDto;

[Facet(typeof(User), "Password", "CreatedAt", SetAccessor = PropertySetAccessor.Set)]
public partial class UserMutableDto;

[Facet(typeof(ImmutableEntity), SetAccessor = PropertySetAccessor.Preserve)]
public partial record ImmutableEntityPreserveDto;

[Facet(typeof(ImmutableEntity), SetAccessor = PropertySetAccessor.Set)]
public partial class ImmutableEntityMutableDto;

