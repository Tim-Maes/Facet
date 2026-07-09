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
/// Flags specifying the output(s) for generated DTOs: four concrete <em>kinds</em>
/// (<see cref="Class"/>, <see cref="Record"/>, <see cref="Struct"/>, <see cref="RecordStruct"/>),
/// the <see cref="Interface"/> kind, and one <em>modifier</em> (<see cref="Partial"/>) that
/// composes with any kind. Combine kinds to emit several outputs from a single
/// <see cref="GenerateDtosAttribute"/> — most usefully <c>Interface | Record | Partial</c>,
/// which produces an extensible contract + implementation pair.
/// Combining multiple <em>concrete</em> kinds (e.g. <c>Class | Record</c>) is rejected with
/// error <c>FAC101</c>: they would all generate identically-named types and collide. Only
/// <see cref="Interface"/> composes with a concrete kind (its names carry an <c>I</c> prefix).
/// <see cref="Partial"/> with no kind is rejected with error <c>FAC102</c>.
/// </summary>
[Flags]
public enum OutputType
{
    /// <summary>
    /// Generates nothing. Present only as the required zero value for a flags enum;
    /// specify at least one output kind.
    /// </summary>
    None = 0,
    Class = 1,
    Record = 2,
    Struct = 4,
    RecordStruct = 8,
    /// <summary>
    /// Generates an interface declaring the entity-mapped properties as get-only members.
    /// Useful when you want compile-time enforcement that a hand-written DTO contains all
    /// the entity's properties (the DTO declares <c>: IMyEntityCreateRequest</c> and the
    /// compiler fails until every interface member is satisfied) without surrendering the
    /// DTO's own shape (construction syntax, validation attributes, extra non-entity fields).
    /// When combined with a concrete kind (e.g. <c>Interface | Record</c>), the concrete output
    /// declares the generated interface as a base, so consuming code can accept the interface —
    /// which also makes request DTOs easy to mock in tests (e.g. with Moq or NSubstitute)
    /// instead of constructing full concrete instances.
    /// Constructors, projections, and ToSource methods are not emitted on interface output.
    /// Not supported for Patch DTOs (their ApplyTo method requires a concrete implementation).
    /// </summary>
    Interface = 16,
    /// <summary>
    /// Modifier, not a kind: emits every requested kind as <c>partial</c> so a hand-written
    /// partial half in the same project can extend it — validation attributes, computed
    /// members, custom constructors, or mapping logic. Constructors are still generated for
    /// concrete kinds, but projection, <c>ToSource</c>, and <c>BackTo</c> are omitted: a
    /// hand-written half may add members the generator can't see, so a generator-owned
    /// mapping would be silently incomplete. Applies to <see cref="Interface"/> too
    /// (<c>partial interface</c>), making generated contracts user-extensible.
    /// Must be combined with at least one kind; <see cref="Partial"/> alone is rejected
    /// with error <c>FAC102</c>.
    /// </summary>
    Partial = 32,
    /// <summary>
    /// Back-compat alias for <c>Class | Partial</c>. Prefer composing the
    /// <see cref="Partial"/> modifier explicitly.
    /// </summary>
    PartialClass = Class | Partial
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
    /// The output kind(s) for generated DTOs (default: Record). This is a flags value:
    /// combine kinds to emit several outputs from this single attribute, sharing every
    /// other option — e.g. <c>OutputType.Interface | OutputType.PartialClass</c> emits
    /// the contract + implementation pair (the partial class declares the generated
    /// interface as a base) without duplicating the attribute.
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
    /// When true, automatically excludes navigation properties: properties whose type
    /// (or collection element type, for any <see cref="System.Collections.Generic.IEnumerable{T}"/>
    /// other than <see cref="string"/>, with dictionary key/value types unwrapped) is a class
    /// or interface declared in the same assembly as the source type. This removes ORM
    /// navigation/back-reference properties from generated DTOs without listing each one in
    /// <see cref="ExcludeProperties"/>. Scalar properties — primitives, enums, framework
    /// types, and user-defined value types such as strongly-typed ID structs — are always
    /// kept. Aggregate children that should stay in the DTO despite matching the heuristic
    /// can be forced back in via <see cref="IncludeProperties"/>. Default is false.
    /// <para>
    /// For EF Core entities the heuristic can be replaced by the model's own designation:
    /// when a <c>*.facetmodel</c> manifest (written beside the model snapshot by
    /// Facet.Extensions.EFCore's design-time services on every <c>dotnet ef migrations
    /// add</c>/<c>remove</c>) is exposed as an AdditionalFile and lists the source type,
    /// exactly the properties EF maps as data are kept — so value-converted columns survive
    /// and EF-ignored properties drop, in both cases unlike the heuristic. Types not listed
    /// in any manifest keep the heuristic behavior.
    /// </para>
    /// </summary>
    public bool ExcludeNavigationProperties { get; set; } = false;

    /// <summary>
    /// Properties to keep in every generated DTO regardless of <see cref="ExcludeProperties"/>,
    /// <see cref="ExcludeAuditFields"/>, or <see cref="ExcludeNavigationProperties"/> — the
    /// escape hatch for aggregate children (e.g. an owned parameter collection edited together
    /// with its parent) that the navigation heuristic would otherwise drop.
    /// </summary>
    public string[] IncludeProperties { get; set; } = Array.Empty<string>();

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
/// Assembly-level counterpart of <see cref="GenerateDtosAttribute"/>: generates DTOs for
/// <see cref="SourceType"/> into the assembly that DECLARES this attribute rather than the
/// assembly that declares the entity. Place it in the project where the DTOs should live
/// (e.g. a web-contracts layer referencing the domain), keeping the domain assembly free of
/// generated contract types:
/// <code>
/// [assembly: GenerateDtosFor(typeof(Schedule),
///     Types = DtoTypes.Create | DtoTypes.Update,
///     OutputType = OutputType.Interface | OutputType.Record | OutputType.Partial)]
/// </code>
/// All options of <see cref="GenerateDtosAttribute"/> apply. Interface/concrete sibling
/// pairing links outputs for the same <see cref="SourceType"/> only.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GenerateDtosForAttribute : GenerateDtosAttribute
{
    /// <summary>The entity type (typically from a referenced assembly) to generate DTOs for.</summary>
    public Type SourceType { get; }

    public GenerateDtosForAttribute(Type sourceType)
    {
        SourceType = sourceType;
    }
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
