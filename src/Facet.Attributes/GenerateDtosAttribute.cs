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
    Patch = 32,
    All = Create | Update | Response | Query | Upsert | Patch
}

/// <summary>
/// Specifies the output type for generated DTOs.
/// </summary>
public enum OutputType
{
    Class = 0,
    Record = 1,
    Struct = 2,
    RecordStruct = 3,
    /// <summary>
    /// Generates an interface declaring the entity-mapped properties as get-only members.
    /// Useful when you want compile-time enforcement that a hand-written DTO contains all
    /// the entity's properties (the DTO declares <c>: IMyEntityCreateRequest</c> and the
    /// compiler fails until every interface member is satisfied) without surrendering the
    /// DTO's own shape (construction syntax, validation attributes, extra non-entity fields).
    /// Constructors, projections, and ToSource methods are not emitted on interface output.
    /// Not supported for Patch DTOs (their ApplyTo method requires a concrete implementation).
    /// </summary>
    Interface = 4,
    /// <summary>
    /// Generates a <c>partial class</c> (not sealed) with get/set properties and the same constructors
    /// as <see cref="Class"/>, but without projection, <c>ToSource</c>, or <c>BackTo</c> methods.
    /// Designed to be extended by a hand-written partial file in the same project — that is where
    /// callers add validation attributes, computed members, custom constructors, or mapping logic.
    /// When a sibling <c>[GenerateDtos]</c> attribute on the same type uses
    /// <see cref="Interface"/> for the matching DTO type, the generated partial class also declares
    /// the matching generated interface as a base (e.g. <c>: ICreateUserRequest</c>) so the two
    /// outputs compose into a contract + implementation pair.
    /// Not supported for Patch DTOs (their <c>ApplyTo</c> method already lives on a concrete type).
    /// </summary>
    PartialClass = 5
}

/// <summary>
/// Generates standard CRUD DTOs (Create, Update, Response, Query, Upsert, Patch) for a domain model.
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
    /// Properties to exclude from all generated DTOs.
    /// </summary>
    public string[] ExcludeProperties { get; set; } = Array.Empty<string>();

    /// <summary>
    /// When true, automatically excludes common audit fields from generated DTOs.
    /// <para>
    /// Excluded fields: CreatedDate, UpdatedDate, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, UpdatedById.
    /// </para>
    /// Default is false.
    /// </summary>
    public bool ExcludeAuditFields { get; set; } = false;

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
    /// When set, all enum properties from the source type will be converted to the specified type
    /// in generated DTOs. Supported values are <see cref="string"/> and <see cref="int"/>.
    /// When null (default), enum properties retain their original enum types.
    /// </summary>
    public Type? ConvertEnumsTo { get; set; }

    /// <summary>
    /// If true, generated files will use the full type name (namespace + containing types)
    /// to avoid collisions. Default is false (shorter file names).
    /// </summary>
    public bool UseFullName { get; set; } = false;
}

/// <summary>
/// Obsolete. Use <see cref="GenerateDtosAttribute"/> with <c>ExcludeAuditFields = true</c> instead.
/// </summary>
/// <remarks>
/// This attribute has been replaced by <see cref="GenerateDtosAttribute"/> with the <c>ExcludeAuditFields</c> property.
/// </remarks>
/// <example>
/// <code>
/// // Old way (deprecated):
/// [GenerateAuditableDtos(Types = DtoTypes.Create)]
/// 
/// // New way:
/// [GenerateDtos(Types = DtoTypes.Create, ExcludeAuditFields = true)]
/// </code>
/// </example>
[Obsolete("Use GenerateDtosAttribute with ExcludeAuditFields = true instead. This attribute will be removed in a future version.")]
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
    /// If true, generated files will use the full type name (namespace + containing types)
    /// to avoid collisions. Default is false (shorter file names).
    /// </summary>
    public bool UseFullName { get; set; } = false;
}
