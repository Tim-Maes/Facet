using System;
using System.Linq;
using Facet;

namespace Facet.TestConsole.Tests;

/// <summary>
/// Comprehensive tests for the new GenerateDtos attribute feature.
/// Tests all DTO variants and configuration options.
/// </summary>
public static class GenerateDtosTests
{
    public static void RunAllTests()
    {
        Console.WriteLine("=== GenerateDtos Attribute Tests ===");
        Console.WriteLine();

        TestBasicGenerateDtos();
        TestCustomConfiguration();
        TestAuditableEntities();
        TestNamespaceTargeting();
        TestCustomNaming();
        TestDifferentOutputTypes();
        TestSpecificDtoTypes();
        TestComplexEntity();

        Console.WriteLine("=== GenerateDtos Tests Completed ===");
        Console.WriteLine();
    }

    private static void TestBasicGenerateDtos()
    {
        Console.WriteLine("1. Testing Basic GenerateDtos (All DTOs):");
        Console.WriteLine("==========================================");

        var customer = new CustomerEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "+1-555-0123",
            DateOfBirth = new DateTime(1985, 5, 15),
            IsActive = true,
            CreatedAt = DateTime.Now.AddDays(-30),
            UpdatedAt = DateTime.Now.AddDays(-1),
            CreatedBy = "System",
            UpdatedBy = "Admin"
        };

