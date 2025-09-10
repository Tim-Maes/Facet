using Facet.Tests.TestModels;

namespace Facet.Tests.TestModels;

[Facet(typeof(User), "Password", "CreatedAt")]
public partial class UserDto 
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

[Facet(typeof(Product), "InternalNotes", Kind = FacetKind.Record)]
public partial record ProductDto;

[Facet(typeof(Employee), "Password", "Salary", "CreatedAt")]
public partial class EmployeeDto;

[Facet(typeof(Manager), "Password", "Salary", "Budget", "CreatedAt")]
public partial class ManagerDto;

[Facet(typeof(ModernUser), "PasswordHash", "Bio")]
public partial record ModernUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

[Facet(typeof(UserWithEnum))]
public partial class UserWithEnumDto;

[Facet(typeof(User), "Password", "CreatedAt", Kind = FacetKind.RecordStruct)]
public partial record struct UserSummary;

[Facet(typeof(Product), "InternalNotes", "CreatedAt", Kind = FacetKind.Struct)]
public partial struct ProductSummary;

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