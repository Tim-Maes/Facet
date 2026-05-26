using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;

namespace Facet.Generators;

/// <summary>
/// Analyzes type characteristics such as containing types, primary constructors, and record detection.
/// </summary>
internal static class TypeAnalyzer
{
    /// <summary>
    /// Gets the containing types for nested classes, in order from outermost to innermost.
    /// </summary>
    public static ImmutableArray<string> GetContainingTypes(INamedTypeSymbol targetSymbol)
    {
        var containingTypes = new List<string>();
        var current = targetSymbol.ContainingType;

        while (current != null)
        {
            containingTypes.Insert(0, current.Name); 
            current = current.ContainingType;
        }

        return containingTypes.ToImmutableArray();
    }

    /// <summary>
    /// Checks if the target type already has a primary constructor defined.
    /// For records, this means checking if the user has defined constructor parameters.
    /// </summary>
    public static bool HasExistingPrimaryConstructor(INamedTypeSymbol targetSymbol)
    {
        if (targetSymbol.TypeKind == TypeKind.Class || targetSymbol.TypeKind == TypeKind.Struct)
        {
            var syntaxRef = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef != null)
            {
                var syntax = syntaxRef.GetSyntax();

                if (syntax is RecordDeclarationSyntax recordDecl && recordDecl.ParameterList != null)
                {
                    return true;
                }

                if ((syntax is ClassDeclarationSyntax classDecl && classDecl.ParameterList != null) ||
                    (syntax is StructDeclarationSyntax structDecl && structDecl.ParameterList != null))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the source type has a positional constructor (like records with primary constructors).
    /// </summary>
    public static bool HasPositionalConstructor(INamedTypeSymbol sourceType)
    {
        if (sourceType.TypeKind == TypeKind.Class || sourceType.TypeKind == TypeKind.Struct)
        {
            var syntaxRef = sourceType.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef != null)
            {
                var syntax = syntaxRef.GetSyntax();

                if (syntax is RecordDeclarationSyntax recordDecl && recordDecl.ParameterList != null && recordDecl.ParameterList.Parameters.Count > 0)
                {
                    return true;
                }

                if ((syntax is ClassDeclarationSyntax classDecl && classDecl.ParameterList != null && classDecl.ParameterList.Parameters.Count > 0) ||
                    (syntax is StructDeclarationSyntax structDecl && structDecl.ParameterList != null && structDecl.ParameterList.Parameters.Count > 0))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Infers the TypeKind and whether it's a record from the target symbol's declaration.
    /// Returns a tuple of (TypeKind, IsRecord).
    /// </summary>
    public static (TypeKind typeKind, bool isRecord) InferTypeKind(INamedTypeSymbol targetSymbol)
    {
        var typeKind = targetSymbol.TypeKind;
        var isRecord = false;

        if (typeKind == TypeKind.Struct || typeKind == TypeKind.Class)
        {
            var syntax = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (syntax is RecordDeclarationSyntax)
            {
                isRecord = true;
            }

            if (!isRecord && typeKind == TypeKind.Class)
            {
                if (targetSymbol.GetMembers().Any(m => m.Name.Contains("Clone") && m.IsImplicitlyDeclared))
                {
                    isRecord = true;
                }
            }
        }

        return (typeKind, isRecord);
    }

    /// <summary>
    /// Checks if the source type has an accessible parameterless constructor.
    /// For ToSource generation, we need a public or internal parameterless constructor,
    /// or any parameterless constructor if the facet is nested inside the source type.
    /// </summary>
    public static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol sourceType, IAssemblySymbol? compilationAssembly = null, bool isNestedInSourceType = false)
    {
        var constructors = sourceType.InstanceConstructors;

        if (!constructors.Any())
            return true;

        foreach (var constructor in constructors)
        {
            if (constructor.Parameters.Length == 0)
            {
                if (constructor.DeclaredAccessibility == Accessibility.Public)
                    return true;

                // Nested facets can access private constructors on the containing source type.
                if (isNestedInSourceType)
                    return true;

                if (constructor.DeclaredAccessibility == Accessibility.Internal &&
                    compilationAssembly != null &&
                    IsInternalAccessible(sourceType.ContainingAssembly, compilationAssembly))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if all the specified properties have accessible setters.
    /// For ToSource generation using object initializer syntax, we need public or internal setters,
    /// or any setters if the facet is nested inside the source type.
    /// </summary>
    public static bool AllPropertiesHaveAccessibleSetters(
        INamedTypeSymbol sourceType,
        IEnumerable<FacetMember> members,
        bool isNestedInSourceType = false,
        IAssemblySymbol? compilationAssembly = null)
    {
        foreach (var member in members)
        {
            if (!member.MapFromReversible)
                continue;

            var sourceProperty = sourceType.GetMembers(member.SourcePropertyName)
                .OfType<IPropertySymbol>()
                .FirstOrDefault();

            if (sourceProperty == null)
                continue; 

            if (sourceProperty.SetMethod == null)
                return false; 

            // Nested facets can access private setters on the containing source type.
            if (isNestedInSourceType)
                continue;

            var setterAccessibility = sourceProperty.SetMethod.DeclaredAccessibility;
            if (setterAccessibility == Accessibility.Public)
                continue;

            // InternalsVisibleTo also makes internal setters assignable.
            if (setterAccessibility == Accessibility.Internal &&
                compilationAssembly != null &&
                IsInternalAccessible(sourceType.ContainingAssembly, compilationAssembly))
            {
                continue;
            }

            return false; 
        }

        return true;
    }

    /// <summary>
    /// Checks if the target type is nested inside the source type (at any depth).
    /// Nested types in C# have access to all members of their containing type, including private ones.
    /// </summary>
    public static bool IsNestedInsideType(INamedTypeSymbol innerType, INamedTypeSymbol outerType)
    {
        var current = innerType.ContainingType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, outerType))
                return true;
            current = current.ContainingType;
        }
        return false;
    }

    /// <summary>
    /// Checks if internal members of the source assembly are accessible from the compilation assembly.
    /// This is true when both assemblies are the same, or when the source assembly has an
    /// [InternalsVisibleTo] attribute that references the compilation assembly.
    /// </summary>
    public static bool IsInternalAccessible(IAssemblySymbol sourceAssembly, IAssemblySymbol compilationAssembly)
    {
        if (SymbolEqualityComparer.Default.Equals(sourceAssembly, compilationAssembly))
            return true;

        var compilationAssemblyName = compilationAssembly.Name;
        foreach (var attr in sourceAssembly.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "InternalsVisibleToAttribute" &&
                attr.AttributeClass.ContainingNamespace?.ToDisplayString() == "System.Runtime.CompilerServices" &&
                attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is string assemblyName)
            {
                var commaIndex = assemblyName.IndexOf(',');
                var name = commaIndex >= 0 ? assemblyName.Substring(0, commaIndex).Trim() : assemblyName.Trim();

                if (name == compilationAssemblyName)
                    return true;
            }
        }

        return false;
    }
}
