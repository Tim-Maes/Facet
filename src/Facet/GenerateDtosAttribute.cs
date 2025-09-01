using System;

namespace Facet;

/// <summary>
/// Flags indicating which DTO types to generate for a domain model.
/// </summary>
[Flags]
public enum DtoTypes
{
    None = 0,
    Create = 1,
    Update = 2,
    Response = 4,
    Query = 8,
    Upsert = 16,
    All = Create | Update | Response | Query | Upsert
}

/// <summary>
/// Specifies the output type for generated DTOs.
/// </summary>
public enum OutputType
{
    Class = 0,
    Record = 1,
    Struct = 2,
    RecordStruct = 3
}

/// <summary>
/// Generates standard CRUD DTOs (Create, Update, Response, Query, Upsert) for a domain model.
/// Can be applied multiple times with different configurations for fine-grained control.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class GenerateDtosAttribute : Attribute
{
    /// <summary>
    /// Which DTO types to generate (default: All).
    /// </summary>
    public DtoTypes Types { get; set; } = DtoTypes.All;

    /// <summary>
    /// The output type for generated DTOs (default: Record).
    /// </summary>
    public OutputType OutputType { get; set; } = OutputType.Record;

    /// <summary>
    /// Custom namespace for generated DTOs. If null, uses the same namespace as the source type.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Additional properties to exclude from all generated DTOs.
    /// </summary>
    public string[] ExcludeProperties { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Custom prefix for generated DTO names (default: none).
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Custom suffix for generated DTO names (default: none).
    /// </summary>
    public string? Suffix { get; set; }

    /// <summary>
    /// Whether to include public fields from the source type (default: false).
    /// </summary>
    public bool IncludeFields { get; set; } = false;

    /// <summary>
    /// Whether to generate constructors for the DTOs (default: true).
    /// </summary>
    public bool GenerateConstructors { get; set; } = true;

    /// <summary>
    /// Whether to generate projection expressions for the DTOs (default: true).
    /// </summary>
    public bool GenerateProjections { get; set; } = true;
}

/// <summary>
/// Predefined attribute for common auditable entity scenarios.
/// Automatically excludes common audit fields: CreatedDate, UpdatedDate, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy.
/// Can be applied multiple times with different configurations for fine-grained control.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GenerateAuditableDtosAttribute : GenerateDtosAttribute
{
    /// <summary>
    /// Common audit field names to exclude by default.
    /// </summary>
    private static readonly string[] DefaultAuditFields = 
    {
        "CreatedDate", "UpdatedDate", "CreatedAt", "UpdatedAt", 
        "CreatedBy", "UpdatedBy", "CreatedById", "UpdatedById"
    };

    public GenerateAuditableDtosAttribute()
    {
        // Combine user-specified exclusions with default audit fields
        var userExclusions = base.ExcludeProperties ?? Array.Empty<string>();
        var allExclusions = new string[userExclusions.Length + DefaultAuditFields.Length];
        userExclusions.CopyTo(allExclusions, 0);
        DefaultAuditFields.CopyTo(allExclusions, userExclusions.Length);
        base.ExcludeProperties = allExclusions;
    }
}