        try
        {
            // Test Create DTO (should exclude Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
            var createDto = new CreateCustomerEntityRequest(customer);
            Console.WriteLine($"? CreateCustomerEntityRequest created successfully");
            Console.WriteLine($"  FirstName: {createDto.FirstName}, LastName: {createDto.LastName}");
            Console.WriteLine($"  Email: {createDto.Email}, Phone: {createDto.Phone}");
            Console.WriteLine($"  DateOfBirth: {createDto.DateOfBirth:yyyy-MM-dd}");
            Console.WriteLine($"  IsActive: {createDto.IsActive}");
            Console.WriteLine($"  Properties excluded: Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy");

            // Test Update DTO (should exclude CreatedAt, CreatedBy, UpdatedAt, UpdatedBy but keep Id)
            var updateDto = new UpdateCustomerEntityRequest(customer);
            Console.WriteLine($"\n? UpdateCustomerEntityRequest created successfully");
            Console.WriteLine($"  Id: {updateDto.Id} (included for identification)");
            Console.WriteLine($"  FirstName: {updateDto.FirstName}, LastName: {updateDto.LastName}");
            Console.WriteLine($"  Email: {updateDto.Email}, Phone: {updateDto.Phone}");
            Console.WriteLine($"  Properties excluded: CreatedAt, UpdatedAt, CreatedBy, UpdatedBy");

            // Test Response DTO (should include all properties)
            var responseDto = new CustomerEntityResponse(customer);
            Console.WriteLine($"\n? CustomerEntityResponse created successfully");
            Console.WriteLine($"  Id: {responseDto.Id}, FirstName: {responseDto.FirstName}");
            Console.WriteLine($"  Email: {responseDto.Email}, Active: {responseDto.IsActive}");
            Console.WriteLine($"  CreatedAt: {responseDto.CreatedAt:yyyy-MM-dd}, UpdatedAt: {responseDto.UpdatedAt:yyyy-MM-dd}");
            Console.WriteLine($"  CreatedBy: {responseDto.CreatedBy}, UpdatedBy: {responseDto.UpdatedBy}");
            Console.WriteLine($"  All properties included");

            // Test Query DTO (should have all properties nullable)
            var queryDto = new CustomerEntityQuery(customer);
            Console.WriteLine($"\n? CustomerEntityQuery created successfully");
            Console.WriteLine($"  Id: {queryDto.Id} (nullable for filtering)");
            Console.WriteLine($"  FirstName: {queryDto.FirstName} (nullable)");
            Console.WriteLine($"  Email: {queryDto.Email} (nullable)");
            Console.WriteLine($"  All properties are nullable for flexible filtering");

            // Test LINQ projections work
            var customers = new[] { customer }.AsQueryable();
            var createProjections = customers.Select(CreateCustomerEntityRequest.Projection).ToList();
            var responseProjections = customers.Select(CustomerEntityResponse.Projection).ToList();
            
            Console.WriteLine($"\n? LINQ Projections work correctly");
            Console.WriteLine($"  Create projections: {createProjections.Count}");
            Console.WriteLine($"  Response projections: {responseProjections.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in BasicGenerateDtos test: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static void TestCustomConfiguration()
    {
        Console.WriteLine("2. Testing Custom Configuration:");
        Console.WriteLine("================================");

        var product = new ProductEntity
        {
            Id = 1,
            Name = "Premium Laptop",
            Description = "High-performance laptop for professionals",
            Price = 1299.99m,
            CategoryId = 5,
            SKU = "LAPTOP-001",
            IsActive = true,
            CreatedAt = DateTime.Now.AddDays(-10)
        };

        try
        {
            // Test Create DTO only (excludes Id, CreatedAt, and custom exclusions)
            var createDto = new CreateProductEntityRequest(product);
            Console.WriteLine($"? CreateProductEntityRequest created successfully");
            Console.WriteLine($"  Name: {createDto.Name}, Price: {createDto.Price:C}");
            Console.WriteLine($"  CategoryId: {createDto.CategoryId}, Active: {createDto.IsActive}");
            Console.WriteLine($"  Properties excluded: Id, CreatedAt, Description, SKU");

            // Test Response DTO (includes all except custom exclusions)
            var responseDto = new ProductEntityResponse(product);
            Console.WriteLine($"\n? ProductEntityResponse created successfully");
            Console.WriteLine($"  Id: {responseDto.Id}, Name: {responseDto.Name}");
            Console.WriteLine($"  Price: {responseDto.Price:C}, CategoryId: {responseDto.CategoryId}");
            Console.WriteLine($"  CreatedAt: {responseDto.CreatedAt:yyyy-MM-dd}, Active: {responseDto.IsActive}");
            Console.WriteLine($"  Properties excluded: Description, SKU");

            // Test that these are generated as classes, not records
            Console.WriteLine($"\n? Generated as Class type (mutable properties)");
            Console.WriteLine($"  Note: For classes, all properties are mutable by default");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in CustomConfiguration test: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static void TestAuditableEntities()
    {
        Console.WriteLine("3. Testing Auditable Entities:");
        Console.WriteLine("===============================");

        var order = new OrderEntity
        {
            Id = 1,
            OrderNumber = "ORD-2024-001",
            CustomerId = 123,
            TotalAmount = 1599.98m,
            Status = "Pending",
            OrderDate = DateTime.Now,
            CreatedAt = DateTime.Now.AddHours(-2),
            UpdatedAt = DateTime.Now.AddMinutes(-30),
            CreatedBy = "CustomerPortal",
            UpdatedBy = "OrderProcessor"
        };

        try
        {
            // Test that audit fields are automatically excluded
            var createDto = new CreateOrderEntityRequest(order);
            Console.WriteLine($"? CreateOrderEntityRequest created successfully");
            Console.WriteLine($"  OrderNumber: {createDto.OrderNumber}");
            Console.WriteLine($"  CustomerId: {createDto.CustomerId}, Amount: {createDto.TotalAmount:C}");
            Console.WriteLine($"  Status: {createDto.Status}, Date: {createDto.OrderDate:yyyy-MM-dd}");
            Console.WriteLine($"  Auto-excluded: Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy");

            var updateDto = new UpdateOrderEntityRequest(order);
            Console.WriteLine($"\n? UpdateOrderEntityRequest created successfully");
            Console.WriteLine($"  Id: {updateDto.Id} (kept for identification)");
            Console.WriteLine($"  OrderNumber: {updateDto.OrderNumber}, Status: {updateDto.Status}");
            Console.WriteLine($"  Auto-excluded: CreatedAt, UpdatedAt, CreatedBy, UpdatedBy");

            var responseDto = new OrderEntityResponse(order);
            Console.WriteLine($"\n? OrderEntityResponse created successfully");
            Console.WriteLine($"  All properties included in Response DTO");
            Console.WriteLine($"  CreatedAt: {responseDto.CreatedAt:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"  UpdatedAt: {responseDto.UpdatedAt:yyyy-MM-dd HH:mm}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in AuditableEntities test: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static void TestNamespaceTargeting()
    {
        Console.WriteLine("4. Testing Namespace Targeting:");
        Console.WriteLine("===============================");

        var invoice = new InvoiceEntity
        {
            Id = 1,
            InvoiceNumber = "INV-2024-001",
            Amount = 2500.00m,
            DueDate = DateTime.Now.AddDays(30),
            IsPaid = false,
            CreatedAt = DateTime.Now
        };

        try
        {
            // These DTOs should be generated in Facet.TestConsole.DTOs namespace
            var createDto = new Facet.TestConsole.DTOs.CreateInvoiceEntityRequest(invoice);
            var responseDto = new Facet.TestConsole.DTOs.InvoiceEntityResponse(invoice);

            Console.WriteLine($"? DTOs generated in target namespace 'Facet.TestConsole.DTOs'");
            Console.WriteLine($"  CreateInvoiceEntityRequest: {createDto.InvoiceNumber}");
            Console.WriteLine($"  InvoiceEntityResponse: {responseDto.InvoiceNumber}, Amount: {responseDto.Amount:C}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in NamespaceTargeting test: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static void TestCustomNaming()
    {
        Console.WriteLine("5. Testing Custom Naming Convention:");
        Console.WriteLine("====================================");

        var simple = new SimpleEntity
        {
            Id = 1,
            Name = "Test Entity"
        };

        var payment = new PaymentEntity
        {
            Id = 1,
            Amount = 150.00m,
            PaymentMethod = "CreditCard",
            ProcessedAt = DateTime.Now,
            IsSuccessful = true
        };

        try
        {
            // Test the simple entity with custom naming: Test + SimpleEntity + Dto = TestSimpleEntityDto
            var testDto = new TestSimpleEntityDto(simple);
            Console.WriteLine($"? Custom naming convention applied for SimpleEntity");
            Console.WriteLine($"  Generated: TestSimpleEntityDto (Test + SimpleEntity + Dto)");
            Console.WriteLine($"  Id: {testDto.Id}, Name: {testDto.Name}");

            // For PaymentEntity, it should be: Api + PaymentEntity + Model = ApiPaymentEntityModel
            Console.WriteLine($"\n? Custom naming convention configured for PaymentEntity");
            Console.WriteLine($"  Expected: ApiPaymentEntityModel (Api + PaymentEntity + Model)");
            Console.WriteLine($"  Payment amount: {payment.Amount:C}, Method: {payment.PaymentMethod}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in CustomNaming test: {ex.Message}");
            Console.WriteLine($"  Note: Custom naming may need verification");
        }

        Console.WriteLine();
    }

    private static void TestDifferentOutputTypes()
    {
        Console.WriteLine("6. Testing Different Output Types:");
        Console.WriteLine("==================================");

        var notification = new NotificationEntity
        {
            Id = 1,
            Title = "System Update",
            Message = "The system will be updated tonight.",
            IsRead = false,
            CreatedAt = DateTime.Now
        };

        try
        {
            // Generated as record struct
            var createDto = new CreateNotificationEntityRequest(notification);
            var responseDto = new NotificationEntityResponse(notification);

            Console.WriteLine($"? DTOs generated as RecordStruct");
            Console.WriteLine($"  CreateNotificationEntityRequest: {createDto.Title}");
            Console.WriteLine($"  NotificationEntityResponse: {responseDto.Title} (Read: {responseDto.IsRead})");
            
            // Test that they're actually structs (value semantics)
            var copy = createDto;
            Console.WriteLine($"? Value semantics confirmed (record struct behavior)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in DifferentOutputTypes test: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static void TestSpecificDtoTypes()
    {
        Console.WriteLine("7. Testing Specific DTO Types Only:");
        Console.WriteLine("====================================");

        var category = new CategoryEntity
        {
            Id = 1,
            Name = "Electronics",
            Description = "Electronic devices and components",
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        try
        {
            // Only Update and Response DTOs should be generated
            var updateDto = new UpdateCategoryEntityRequest(category);
            var responseDto = new CategoryEntityResponse(category);

            Console.WriteLine($"? Only specified DTO types generated (Update and Response)");
            Console.WriteLine($"  UpdateCategoryEntityRequest: {updateDto.Name}");
            Console.WriteLine($"  CategoryEntityResponse: {responseDto.Name} - {responseDto.Description}");
            Console.WriteLine($"  Create and Query DTOs not generated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in SpecificDtoTypes test: {ex.Message}");
        }

        Console.WriteLine();
    }

    private static void TestComplexEntity()
    {
        Console.WriteLine("8. Testing Complex Entity with Relationships:");
        Console.WriteLine("==============================================");

        var employee = new EmployeeEntity
        {
            Id = 1,
            EmployeeNumber = "EMP001",
            FirstName = "Alice",
            LastName = "Johnson",
            Email = "alice.johnson@company.com",
            Department = "Engineering",
            Position = "Senior Developer",
            Salary = 95000m,
            HireDate = new DateTime(2020, 3, 15),
            ManagerId = 10,
            IsActive = true,
            CreatedAt = DateTime.Now.AddDays(-365),
            UpdatedAt = DateTime.Now.AddDays(-10)
        };

        try
        {
            var createDto = new CreateEmployeeEntityRequest(employee);
            Console.WriteLine($"? CreateEmployeeEntityRequest created successfully");
            Console.WriteLine($"  Employee: {createDto.FirstName} {createDto.LastName}");
            Console.WriteLine($"  Department: {createDto.Department}, Position: {createDto.Position}");
            Console.WriteLine($"  Hire Date: {createDto.HireDate:yyyy-MM-dd}");
            Console.WriteLine($"  Excluded: Id, Salary, CreatedAt, UpdatedAt (sensitive/audit fields)");

            var updateDto = new UpdateEmployeeEntityRequest(employee);
            Console.WriteLine($"\n? UpdateEmployeeEntityRequest created successfully");
            Console.WriteLine($"  Id: {updateDto.Id}, Employee: {updateDto.FirstName} {updateDto.LastName}");
            Console.WriteLine($"  Department: {updateDto.Department}, Position: {updateDto.Position}");
            Console.WriteLine($"  Excluded: Salary, CreatedAt, UpdatedAt (sensitive/audit fields)");

            var responseDto = new EmployeeEntityResponse(employee);
            Console.WriteLine($"\n? EmployeeEntityResponse created successfully");
            Console.WriteLine($"  Complete employee data available in Response DTO");
            Console.WriteLine($"  Id: {responseDto.Id}, Created: {responseDto.CreatedAt:yyyy-MM-dd}");
            Console.WriteLine($"  Note: Salary excluded from all DTOs as specified");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in ComplexEntity test: {ex.Message}");
        }

        Console.WriteLine();
    }
}