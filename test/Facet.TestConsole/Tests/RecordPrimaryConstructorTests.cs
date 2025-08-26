using System;
using Facet.Mapping;

namespace Facet.TestConsole.Tests;

/// <summary>
/// Comprehensive tests for record types with and without existing primary constructors.
/// Validates that the record primary constructor conflict issue is completely resolved.
/// </summary>
public class RecordPrimaryConstructorTests
{
    public static void RunAllTests()
    {
        Console.WriteLine("=== Record Primary Constructor Tests ===");
        Console.WriteLine();

        TestBasicRecordWithPrimaryConstructor();
        TestRegularRecordWithoutPrimaryConstructor();
        
        Console.WriteLine("=== Record Primary Constructor Tests Completed ===");
        Console.WriteLine();
    }

    private static void TestBasicRecordWithPrimaryConstructor()
    {
        Console.WriteLine("1. Testing Basic Record with Existing Primary Constructor:");
        Console.WriteLine("=========================================================");

        try
        {
            var source = new TestSource { PropA = true, PropB = "Hello" };
            
            // The key test: creating a record with user-defined primary constructor
            // This should work without compilation conflicts
            var facet = new TestFacet(42)
            {
                PropA = source.PropA,
                PropB = source.PropB
            };
            
            Console.WriteLine($"? Successfully created TestFacet with existing primary constructor");
            Console.WriteLine($"  PropA: {facet.PropA}");
            Console.WriteLine($"  PropB: {facet.PropB}");
            Console.WriteLine($"  ExtraParam (from primary constructor): {facet.ExtraParam}");
            
            // Verify FromSource provides helpful guidance
            try
            {
                TestFacet.FromSource(source, 123);
                Console.WriteLine("? FromSource should have thrown an exception with guidance");
            }
            catch (NotSupportedException ex)
            {
                Console.WriteLine($"? FromSource correctly provides guidance");
            }
            
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in TestBasicRecordWithPrimaryConstructor: {ex.Message}");
            Console.WriteLine();
        }
    }

    private static void TestRegularRecordWithoutPrimaryConstructor()
    {
        Console.WriteLine("2. Testing Regular Record without Primary Constructor:");
        Console.WriteLine("===================================================");

        try
        {
            var source = new TestSource { PropA = false, PropB = "World" };
            
            // Regular record should still work with the generated constructor
            var facet = new RegularRecordFacet(source);
            
            Console.WriteLine($"? Successfully created RegularRecordFacet with generated constructor");
            Console.WriteLine($"  PropA: {facet.PropA}");
            Console.WriteLine($"  PropB: {facet.PropB}");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in TestRegularRecordWithoutPrimaryConstructor: {ex.Message}");
            Console.WriteLine();
        }
    }
}

// Test source types
public class TestSource
{
    public bool PropA { get; set; }
    public string PropB { get; set; } = string.Empty;
}

// Test facet types - these demonstrate the solution to the primary constructor conflict

// 1. Basic record with existing primary constructor - should generate properties but no conflicting positional declaration
[Facet(typeof(TestSource))]
public partial record TestFacet(int ExtraParam);

// 2. Regular record without primary constructor - should work as before with positional generation
[Facet(typeof(TestSource))]
public partial record RegularRecordFacet;