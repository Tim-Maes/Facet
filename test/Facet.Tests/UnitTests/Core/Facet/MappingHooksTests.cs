using Facet.Mapping;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for Before/After mapping hooks functionality.
/// </summary>
public class MappingHooksTests
{
    public class HooksTestEntity
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public bool IsActive { get; set; }
    }

    public class UserBeforeMapConfig
    {
        public static void BeforeMap(HooksTestEntity source, BeforeMapFacet target)
        {
            target.MappedAt = DateTime.UtcNow;
            
            if (string.IsNullOrEmpty(source.FirstName))
            {
                target.ValidationMessage = "FirstName is required";
            }
        }
    }

    public class UserAfterMapConfig
    {
        public static void AfterMap(HooksTestEntity source, AfterMapFacet target)
        {
            target.FullName = $"{target.FirstName} {target.LastName}";
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

    public class UserCombinedHooksConfig
    {
        public static void BeforeMap(HooksTestEntity source, CombinedHooksFacet target)
        {
            target.MappedAt = DateTime.UtcNow;
        }

        public static void AfterMap(HooksTestEntity source, CombinedHooksFacet target)
        {
            target.FullName = $"{target.FirstName} {target.LastName}";
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

    public class BeforeMapFacet
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public bool IsActive { get; set; }
        public DateTime MappedAt { get; set; }
        public string? ValidationMessage { get; set; }
    }

    public class AfterMapFacet
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public bool IsActive { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    public class CombinedHooksFacet
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public bool IsActive { get; set; }
        public DateTime MappedAt { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    #region Interface Availability Tests

    [Fact]
    public void BeforeMapConfiguration_Interface_ShouldBeAvailable()
    {
        var type = typeof(IFacetBeforeMapConfiguration<,>);
        type.Should().NotBeNull();
        type.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void AfterMapConfiguration_Interface_ShouldBeAvailable()
    {
        var type = typeof(IFacetAfterMapConfiguration<,>);
        type.Should().NotBeNull();
        type.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void CombinedHooksConfiguration_Interface_ShouldBeAvailable()
    {
        var type = typeof(IFacetMapHooksConfiguration<,>);
        type.Should().NotBeNull();
        type.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void AsyncHooksInterfaces_ShouldExist()
    {
        typeof(IFacetBeforeMapConfigurationAsync<,>).Should().NotBeNull();
        typeof(IFacetAfterMapConfigurationAsync<,>).Should().NotBeNull();
        typeof(IFacetMapHooksConfigurationAsync<,>).Should().NotBeNull();
    }

    [Fact]
    public void InstanceHooksInterfaces_ShouldExist()
    {
        typeof(IFacetBeforeMapConfigurationInstance<,>).Should().NotBeNull();
        typeof(IFacetAfterMapConfigurationInstance<,>).Should().NotBeNull();
        typeof(IFacetMapHooksConfigurationInstance<,>).Should().NotBeNull();
    }

    [Fact]
    public void AsyncInstanceHooksInterfaces_ShouldExist()
    {
        typeof(IFacetBeforeMapConfigurationAsyncInstance<,>).Should().NotBeNull();
        typeof(IFacetAfterMapConfigurationAsyncInstance<,>).Should().NotBeNull();
        typeof(IFacetMapHooksConfigurationAsyncInstance<,>).Should().NotBeNull();
    }

    #endregion

    #region FacetAttribute Property Tests

    [Fact]
    public void FacetAttribute_ShouldHaveBeforeMapConfigurationProperty()
    {
        var property = typeof(FacetAttribute).GetProperty("BeforeMapConfiguration");
        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(Type));
    }

    [Fact]
    public void FacetAttribute_ShouldHaveAfterMapConfigurationProperty()
    {
        var property = typeof(FacetAttribute).GetProperty("AfterMapConfiguration");
        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(Type));
    }

    #endregion

    #region BeforeMap Tests

    [Fact]
    public void BeforeMap_ShouldBeCalledBeforePropertyMapping()
    {
        var entity = new HooksTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = DateTime.Today.AddYears(-30),
            IsActive = true
        };
        var target = new BeforeMapFacet();

        UserBeforeMapConfig.BeforeMap(entity, target);

        target.MappedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        target.ValidationMessage.Should().BeNull();
    }

    [Fact]
    public void BeforeMap_ShouldSetValidationMessage_WhenInputInvalid()
    {
        var entity = new HooksTestEntity
        {
            Id = 1,
            FirstName = "",
            LastName = "Doe"
        };
        var target = new BeforeMapFacet();

        UserBeforeMapConfig.BeforeMap(entity, target);

        target.ValidationMessage.Should().Be("FirstName is required");
    }

    [Fact]
    public void BeforeMap_ShouldSetMappedAtTimestamp()
    {
        var entity = new HooksTestEntity { Id = 1, FirstName = "Test", LastName = "User" };
        var target = new BeforeMapFacet();
        var beforeCall = DateTime.UtcNow;

        UserBeforeMapConfig.BeforeMap(entity, target);
        var afterCall = DateTime.UtcNow;

        target.MappedAt.Should().BeOnOrAfter(beforeCall);
        target.MappedAt.Should().BeOnOrBefore(afterCall);
    }

    #endregion

    #region AfterMap Tests

    [Fact]
    public void AfterMap_ShouldComputeDerivedValues()
    {
        var birthDate = DateTime.Today.AddYears(-25);
        var entity = new HooksTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = birthDate,
            IsActive = true
        };
        var target = new AfterMapFacet
        {
            Id = entity.Id,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            DateOfBirth = entity.DateOfBirth,
            IsActive = entity.IsActive
        };

        UserAfterMapConfig.AfterMap(entity, target);

        target.FullName.Should().Be("John Doe");
        target.Age.Should().Be(25);
    }

    [Fact]
    public void AfterMap_ShouldCalculateAge_ForRecentBirthday()
    {
        var birthDate = DateTime.Today.AddMonths(-6).AddYears(-30);
        var entity = new HooksTestEntity
        {
            Id = 1,
            FirstName = "Past",
            LastName = "Birthday",
            DateOfBirth = birthDate
        };
        var target = new AfterMapFacet
        {
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            DateOfBirth = entity.DateOfBirth
        };

        UserAfterMapConfig.AfterMap(entity, target);

        target.Age.Should().Be(30);
    }

    [Fact]
    public void AfterMap_ShouldCalculateAge_ForUpcomingBirthday()
    {
        var birthDate = DateTime.Today.AddMonths(6).AddYears(-30);
        var entity = new HooksTestEntity
        {
            Id = 1,
            FirstName = "Future",
            LastName = "Birthday",
            DateOfBirth = birthDate
        };
        var target = new AfterMapFacet
        {
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            DateOfBirth = entity.DateOfBirth
        };

        UserAfterMapConfig.AfterMap(entity, target);

        target.Age.Should().Be(29);
    }

    #endregion

    #region Combined Hooks Tests

    [Fact]
    public void CombinedHooks_ShouldCallBothBeforeAndAfterMap()
    {
        var birthDate = DateTime.Today.AddYears(-35);
        var entity = new HooksTestEntity
        {
            Id = 1,
            FirstName = "Jane",
            LastName = "Smith",
            DateOfBirth = birthDate,
            IsActive = true
        };
        var target = new CombinedHooksFacet();

        UserCombinedHooksConfig.BeforeMap(entity, target);
        var mappedAtTime = target.MappedAt;
        
        target.Id = entity.Id;
        target.FirstName = entity.FirstName;
        target.LastName = entity.LastName;
        target.DateOfBirth = entity.DateOfBirth;
        target.IsActive = entity.IsActive;
        
        UserCombinedHooksConfig.AfterMap(entity, target);

        target.MappedAt.Should().Be(mappedAtTime);
        target.FullName.Should().Be("Jane Smith");
        target.Age.Should().Be(35);
    }

    [Fact]
    public void CombinedHooks_ShouldPreserveMappedAtFromBeforeMap()
    {
        var entity = new HooksTestEntity
        {
            Id = 1,
            FirstName = "Test",
            LastName = "User",
            DateOfBirth = DateTime.Today.AddYears(-20)
        };
        var target = new CombinedHooksFacet();

        UserCombinedHooksConfig.BeforeMap(entity, target);
        var originalMappedAt = target.MappedAt;
        
        target.FirstName = entity.FirstName;
        target.LastName = entity.LastName;
        target.DateOfBirth = entity.DateOfBirth;
        
        UserCombinedHooksConfig.AfterMap(entity, target);

        target.MappedAt.Should().Be(originalMappedAt);
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void CombinedHooksInterface_ShouldInheritFromBothBeforeAndAfter()
    {
        var combinedType = typeof(IFacetMapHooksConfiguration<,>);
        var interfaces = combinedType.GetInterfaces();
        
        interfaces.Should().Contain(i => 
            i.IsGenericType && 
            i.GetGenericTypeDefinition() == typeof(IFacetBeforeMapConfiguration<,>));
        interfaces.Should().Contain(i => 
            i.IsGenericType && 
            i.GetGenericTypeDefinition() == typeof(IFacetAfterMapConfiguration<,>));
    }

    [Fact]
    public void CombinedAsyncHooksInterface_ShouldInheritFromBothAsyncInterfaces()
    {
        var combinedType = typeof(IFacetMapHooksConfigurationAsync<,>);
        var interfaces = combinedType.GetInterfaces();
        
        interfaces.Should().Contain(i => 
            i.IsGenericType && 
            i.GetGenericTypeDefinition() == typeof(IFacetBeforeMapConfigurationAsync<,>));
        interfaces.Should().Contain(i => 
            i.IsGenericType && 
            i.GetGenericTypeDefinition() == typeof(IFacetAfterMapConfigurationAsync<,>));
    }

    [Fact]
    public void CombinedInstanceHooksInterface_ShouldInheritFromBothInstanceInterfaces()
    {
        var combinedType = typeof(IFacetMapHooksConfigurationInstance<,>);
        var interfaces = combinedType.GetInterfaces();
        
        interfaces.Should().Contain(i => 
            i.IsGenericType && 
            i.GetGenericTypeDefinition() == typeof(IFacetBeforeMapConfigurationInstance<,>));
        interfaces.Should().Contain(i => 
            i.IsGenericType && 
            i.GetGenericTypeDefinition() == typeof(IFacetAfterMapConfigurationInstance<,>));
    }

    [Fact]
    public void CombinedAsyncInstanceInterface_ShouldInheritFromBothAsyncInstanceInterfaces()
    {
        var combinedType = typeof(IFacetMapHooksConfigurationAsyncInstance<,>);
        var interfaces = combinedType.GetInterfaces();
        
        interfaces.Should().Contain(i => 
            i.IsGenericType && 
            i.GetGenericTypeDefinition() == typeof(IFacetBeforeMapConfigurationAsyncInstance<,>));
        interfaces.Should().Contain(i => 
            i.IsGenericType && 
            i.GetGenericTypeDefinition() == typeof(IFacetAfterMapConfigurationAsyncInstance<,>));
    }

    #endregion

    #region Interface Method Signature Tests

    [Fact]
    public void BeforeMapInterface_ShouldHaveStaticAbstractBeforeMapMethod()
    {
        var interfaceType = typeof(IFacetBeforeMapConfiguration<,>);
        var method = interfaceType.GetMethod("BeforeMap");
        
        method.Should().NotBeNull();
        method!.IsStatic.Should().BeTrue();
        method.IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void AfterMapInterface_ShouldHaveStaticAbstractAfterMapMethod()
    {
        var interfaceType = typeof(IFacetAfterMapConfiguration<,>);
        var method = interfaceType.GetMethod("AfterMap");
        
        method.Should().NotBeNull();
        method!.IsStatic.Should().BeTrue();
        method.IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void AsyncBeforeMapInterface_ShouldReturnTask()
    {
        var interfaceType = typeof(IFacetBeforeMapConfigurationAsync<,>);
        var method = interfaceType.GetMethod("BeforeMapAsync");
        
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
    }

    [Fact]
    public void AsyncAfterMapInterface_ShouldReturnTask()
    {
        var interfaceType = typeof(IFacetAfterMapConfigurationAsync<,>);
        var method = interfaceType.GetMethod("AfterMapAsync");
        
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
    }

    [Fact]
    public void InstanceBeforeMapInterface_ShouldHaveInstanceMethod()
    {
        var interfaceType = typeof(IFacetBeforeMapConfigurationInstance<,>);
        var method = interfaceType.GetMethod("BeforeMap");
        
        method.Should().NotBeNull();
        method!.IsStatic.Should().BeFalse();
    }

    [Fact]
    public void InstanceAfterMapInterface_ShouldHaveInstanceMethod()
    {
        var interfaceType = typeof(IFacetAfterMapConfigurationInstance<,>);
        var method = interfaceType.GetMethod("AfterMap");
        
        method.Should().NotBeNull();
        method!.IsStatic.Should().BeFalse();
    }

    #endregion
}
