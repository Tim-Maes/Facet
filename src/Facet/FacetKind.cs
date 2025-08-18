namespace Facet
{
    /// <summary>
    /// Determines the generated facet type:
    /// - Auto: automatically detect the kind from the target type declaration.
    /// - Class: mutable class with properties/fields.
    /// - Record: immutable record class (primary constructor + init-only props).
    /// - RecordStruct: immutable record struct.
    /// - Struct: mutable struct with properties/fields.
    /// </summary>
    public enum FacetKind
    {
        Auto = -1,
        Class = 0,
        Record = 1,
        RecordStruct = 2,
        Struct = 3
    }
}
