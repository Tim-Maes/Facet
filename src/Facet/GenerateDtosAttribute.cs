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
    /// Types whose members should be excluded from all generated DTOs.
    /// Useful for excluding all properties from base classes or mixins.
    /// Example: new[] { typeof(BaseEntity), typeof(IAuditableEntity) }
    /// </summary>
    public Type[] ExcludeMembersFromType { get; set; } = Array.Empty<Type>();

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

    /// <summary>
    /// Optional interface types the generated DTOs should implement.
    /// Applies to all generated DTOs for this attribute instance.
    /// Example: new[] { typeof(ICreateSchedulePayload) }
    /// </summary>
    public Type[] InterfaceContracts { get; set; } = Array.Empty<Type>();
}

/// <summary>
/// Predefined attribute for common auditable entity scenarios.
/// Automatically excludes common audit fields: CreatedDate, UpdatedDate, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy.
/// Can be applied multiple times with different configurations for fine-grained control.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class GenerateAuditableDtosAttribute : Attribute
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
    /// Additional properties to exclude from all generated DTOs (in addition to audit fields).
    /// </summary>
    public string[] ExcludeProperties { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Types whose members should be excluded from all generated DTOs.
    /// Useful for excluding all properties from base classes or mixins.
    /// Example: new[] { typeof(BaseEntity), typeof(IAuditableEntity) }
    /// </summary>
    public Type[] ExcludeMembersFromType { get; set; } = Array.Empty<Type>();

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

    /// <summary>
    /// Optional interface types the generated DTOs should implement.
    /// Applies to all generated DTOs for this attribute instance.
    /// Example: new[] { typeof(ICreateSchedulePayload) }
    /// </summary>
    public Type[] InterfaceContracts { get; set; } = Array.Empty<Type>();
}
