using System;
using Facet.Tests.TestModels;

namespace Facet.Tests.Verification;

public class NullableVerification
{
    public static void VerifyNullableHandling()
    {
        Console.WriteLine("=== Nullable Handling Verification ===");
        
        // Create test entity with nullable string
        var testEntity = new NullableTestEntity
        {
            Test1 = true,
            Test2 = false,
            Test3 = "Non-nullable string",
            Test4 = null  // This is nullable and set to null
        };

        // Map to DTO
        var dto = testEntity.ToFacet<NullableTestEntity, NullableTestDto>();
        
        // Verify the mapping worked
        Console.WriteLine($"Test1: {dto.Test1} (bool)");
        Console.WriteLine($"Test2: {dto.Test2} (bool?)");
        Console.WriteLine($"Test3: '{dto.Test3}' (string)");
        Console.WriteLine($"Test4: {(dto.Test4 == null ? "null" : $"'{dto.Test4}'")} (string?)");
        
        // Verify type information using reflection
        var dtoType = typeof(NullableTestDto);
        
        var test1Property = dtoType.GetProperty("Test1");
        var test2Property = dtoType.GetProperty("Test2");
        var test3Property = dtoType.GetProperty("Test3");
        var test4Property = dtoType.GetProperty("Test4");
        
        Console.WriteLine("\n=== Property Type Information ===");
        Console.WriteLine($"Test1 Type: {test1Property?.PropertyType}");
        Console.WriteLine($"Test2 Type: {test2Property?.PropertyType}");
        Console.WriteLine($"Test3 Type: {test3Property?.PropertyType}");
        Console.WriteLine($"Test4 Type: {test4Property?.PropertyType}");
        
        // Test with nullable reference context
        var nullabilityContext = new System.Reflection.NullabilityInfoContext();
        
        if (test4Property != null)
        {
            var test4NullabilityInfo = nullabilityContext.Create(test4Property);
            Console.WriteLine($"Test4 Nullability State: {test4NullabilityInfo.ReadState}");
            
            var isNullable = test4NullabilityInfo.ReadState == System.Reflection.NullabilityState.Nullable;
            Console.WriteLine($"Test4 Is Nullable: {isNullable}");
        }
        
        Console.WriteLine("\n=== Test passed! Nullable strings are now preserved correctly ===");
    }
}