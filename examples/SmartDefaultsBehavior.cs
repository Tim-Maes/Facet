using System;
using Facet;

namespace Facet.Examples.SmartDefaults;

// Modern source type with init-only and required properties
public record ModernUser
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

// === SMART DEFAULTS IN ACTION ===

// 1. RECORD TYPES: Automatically preserve init-only and required modifiers
[Facet(typeof(ModernUser))]
public partial record UserRecord;
// Generated with: required string Id { get; init; }, required string Name { get; init; }, etc.

// 2. RECORD STRUCT TYPES: Also automatically preserve modifiers  
[Facet(typeof(ModernUser))]
public partial record struct UserRecordStruct;
// Generated with: required string Id { get; init; }, required string Name { get; init; }, etc.

// 3. CLASS TYPES: Default to mutable (backward compatibility)
[Facet(typeof(ModernUser))]
public partial class UserClass;
// Generated with: string Id { get; set; }, string Name { get; set; }, etc.

// 4. STRUCT TYPES: Default to mutable (backward compatibility)
[Facet(typeof(ModernUser))]
public partial struct UserStruct;
// Generated with: string Id { get; set; }, string Name { get; set; }, etc.

// === EXPLICIT OVERRIDES ===

// Force a record to use mutable properties (unusual but possible)
[Facet(typeof(ModernUser), 
       PreserveInitOnlyProperties = false,
       PreserveRequiredProperties = false)]
public partial record MutableUserRecord;
// Generated with: string Id { get; set; }, string Name { get; set; }, etc.

// Force a class to preserve init-only and required (modern class pattern)
[Facet(typeof(ModernUser),
       PreserveInitOnlyProperties = true,
       PreserveRequiredProperties = true)]
public partial class ImmutableUserClass;
// Generated with: required string Id { get; init; }, required string Name { get; init; }, etc.

// === USAGE EXAMPLES ===
public class SmartDefaultsDemo
{
    public static void Demonstrate()
    {
        var user = new ModernUser
        {
            Id = "123",
            Name = "John Doe",
            Email = "john@example.com"
        };

        // Record: Automatically gets init-only and required modifiers
        var userRecord = user.ToFacet<ModernUser, UserRecord>();
        // userRecord.Id is required init-only (preserved from source)
        // userRecord.Name is required init-only (preserved from source)
        
        // Class: Gets mutable properties by default (backward compatibility)
        var userClass = user.ToFacet<ModernUser, UserClass>();
        // userClass.Id is mutable (default behavior for classes)
        // userClass.Name is mutable (default behavior for classes)
        
        // Override examples work as expected
        var mutableRecord = user.ToFacet<ModernUser, MutableUserRecord>();
        var immutableClass = user.ToFacet<ModernUser, ImmutableUserClass>();
    }
}