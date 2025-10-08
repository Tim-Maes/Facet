# Facet Testing Guide

This directory contains comprehensive tests for the Facet source generator library using modern testing best practices.

## Test Project

### Facet.Tests - Modern Unit Test Suite
xUnit-based test suite with proper assertions and isolated test scenarios.

**Features:**
- ✅ xUnit testing framework
- ✅ FluentAssertions for readable assertions  
- ✅ Isolated test methods with proper setup/teardown
- ✅ IDE integration (Visual Studio, VS Code, Rider)
- ✅ CI/CD friendly with standard test runners
- ✅ Detailed test categorization (Unit Tests, Integration Tests)
- ✅ In-memory database testing for EF Core integration

**Test Categories:**
- **UnitTests/**: Core functionality tests
  - BasicMappingTests.cs - Property mapping verification
  - InheritanceTests.cs - Class inheritance handling
  - CustomMappingTests.cs - Custom mapping configurations
  - ModernRecordTests.cs - Modern C# record support
  - EnumHandlingTests.cs - Enum property handling
- **IntegrationTests/**: EF Core and LINQ integration
  - LinqProjectionTests.cs - LINQ query projection testing

## Running Tests

### Option 1: Use the Test Scripts (Recommended)

**Windows:**
```cmd
run-tests.bat
```

**Linux/macOS:**
```bash
./run-tests.sh
```

### Option 2: Manual Commands

**Run unit tests only:**
```bash
dotnet test test/Facet.Tests --verbosity normal
```


**Build and run all:**
```bash
dotnet build test/FacetTest.sln
dotnet test test/Facet.Tests
```

### Option 3: IDE Integration

**Visual Studio:**
- Use Test Explorer to run individual tests or test suites
- Right-click on test methods to run/debug specific tests

**VS Code with C# extension:**
- Use the Testing panel to discover and run tests
- CodeLens integration for running tests inline

## Test Structure Best Practices

### 1. Arrange-Act-Assert Pattern
```csharp
[Fact]
public void ToFacet_ShouldMapBasicProperties_WhenMappingUserToDto()
{
    // Arrange
    var user = TestDataFactory.CreateUser("John", "Doe");

    // Act  
    var dto = user.ToFacet<User, UserDto>();

    // Assert
    dto.FirstName.Should().Be("John");
    dto.LastName.Should().Be("Doe");
}
```

### 2. Descriptive Test Names
Test names follow the pattern: `MethodUnderTest_ShouldExpectedBehavior_WhenCondition`

### 3. FluentAssertions Usage
```csharp
// Instead of Assert.Equal
dto.FirstName.Should().Be("John");

// Instead of Assert.True
dto.IsActive.Should().BeTrue();

// For collections
dtos.Should().HaveCount(3);
dtos.Should().OnlyContain(dto => dto.IsActive);
```

### 4. Test Data Factory
Centralized test data creation in `Utilities/TestDataFactory.cs`:
```csharp
var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
var users = TestDataFactory.CreateUsers(); // Creates 3 sample users
```

## Key Test Scenarios Covered

### Core Functionality
- ✅ Basic property mapping
- ✅ Property exclusion (specified properties not copied)
- ✅ Nullable property handling
- ✅ Boolean value preservation
- ✅ Different data types (strings, dates, decimals, enums)

### Advanced Features
- ✅ Class inheritance (Employee : User, Manager : Employee)
- ✅ Different facet kinds (Class, Record, Struct, RecordStruct)
- ✅ Custom mapping configurations with IFacetMapConfiguration
- ✅ Modern C# records with required/init properties
- ✅ Enum handling and preservation
- ✅ LINQ query projection integration
- ✅ Entity Framework Core integration

### Edge Cases
- ✅ Null values in nullable properties
- ✅ Empty strings and default values
- ✅ Complex inheritance hierarchies
- ✅ Multiple exclusion properties
- ✅ Age calculation edge cases (birthdays)
- ✅ Record equality and with expressions

## Test Best Practices

The test suite follows modern .NET testing conventions:

1. **Descriptive Test Names:**
   ```csharp
   [Fact]
   public void ToFacet_ShouldMapBasicProperties_WhenMappingUserToDto()
   ```

2. **Arrange-Act-Assert Pattern:**
   ```csharp
   // Arrange
   var user = TestDataFactory.CreateUser("John", "Doe");
   
   // Act
   var dto = user.ToFacet<User, UserDto>();
   
   // Assert
   dto.FirstName.Should().Be("John");
   ```

3. **FluentAssertions for Readability:**
   ```csharp
   dto.FirstName.Should().Be("John");
   dto.IsActive.Should().BeTrue();
   dtos.Should().HaveCount(3);
   ```

## Continuous Integration

The test suite is designed to work with standard CI/CD pipelines:

```yaml
# GitHub Actions example
- name: Run Tests
  run: dotnet test test/Facet.Tests --logger trx --results-directory TestResults

- name: Publish Test Results  
  uses: dorny/test-reporter@v1
  if: success() || failure()
  with:
    name: Test Results
    path: TestResults/*.trx
    reporter: dotnet-trx
```

## Benefits of This Approach

1. **Better Error Messages**: FluentAssertions provides detailed failure information
2. **IDE Integration**: IntelliSense, debugging, test discovery
3. **Parallel Execution**: xUnit runs tests in parallel by default
4. **Test Categorization**: Organize tests by feature/scenario
5. **Isolated Tests**: Each test is independent and can be run separately
6. **Standard Tooling**: Works with all .NET testing tools and CI/CD systems
7. **Maintainability**: Clear test structure makes maintenance easier
8. **Coverage Reports**: Integration with code coverage tools

This modern testing approach provides automated, reliable, and maintainable test verification with excellent developer tooling integration.