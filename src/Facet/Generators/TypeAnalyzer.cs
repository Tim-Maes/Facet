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
            containingTypes.Insert(0, current.Name); // Insert at beginning to maintain order
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
        // Check if this is a record type with an existing primary constructor
        if (targetSymbol.TypeKind == TypeKind.Class || targetSymbol.TypeKind == TypeKind.Struct)
        {
            // Look at the syntax to see if it has primary constructor parameters
            var syntaxRef = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef != null)
            {
                var syntax = syntaxRef.GetSyntax();

                // Check for record with parameter list
                if (syntax is RecordDeclarationSyntax recordDecl && recordDecl.ParameterList != null && recordDecl.ParameterList.Parameters.Count > 0)
                {
                    return true;
                }

                // Check for regular class/struct with primary constructor (C# 12+)
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
    /// Checks if the source type has a positional constructor (like records with primary constructors).
    /// </summary>
    public static bool HasPositionalConstructor(INamedTypeSymbol sourceType)
    {
        if (sourceType.TypeKind == TypeKind.Class || sourceType.TypeKind == TypeKind.Struct)
        {
            // Look at the syntax to see if it has primary constructor parameters
            var syntaxRef = sourceType.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef != null)
            {
                var syntax = syntaxRef.GetSyntax();

                // Check for record with parameter list
                if (syntax is RecordDeclarationSyntax recordDecl && recordDecl.ParameterList != null && recordDecl.ParameterList.Parameters.Count > 0)
                {
                    return true;
                }

                // Check for regular class/struct with primary constructor (C# 12+)
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
            if (syntax != null)
            {
                var syntaxText = syntax.ToString();
                if (syntaxText.Contains("record struct") || syntaxText.Contains("record "))
                {
                    isRecord = true;
                }
            }

            // Additional check for records by looking for the compiler-generated Clone method
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
}
