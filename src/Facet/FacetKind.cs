namespace Facet
{
    /// <summary>
    /// Determines the generated facet type:
    /// - Class: mutable class with properties/fields.
    /// - Record: immutable record class (primary constructor + init-only props).
    /// - RecordStruct: immutable record struct.
    /// - Struct: mutable struct with properties/fields.
    /// </summary>
    public enum FacetKind
    {
        Class = 0,
        Record = 1,
        RecordStruct = 2,
        Struct = 3
    }
}
