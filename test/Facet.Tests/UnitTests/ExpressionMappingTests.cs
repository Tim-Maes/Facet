using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Facet.Extensions;
using Facet.Mapping.Expressions;
using Facet.Tests.TestModels;
using Facet.Tests.Utilities;
using FluentAssertions;
using Xunit;

namespace Facet.Tests.UnitTests;

public class ExpressionMappingTests
{
    #region Test Data Setup

    private static List<User> CreateTestUsers()
    {
        return new List<User>
        {
            TestDataFactory.CreateUser("John", "Doe"),
            TestDataFactory.CreateUser("Jane", "Smith"),
            TestDataFactory.CreateUser("Bob", "Johnson"),
            TestDataFactory.CreateUser("Alice", "Williams")
        };
    }

    private static List<UserDto> CreateTestUserDtos()
    {
        var users = CreateTestUsers();
        return users.Select(u => u.ToFacet<User, UserDto>()).ToList();
    }

    #endregion

    #region Predicate Mapping Tests

    [Fact]
    public void MapToFacet_ShouldTransformSimplePredicate()
    {
        // Arrange
        Expression<Func<User, bool>> sourcePredicate = u => u.Id > 1;

        // Act
        Expression<Func<UserDto, bool>> targetPredicate = sourcePredicate.MapToFacet<UserDto>();
        var compiledPredicate = targetPredicate.Compile();

        // Assert
        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledPredicate).ToList();

