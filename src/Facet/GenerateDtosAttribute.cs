using System;

namespace Facet
{
    /// <summary>
    /// Generates multiple DTO variants (Create, Update, Response, Query) from a model class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class GenerateDtosAttribute : Attribute
    {
        /// <summary>
        /// Which DTO types to generate. Default is All (Create, Update, Response, Query).
        /// </summary>
        public DtoTypes Types { get; set; } = DtoTypes.All;

        /// <summary>
        /// The output type for generated DTOs. Default is Record.
        /// </summary>
        public FacetKind OutputType { get; set; } = FacetKind.Record;

        /// <summary>
        /// Properties to exclude from all generated DTOs.
        /// </summary>
        public string[] ExcludeProperties { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Namespace for generated DTOs. If null, uses the source type's namespace.
        /// </summary>
        public string? Namespace { get; set; }

        /// <summary>
        /// Naming convention for generated DTOs. Default is Convention.
        /// </summary>
        public DtoNamingConvention NamingConvention { get; set; } = DtoNamingConvention.Convention;

        /// <summary>
        /// Custom prefix for generated DTO names. Only used with Custom naming convention.
        /// </summary>
        public string? CustomPrefix { get; set; }

        /// <summary>
        /// Custom suffix for generated DTO names. Only used with Custom naming convention.
        /// </summary>
        public string? CustomSuffix { get; set; }

        /// <summary>
        /// Whether to include public fields from the source type (default: false).
        /// </summary>
        public bool IncludeFields { get; set; } = false;

        /// <summary>
        /// Whether to generate constructors that accept the source type and copy over matching members.
        /// </summary>
        public bool GenerateConstructor { get; set; } = true;

        /// <summary>
        /// Whether to generate the static Expression&lt;Func&lt;TSource,TTarget&gt;&gt; Projection.
        /// Default is true so you always get a Projection by default.
        /// </summary>
        public bool GenerateProjection { get; set; } = true;

        /// <summary>
        /// Controls whether generated properties should preserve init-only modifiers from source properties.
        /// When true, properties with init accessors in the source will be generated as init-only in the target.
        /// Defaults to true for record and record struct types, false for class and struct types.
        /// </summary>
        public bool PreserveInitOnlyProperties { get; set; } = false;

        /// <summary>
        /// Controls whether generated properties should preserve required modifiers from source properties.
        /// When true, properties marked as required in the source will be generated as required in the target.
        /// Defaults to true for record and record struct types, false for class and struct types.
        /// </summary>
        public bool PreserveRequiredProperties { get; set; } = false;
    }

    /// <summary>
    /// Generates DTO variants for auditable entities with common audit fields automatically excluded.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class GenerateAuditableDtosAttribute : GenerateDtosAttribute
    {
        public GenerateAuditableDtosAttribute()
        {
            // Automatically exclude common audit fields
            ExcludeProperties = new[]
            {
                "CreatedAt", "CreatedDate", "CreatedOn", "CreatedBy",
                "UpdatedAt", "UpdatedDate", "UpdatedOn", "UpdatedBy", "ModifiedBy",
                "DeletedAt", "DeletedDate", "DeletedOn", "DeletedBy"
            };
        }
    }

    [Flags]
    public enum DtoTypes
    {
        Create = 1,
        Update = 2,
        Response = 4,
        Query = 8,
        All = Create | Update | Response | Query
    }

    public enum DtoNamingConvention
    {
        /// <summary>
        /// Uses conventional naming: Create{Model}Request, Update{Model}Request, {Model}Response, {Model}Query
        /// </summary>
        Convention,
        
        /// <summary>
        /// Uses custom prefix/suffix specified in CustomPrefix and CustomSuffix properties
        /// </summary>
        Custom
    }
}