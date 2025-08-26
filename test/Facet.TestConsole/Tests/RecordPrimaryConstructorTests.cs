using System;

namespace Facet.TestConsole.Tests;

/// <summary>
/// Simple test for record types with existing primary constructors.
/// </summary>
public class RecordPrimaryConstructorTests
{
    public static void RunAllTests()
    {
        Console.WriteLine("=== Record Primary Constructor Tests ===");
        Console.WriteLine();

        TestBasicRecordWithPrimaryConstructor();
        
        Console.WriteLine("=== Record Primary Constructor Tests Completed ===");
        Console.WriteLine();
    }

    private static void TestBasicRecordWithPrimaryConstructor()
    {
        Console.WriteLine("1. Testing Basic Record with Primary Constructor:");
        Console.WriteLine("==============================================");

        try
        {
            // Create a simple test record with an existing primary constructor
            var source = new TestSource { PropA = true, PropB = "Hello" };
            
            // Create a TestFacet manually using the primary constructor
            // This verifies that the generator doesn't create conflicting positional declarations
            var facet = new TestFacet(42)
            {
                PropA = source.PropA,
                PropB = source.PropB
            };
            
            Console.WriteLine($"? Successfully created TestFacet with existing primary constructor");
            Console.WriteLine($"  PropA: {facet.PropA}");
            Console.WriteLine($"  PropB: {facet.PropB}");
            Console.WriteLine($"  ExtraParam (from primary constructor): {facet.ExtraParam}");
            Console.WriteLine();
            
            // Test that the properties are generated and accessible
            Console.WriteLine($"? Verified that facet properties are generated and accessible");
            
            // Test that the FromSource method exists and provides guidance
            try
            {
                TestFacet.FromSource(source, 123);
                Console.WriteLine("? FromSource should have thrown an exception with guidance");
            }
            catch (NotSupportedException ex)
            {
                Console.WriteLine($"? FromSource correctly provides guidance: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 100))}...");
            }
            
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in TestBasicRecordWithPrimaryConstructor: {ex.Message}");
            Console.WriteLine();
        }
    }
}

// Source type
public class TestSource
{
    public bool PropA { get; set; }
    public string PropB { get; set; } = string.Empty;
}

// Facet with existing primary constructor - this should not conflict with generated code
[Facet(typeof(TestSource))]
public partial record TestFacet(int ExtraParam);