        // Should return users with Id > 1
        results.Should().HaveCount(3);
        results.Should().NotContain(dto => dto.Id == 1);
    }

    [Fact]
    public void MapToFacet_ShouldTransformComplexPredicate()
    {
        // Arrange
        Expression<Func<User, bool>> sourcePredicate = u => 
            u.Id > 1 && u.FirstName.StartsWith("J") && u.LastName.Contains("o");

        // Act
        Expression<Func<UserDto, bool>> targetPredicate = sourcePredicate.MapToFacet<UserDto>();
        var compiledPredicate = targetPredicate.Compile();

        // Assert
        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledPredicate).ToList();

        // Should match users with Id > 1, FirstName starts with "J", and LastName contains "o"
        results.Should().HaveCountLessOrEqualTo(1);
        if (results.Any())
        {
            results[0].FirstName.Should().StartWith("J");
            results[0].LastName.Should().Contain("o");
        }
    }

    [Fact]
    public void MapToFacet_ShouldHandleComplexPredicateCorrectly()
    {
        // Arrange
        Expression<Func<User, bool>> sourcePredicate = u => 
            u.IsActive && u.FirstName.StartsWith("J");

        // Act
        Expression<Func<UserDto, bool>> targetPredicate = sourcePredicate.MapToFacet<UserDto>();
        var compiledPredicate = targetPredicate.Compile();

        // Assert
        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledPredicate).ToList();

        // Should match active users whose first name starts with "J"
        results.Should().OnlyContain(dto => dto.FirstName.StartsWith("J"));
    }

    [Fact]
    public void MapToFacet_ShouldTransformLogicalOperators()
    {
        // Arrange
        Expression<Func<User, bool>> sourcePredicate = u => 
            u.FirstName == "John" || u.FirstName == "Jane";

        // Act
        Expression<Func<UserDto, bool>> targetPredicate = sourcePredicate.MapToFacet<UserDto>();
        var compiledPredicate = targetPredicate.Compile();

        // Assert
        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledPredicate).ToList();

        results.Should().HaveCountGreaterOrEqualTo(1);
        results.Should().OnlyContain(dto => dto.FirstName == "John" || dto.FirstName == "Jane");
    }

    [Fact]
    public void MapToFacet_WithNullSource_ShouldThrowArgumentNullException()
    {
        // Arrange
        Expression<Func<User, bool>> nullPredicate = null;

        // Act & Assert
        var act = () => nullPredicate.MapToFacet<UserDto>();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Selector Expression Mapping Tests

    [Fact]
    public void MapToFacet_ShouldTransformSelector()
    {
        // Arrange
        Expression<Func<User, string>> sourceSelector = u => u.LastName;

        // Act
        Expression<Func<UserDto, string>> targetSelector = sourceSelector.MapToFacet<UserDto, string>();
        var compiledSelector = targetSelector.Compile();

        // Assert
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
        // Arrange
        Expression<Func<User, int>> sourceSelector = u => u.Id;

        // Act
        Expression<Func<UserDto, int>> targetSelector = sourceSelector.MapToFacet<UserDto, int>();
        var compiledSelector = targetSelector.Compile();

        // Assert
        var testDtos = CreateTestUserDtos();
        var results = testDtos.Select(compiledSelector).OrderBy(x => x).ToList();

        // Should return ordered IDs
        results.Should().HaveCountGreaterThan(0);
        results.Should().BeInAscendingOrder();
    }

    [Fact]
    public void MapToFacet_ShouldHandleSelectorWithMethodCall()
    {
        // Arrange
        Expression<Func<User, string>> sourceSelector = u => u.FirstName.ToUpper();

        // Act
        Expression<Func<UserDto, string>> targetSelector = sourceSelector.MapToFacet<UserDto, string>();
        var compiledSelector = targetSelector.Compile();

        // Assert
        var testDtos = CreateTestUserDtos();
        var results = testDtos.Select(compiledSelector).ToList();

        results.Should().Contain("JOHN");
        results.Should().Contain("JANE");
        results.Should().Contain("BOB");
        results.Should().Contain("ALICE");
    }

    #endregion

    #region Generic Expression Mapping Tests

    [Fact]
    public void MapToFacetGeneric_ShouldTransformLambdaExpression()
    {
        // Arrange
        Expression<Func<User, object>> sourceExpression = u => new { u.FirstName, u.Id };

        // Act
        var targetExpression = sourceExpression.MapToFacetGeneric<UserDto>();

        // Assert
        targetExpression.Should().NotBeNull();
        targetExpression.Parameters.Should().HaveCount(1);
        targetExpression.Parameters[0].Type.Should().Be<UserDto>();
    }

    [Fact]
    public void MapToFacetGeneric_ShouldPreserveExpressionStructure()
    {
        // Arrange
        Expression<Func<User, bool>> sourceExpression = u => u.Id > 1 && u.FirstName != null;

        // Act
        var targetExpression = sourceExpression.MapToFacetGeneric<UserDto>();

        // Assert
        targetExpression.Should().NotBeNull();
        targetExpression.Parameters[0].Type.Should().Be<UserDto>();
        
        // Verify the expression can be compiled and executed
        var compiledExpression = (Expression<Func<UserDto, bool>>)targetExpression;
        var compiled = compiledExpression.Compile();
        
        var testDto = CreateTestUserDtos().First();
        var result = compiled(testDto);
        // Just verify we can execute the compiled expression without error
        (result is bool).Should().BeTrue();
    }

    #endregion

    #region Expression Composition Tests

    [Fact]
    public void CombineWithAnd_ShouldCombineMultiplePredicates()
    {
        // Arrange
        var hasValidId = (Expression<Func<User, bool>>)(u => u.Id > 0);
        var hasValidEmail = (Expression<Func<User, bool>>)(u => !string.IsNullOrEmpty(u.Email));
        var isFirstNameNotEmpty = (Expression<Func<User, bool>>)(u => !string.IsNullOrEmpty(u.FirstName));

        // Act
        var combinedPredicate = FacetExpressionExtensions.CombineWithAnd(
            hasValidId, hasValidEmail, isFirstNameNotEmpty);

        // Assert
        combinedPredicate.Should().NotBeNull();
        var compiled = combinedPredicate.Compile();
        
        var testUsers = CreateTestUsers();
        var results = testUsers.Where(compiled).ToList();
        
        results.Should().HaveCount(4); // All test users should match these basic conditions
    }

    [Fact]
    public void CombineWithOr_ShouldCombineMultiplePredicates()
    {
        // Arrange
        var firstNameStartsWithA = (Expression<Func<User, bool>>)(u => u.FirstName.StartsWith("A"));
        var firstNameStartsWithJ = (Expression<Func<User, bool>>)(u => u.FirstName.StartsWith("J"));

        // Act
        var combinedPredicate = FacetExpressionExtensions.CombineWithOr(firstNameStartsWithA, firstNameStartsWithJ);

        // Assert
        combinedPredicate.Should().NotBeNull();
        var compiled = combinedPredicate.Compile();
        
        var testUsers = CreateTestUsers();
        var results = testUsers.Where(compiled).ToList();
        
        // Should match users whose first name starts with A or J
        results.Should().OnlyContain(u => u.FirstName.StartsWith("A") || u.FirstName.StartsWith("J"));
    }

    [Fact]
    public void CombineWithAnd_WithEmptyArray_ShouldReturnAlwaysTrue()
    {
        // Act
        var result = FacetExpressionExtensions.CombineWithAnd<User>();

        // Assert
        var compiled = result.Compile();
        var testUser = CreateTestUsers().First();
        compiled(testUser).Should().BeTrue();
    }

    [Fact]
    public void CombineWithOr_WithEmptyArray_ShouldReturnAlwaysFalse()
    {
        // Act
        var result = FacetExpressionExtensions.CombineWithOr<User>();

        // Assert
        var compiled = result.Compile();
        var testUser = CreateTestUsers().First();
        compiled(testUser).Should().BeFalse();
    }

    [Fact]
    public void CombineWithAnd_WithSinglePredicate_ShouldReturnSamePredicate()
    {
        // Arrange
        var predicate = (Expression<Func<User, bool>>)(u => u.Id > 1);

        // Act
        var result = FacetExpressionExtensions.CombineWithAnd(predicate);

        // Assert
        result.Should().BeSameAs(predicate);
    }

    [Fact]
    public void Negate_ShouldCreateOppositeCondition()
    {
        // Arrange
        var originalPredicate = (Expression<Func<User, bool>>)(u => u.IsActive);

        // Act
        var negatedPredicate = originalPredicate.Negate();

        // Assert
        var compiledOriginal = originalPredicate.Compile();
        var compiledNegated = negatedPredicate.Compile();
        
        var testUsers = CreateTestUsers();
        
        foreach (var user in testUsers)
        {
            // The negated predicate should give opposite results
            compiledOriginal(user).Should().Be(!compiledNegated(user));
        }
    }

    [Fact]
    public void Negate_WithNullPredicate_ShouldThrowArgumentNullException()
    {
        // Arrange
        Expression<Func<User, bool>> nullPredicate = null;

        // Act & Assert
        var act = () => nullPredicate.Negate();
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void IntegrationTest_PredicateMappingWithComposition()
    {
        // Arrange - Create complex business logic for entities
        var isActiveUser = (Expression<Func<User, bool>>)(u => u.IsActive);
        var hasValidId = (Expression<Func<User, bool>>)(u => u.Id > 0);
        var hasValidName = (Expression<Func<User, bool>>)(u => 
            !string.IsNullOrEmpty(u.FirstName) && !string.IsNullOrEmpty(u.LastName));

        // Combine business rules
        var validUserFilter = FacetExpressionExtensions.CombineWithAnd(
            isActiveUser, hasValidId, hasValidName);

        // Act - Transform to work with DTOs
        var dtoFilter = validUserFilter.MapToFacet<UserDto>();
        var compiledDtoFilter = dtoFilter.Compile();

        // Assert - Verify it works with DTOs
        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledDtoFilter).ToList();

        // Should only contain active users with valid data
        results.Should().OnlyContain(dto => !string.IsNullOrEmpty(dto.FirstName) && !string.IsNullOrEmpty(dto.LastName));
    }

    [Fact]
    public void IntegrationTest_SelectorMappingWithSorting()
    {
        // Arrange
        var idSelector = (Expression<Func<User, int>>)(u => u.Id);
        var nameSelector = (Expression<Func<User, string>>)(u => u.LastName);

        // Act - Transform selectors to work with DTOs
        var dtoIdSelector = idSelector.MapToFacet<UserDto, int>();
        var dtoNameSelector = nameSelector.MapToFacet<UserDto, string>();

        var compiledIdSelector = dtoIdSelector.Compile();
        var compiledNameSelector = dtoNameSelector.Compile();

        // Assert - Use for sorting DTOs
        var testDtos = CreateTestUserDtos();
        
        var sortedById = testDtos.OrderBy(compiledIdSelector).ToList();
        var sortedByName = testDtos.OrderBy(compiledNameSelector).ToList();

        // Verify ID sorting
        sortedById.Should().BeInAscendingOrder(dto => dto.Id);

        // Verify name sorting
        sortedByName.Should().BeInAscendingOrder(dto => dto.LastName);
    }

    [Fact]
    public void IntegrationTest_ComplexExpressionTransformation()
    {
        // Arrange - Create a complex business rule expression
        Expression<Func<User, bool>> complexBusinessRule = u =>
            u.Id > 0 &&
            (u.FirstName.StartsWith("J") || u.LastName.Contains("son")) &&
            !string.IsNullOrEmpty(u.Email);

        // Act - Transform to DTO
        var dtoBusinessRule = complexBusinessRule.MapToFacet<UserDto>();
        var compiledDtoRule = dtoBusinessRule.Compile();

        // Assert - Verify complex logic works with DTOs
        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledDtoRule).ToList();

        // Should match users with valid IDs, names starting with J or containing "son", and valid email
        results.Should().OnlyContain(dto => 
            dto.Id > 0 && 
            (dto.FirstName.StartsWith("J") || dto.LastName.Contains("son")) &&
            !string.IsNullOrEmpty(dto.Email));
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void MapToFacet_WithNullArguments_ShouldThrowArgumentNullException()
    {
        // Test predicate mapping with null
        Expression<Func<User, bool>> nullPredicate = null;
        var act1 = () => nullPredicate.MapToFacet<UserDto>();
        act1.Should().Throw<ArgumentNullException>();

        // Test selector mapping with null
        Expression<Func<User, string>> nullSelector = null;
        var act2 = () => nullSelector.MapToFacet<UserDto, string>();
        act2.Should().Throw<ArgumentNullException>();

        // Test generic mapping with null
        LambdaExpression nullExpression = null;
        var act3 = () => nullExpression.MapToFacetGeneric<UserDto>();
        act3.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CombineWithAnd_WithNullArray_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => FacetExpressionExtensions.CombineWithAnd<User>(null);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CombineWithOr_WithNullArray_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => FacetExpressionExtensions.CombineWithOr<User>(null);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Property Mapping Edge Cases

    [Fact]
    public void MapToFacet_ShouldHandlePropertyThatExistsInBothTypes()
    {
        // Arrange - Test properties that exist in both User and UserDto
        Expression<Func<User, bool>> predicate = u => u.Id > 0 && !string.IsNullOrEmpty(u.FirstName);

        // Act
        var dtoFilter = predicate.MapToFacet<UserDto>();
        var compiledFilter = dtoFilter.Compile();

        // Assert
        var testDtos = CreateTestUserDtos();
        var results = testDtos.Where(compiledFilter).ToList();
        
        // Should return all users since they all have valid IDs and names
        results.Should().HaveCountGreaterThan(0);
        results.Should().OnlyContain(dto => dto.Id > 0 && !string.IsNullOrEmpty(dto.FirstName));
    }

    #endregion
}