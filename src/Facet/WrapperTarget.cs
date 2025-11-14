using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet;

internal sealed class WrapperTargetModel : IEquatable<WrapperTargetModel>
{
    public string Name { get; }
    public string? Namespace { get; }
    public string FullName { get; }
    public TypeKind TypeKind { get; }
    public bool IsRecord { get; }
    public string SourceTypeName { get; }
    public ImmutableArray<string> SourceContainingTypes { get; }
    public ImmutableArray<FacetMember> Members { get; }
    public string? TypeXmlDocumentation { get; }
    public ImmutableArray<string> ContainingTypes { get; }
    public bool UseFullName { get; }
    public bool CopyAttributes { get; }
    public string SourceFieldName { get; }

    public WrapperTargetModel(
        string name,
        string? @namespace,
        string fullName,
        TypeKind typeKind,
        bool isRecord,
        string sourceTypeName,
        ImmutableArray<string> sourceContainingTypes,
        ImmutableArray<FacetMember> members,
        string? typeXmlDocumentation = null,
        ImmutableArray<string> containingTypes = default,
        bool useFullName = false,
        bool copyAttributes = false,
        string sourceFieldName = "_source")
    {
        Name = name;
        Namespace = @namespace;
        FullName = fullName;
        TypeKind = typeKind;
        IsRecord = isRecord;
        SourceTypeName = sourceTypeName;
        SourceContainingTypes = sourceContainingTypes.IsDefault ? ImmutableArray<string>.Empty : sourceContainingTypes;
        Members = members;
        TypeXmlDocumentation = typeXmlDocumentation;
        ContainingTypes = containingTypes.IsDefault ? ImmutableArray<string>.Empty : containingTypes;
        UseFullName = useFullName;
        CopyAttributes = copyAttributes;
        SourceFieldName = sourceFieldName;
    }

    public bool Equals(WrapperTargetModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name == other.Name
            && Namespace == other.Namespace
            && FullName == other.FullName
            && TypeKind == other.TypeKind
            && IsRecord == other.IsRecord
            && SourceTypeName == other.SourceTypeName
            && SourceContainingTypes.SequenceEqual(other.SourceContainingTypes)
            && TypeXmlDocumentation == other.TypeXmlDocumentation
            && Members.SequenceEqual(other.Members)
            && ContainingTypes.SequenceEqual(other.ContainingTypes)
            && UseFullName == other.UseFullName
            && CopyAttributes == other.CopyAttributes
            && SourceFieldName == other.SourceFieldName;
    }

    public override bool Equals(object? obj) => obj is WrapperTargetModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (Name?.GetHashCode() ?? 0);
            hash = hash * 31 + (Namespace?.GetHashCode() ?? 0);
            hash = hash * 31 + (FullName?.GetHashCode() ?? 0);
            hash = hash * 31 + TypeKind.GetHashCode();
            hash = hash * 31 + IsRecord.GetHashCode();
            hash = hash * 31 + (SourceTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (TypeXmlDocumentation?.GetHashCode() ?? 0);
            hash = hash * 31 + UseFullName.GetHashCode();
            hash = hash * 31 + CopyAttributes.GetHashCode();
            hash = hash * 31 + (SourceFieldName?.GetHashCode() ?? 0);
            hash = hash * 31 + Members.Length.GetHashCode();

            foreach (var member in Members)
                hash = hash * 31 + member.GetHashCode();

            foreach (var containingType in ContainingTypes)
                hash = hash * 31 + (containingType?.GetHashCode() ?? 0);

            foreach (var sourceContainingType in SourceContainingTypes)
                hash = hash * 31 + (sourceContainingType?.GetHashCode() ?? 0);

            return hash;
        }
    }

    internal static IEqualityComparer<WrapperTargetModel> Comparer { get; } = new WrapperTargetModelEqualityComparer();

    internal sealed class WrapperTargetModelEqualityComparer : IEqualityComparer<WrapperTargetModel>
    {
        public bool Equals(WrapperTargetModel? x, WrapperTargetModel? y) => x?.Equals(y) ?? y is null;
        public int GetHashCode(WrapperTargetModel obj) => obj.GetHashCode();
    }
}
