using System.Linq.Expressions;
using Facet.Mapping.Expressions;
using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Extensions.Mapping;

public class ExpressionMappingTests
{
    private static List<User> CreateTestUsers()
    {
        var users = new List<User>
        {
            TestDataFactory.CreateUser("John", "Doe"),
            TestDataFactory.CreateUser("Jane", "Smith"),
            TestDataFactory.CreateUser("Bob", "Johnson"),
            TestDataFactory.CreateUser("Alice", "Williams")
        };
        
        for (int i = 0; i < users.Count; i++)
        {
            users[i].Id = i + 1; 
        }
        
        return users;
    }

    private static List<UserDto> CreateTestUserDtos()
    {
        var users = CreateTestUsers();
        return users.Select(u => u.ToFacet<User, UserDto>()).ToList();
    }

    [Fact]
    public void MapToFacet_ShouldTransformSimplePredicate()
    {
        Expression<Func<User, bool>> sourcePredicate = u => u.Id > 1;

        Expression<Func<UserDto, bool>> targetPredicate = sourcePredicate.MapToFacet<UserDto>();
        var compiledPredicate = targetPredicate.Compile();

        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledPredicate).ToList();

        results.Should().HaveCount(3);
        results.Should().NotContain(dto => dto.Id == 1);
    }

    [Fact]
    public void MapToFacet_ShouldTransformComplexPredicate()
    {
        Expression<Func<User, bool>> sourcePredicate = u => 
            u.Id > 1 && u.FirstName.StartsWith("J");

        Expression<Func<UserDto, bool>> targetPredicate = sourcePredicate.MapToFacet<UserDto>();
        var compiledPredicate = targetPredicate.Compile();

        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledPredicate).ToList();

        results.Should().OnlyContain(dto => 
            dto.Id > 1 && 
            dto.FirstName.StartsWith("J"));
        results.Should().HaveCount(1); 
        results[0].FirstName.Should().Be("Jane");
    }

    [Fact]
    public void MapToFacet_ShouldHandleComplexPredicateCorrectly()
    {
        Expression<Func<User, bool>> sourcePredicate = u => 
            u.IsActive && u.FirstName.StartsWith("J");

        Expression<Func<UserDto, bool>> targetPredicate = sourcePredicate.MapToFacet<UserDto>();
        var compiledPredicate = targetPredicate.Compile();

        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledPredicate).ToList();

        results.Should().OnlyContain(dto => dto.IsActive && dto.FirstName.StartsWith("J"));
    }

    [Fact]
    public void MapToFacet_ShouldTransformLogicalOperators()
    {
        Expression<Func<User, bool>> sourcePredicate = u => 
            u.FirstName == "John" || u.FirstName == "Jane";

        Expression<Func<UserDto, bool>> targetPredicate = sourcePredicate.MapToFacet<UserDto>();
        var compiledPredicate = targetPredicate.Compile();

        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledPredicate).ToList();

        results.Should().HaveCountGreaterThanOrEqualTo(1);
        results.Should().OnlyContain(dto => dto.FirstName == "John" || dto.FirstName == "Jane");
    }

    [Fact]
    public void MapToFacet_WithNullSource_ShouldThrowArgumentNullException()
    {
        Expression<Func<User, bool>> nullPredicate = null;

        var act = () => nullPredicate.MapToFacet<UserDto>();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapToFacet_ShouldTransformSelector()
    {
        Expression<Func<User, string>> sourceSelector = u => u.LastName;

        Expression<Func<UserDto, string>> targetSelector = sourceSelector.MapToFacet<UserDto, string>();
        var compiledSelector = targetSelector.Compile();

        var testDtos = CreateTestUserDtos();
        var results = testDtos.Select(compiledSelector).ToList();

        results.Should().HaveCount(4);
        results.Should().Contain("Doe");
        results.Should().Contain("Smith");
        results.Should().Contain("Johnson");
        results.Should().Contain("Williams");
    }

    [Fact]
    public void MapToFacet_ShouldTransformIntSelector()
    {
        Expression<Func<User, int>> sourceSelector = u => u.Id;

        Expression<Func<UserDto, int>> targetSelector = sourceSelector.MapToFacet<UserDto, int>();
        var compiledSelector = targetSelector.Compile();

        var testDtos = CreateTestUserDtos();
        var results = testDtos.Select(compiledSelector).OrderBy(x => x).ToList();

        results.Should().HaveCountGreaterThan(0);
        results.Should().BeInAscendingOrder();
    }

    [Fact]
    public void MapToFacet_ShouldHandleSelectorWithMethodCall()
    {
        Expression<Func<User, string>> sourceSelector = u => u.FirstName.ToUpper();

        Expression<Func<UserDto, string>> targetSelector = sourceSelector.MapToFacet<UserDto, string>();
        var compiledSelector = targetSelector.Compile();

        var testDtos = CreateTestUserDtos();
        var results = testDtos.Select(compiledSelector).ToList();

        results.Should().Contain("JOHN");
        results.Should().Contain("JANE");
        results.Should().Contain("BOB");
        results.Should().Contain("ALICE");
    }

    [Fact]
    public void MapToFacetGeneric_ShouldTransformLambdaExpression()
    {
        Expression<Func<User, object>> sourceExpression = u => new { u.FirstName, u.Id };

        var targetExpression = sourceExpression.MapToFacetGeneric<UserDto>();

        targetExpression.Should().NotBeNull();
        targetExpression.Parameters.Should().HaveCount(1);
        targetExpression.Parameters[0].Type.Should().Be<UserDto>();
    }

    [Fact]
    public void MapToFacetGeneric_ShouldPreserveExpressionStructure()
    {
        Expression<Func<User, bool>> sourceExpression = u => u.Id > 1 && u.FirstName != null;

        var targetExpression = sourceExpression.MapToFacetGeneric<UserDto>();

        targetExpression.Should().NotBeNull();
        targetExpression.Parameters[0].Type.Should().Be<UserDto>();
        
        var compiledExpression = (Expression<Func<UserDto, bool>>)targetExpression;
        var compiled = compiledExpression.Compile();
        
        var testDto = CreateTestUserDtos().First();
        var result = compiled(testDto);
        
        (result is bool).Should().BeTrue();
    }

    [Fact]
    public void CombineWithAnd_ShouldCombineMultiplePredicates()
    {
        var hasValidId = (Expression<Func<User, bool>>)(u => u.Id > 0);
        var hasValidEmail = (Expression<Func<User, bool>>)(u => !string.IsNullOrEmpty(u.Email));
        var isFirstNameNotEmpty = (Expression<Func<User, bool>>)(u => !string.IsNullOrEmpty(u.FirstName));

        var combinedPredicate = FacetExpressionExtensions.CombineWithAnd(
            hasValidId, hasValidEmail, isFirstNameNotEmpty);

        combinedPredicate.Should().NotBeNull();
        var compiled = combinedPredicate.Compile();
        
        var testUsers = CreateTestUsers();
        var results = testUsers.Where(compiled).ToList();
        
        results.Should().HaveCount(4); 
    }

    [Fact]
    public void CombineWithOr_ShouldCombineMultiplePredicates()
    {
        var firstNameStartsWithA = (Expression<Func<User, bool>>)(u => u.FirstName.StartsWith("A"));
        var firstNameStartsWithJ = (Expression<Func<User, bool>>)(u => u.FirstName.StartsWith("J"));

        var combinedPredicate = FacetExpressionExtensions.CombineWithOr(firstNameStartsWithA, firstNameStartsWithJ);

        combinedPredicate.Should().NotBeNull();
        var compiled = combinedPredicate.Compile();
        
        var testUsers = CreateTestUsers();
        var results = testUsers.Where(compiled).ToList();
        
        results.Should().OnlyContain(u => u.FirstName.StartsWith("A") || u.FirstName.StartsWith("J"));
    }

    [Fact]
    public void CombineWithAnd_WithEmptyArray_ShouldReturnAlwaysTrue()
    {
        var result = FacetExpressionExtensions.CombineWithAnd<User>();

        var compiled = result.Compile();
        var testUser = CreateTestUsers().First();
        compiled(testUser).Should().BeTrue();
    }

    [Fact]
    public void CombineWithOr_WithEmptyArray_ShouldReturnAlwaysFalse()
    {
        var result = FacetExpressionExtensions.CombineWithOr<User>();

        var compiled = result.Compile();
        var testUser = CreateTestUsers().First();
        compiled(testUser).Should().BeFalse();
    }

    [Fact]
    public void CombineWithAnd_WithSinglePredicate_ShouldReturnSamePredicate()
    {
        var predicate = (Expression<Func<User, bool>>)(u => u.Id > 1);

        var result = FacetExpressionExtensions.CombineWithAnd(predicate);

        result.Should().BeSameAs(predicate);
    }

    [Fact]
    public void Negate_ShouldCreateOppositeCondition()
    {
        var originalPredicate = (Expression<Func<User, bool>>)(u => u.IsActive);

        var negatedPredicate = originalPredicate.Negate();

        var compiledOriginal = originalPredicate.Compile();
        var compiledNegated = negatedPredicate.Compile();
        
        var testUsers = CreateTestUsers();
        
        foreach (var user in testUsers)
        {
            compiledOriginal(user).Should().Be(!compiledNegated(user));
        }
    }

    [Fact]
    public void Negate_WithNullPredicate_ShouldThrowArgumentNullException()
    {
        Expression<Func<User, bool>> nullPredicate = null;

        var act = () => nullPredicate.Negate();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IntegrationTest_PredicateMappingWithComposition()
    {
        var isActiveUser = (Expression<Func<User, bool>>)(u => u.IsActive);
        var hasValidId = (Expression<Func<User, bool>>)(u => u.Id > 0);
        var hasValidName = (Expression<Func<User, bool>>)(u => 
            !string.IsNullOrEmpty(u.FirstName) && !string.IsNullOrEmpty(u.LastName));

        var validUserFilter = FacetExpressionExtensions.CombineWithAnd(
            isActiveUser, hasValidId, hasValidName);

        var dtoFilter = validUserFilter.MapToFacet<UserDto>();
        var compiledDtoFilter = dtoFilter.Compile();

        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledDtoFilter).ToList();

        results.Should().OnlyContain(dto => !string.IsNullOrEmpty(dto.FirstName) && !string.IsNullOrEmpty(dto.LastName));
    }

    [Fact]
    public void IntegrationTest_SelectorMappingWithSorting()
    {
        var idSelector = (Expression<Func<User, int>>)(u => u.Id);
        var nameSelector = (Expression<Func<User, string>>)(u => u.LastName);

        var dtoIdSelector = idSelector.MapToFacet<UserDto, int>();
        var dtoNameSelector = nameSelector.MapToFacet<UserDto, string>();

        var compiledIdSelector = dtoIdSelector.Compile();
        var compiledNameSelector = dtoNameSelector.Compile();

        var testDtos = CreateTestUserDtos();
        
        var sortedById = testDtos.OrderBy(compiledIdSelector).ToList();
        var sortedByName = testDtos.OrderBy(compiledNameSelector).ToList();

        sortedById.Should().BeInAscendingOrder(dto => dto.Id);

        sortedByName.Should().BeInAscendingOrder(dto => dto.LastName);
    }

    [Fact]
    public void IntegrationTest_ComplexExpressionTransformation()
    {
        Expression<Func<User, bool>> complexBusinessRule = u =>
            u.Id > 0 &&
            (u.FirstName.StartsWith("J") || u.LastName.Contains("son")) &&
            u.IsActive;

        var dtoBusinessRule = complexBusinessRule.MapToFacet<UserDto>();
        var compiledDtoRule = dtoBusinessRule.Compile();

        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledDtoRule).ToList();

        results.Should().OnlyContain(dto => 
            dto.Id > 0 && 
            (dto.FirstName.StartsWith("J") || dto.LastName.Contains("son")) &&
            dto.IsActive);
    }

    [Fact]
    public void MapToFacet_WithNullArguments_ShouldThrowArgumentNullException()
    {
        Expression<Func<User, bool>> nullPredicate = null;
        var act1 = () => nullPredicate.MapToFacet<UserDto>();
        act1.Should().Throw<ArgumentNullException>();

        Expression<Func<User, string>> nullSelector = null;
        var act2 = () => nullSelector.MapToFacet<UserDto, string>();
        act2.Should().Throw<ArgumentNullException>();

        LambdaExpression nullExpression = null;
        var act3 = () => nullExpression.MapToFacetGeneric<UserDto>();
        act3.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CombineWithAnd_WithNullArray_ShouldThrowArgumentNullException()
    {
        var act = () => FacetExpressionExtensions.CombineWithAnd<User>(null);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CombineWithOr_WithNullArray_ShouldThrowArgumentNullException()
    {
        var act = () => FacetExpressionExtensions.CombineWithOr<User>(null);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapToFacet_ShouldHandlePropertyThatExistsInBothTypes()
    {
        Expression<Func<User, bool>> predicate = u => u.Id > 0 && !string.IsNullOrEmpty(u.FirstName);

        var dtoFilter = predicate.MapToFacet<UserDto>();
        var compiledFilter = dtoFilter.Compile();

        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledFilter).ToList();
        
        results.Should().HaveCountGreaterThan(0);
        results.Should().OnlyContain(dto => dto.Id > 0 && !string.IsNullOrEmpty(dto.FirstName));
    }
}
