using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Analyzers;

/// <summary>
/// Analyzer that validates proper usage of the [Wrapper] attribute.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class WrapperAttributeAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor MissingPartialKeywordRule = new DiagnosticDescriptor(
        "FAC018",
        "Type with [Wrapper] attribute must be declared as partial",
        "Type '{0}' is marked with [Wrapper] but is not declared as partial",
        "Declaration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types marked with [Wrapper] must be partial to allow the source generator to add generated members.");

    public static readonly DiagnosticDescriptor InvalidPropertyNameRule = new DiagnosticDescriptor(
        "FAC019",
        "Property name does not exist in source type",
        "Property '{0}' in {1} does not exist in source type '{2}'",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Property names in Exclude or Include parameters must match properties in the source type.");

    public static readonly DiagnosticDescriptor InvalidSourceTypeRule = new DiagnosticDescriptor(
        "FAC020",
        "Source type is not accessible or does not exist",
        "Source type '{0}' could not be resolved or is not accessible",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The source type specified in the [Wrapper] attribute must be a valid, accessible type.");

    public static readonly DiagnosticDescriptor IncludeAndExcludeBothSpecifiedRule = new DiagnosticDescriptor(
        "FAC021",
        "Cannot specify both Include and Exclude",
        "Cannot specify both Include and Exclude parameters",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Include and Exclude parameters are mutually exclusive.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        MissingPartialKeywordRule,
        InvalidPropertyNameRule,
        InvalidSourceTypeRule,
        IncludeAndExcludeBothSpecifiedRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        var wrapperAttributes = namedType.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() == "Facet.WrapperAttribute")
            .ToList();

        if (!wrapperAttributes.Any())
            return;

        if (!IsPartialType(namedType))
        {
            var diagnostic = Diagnostic.Create(
                MissingPartialKeywordRule,
                namedType.Locations.FirstOrDefault(),
                namedType.Name);
            context.ReportDiagnostic(diagnostic);
        }

        foreach (var wrapperAttr in wrapperAttributes)
        {
            AnalyzeWrapperAttribute(context, namedType, wrapperAttr);
        }
    }

    private static void AnalyzeWrapperAttribute(SymbolAnalysisContext context, INamedTypeSymbol targetType, AttributeData wrapperAttr)
    {
        if (wrapperAttr.ConstructorArguments.Length == 0)
            return;

        var sourceTypeArg = wrapperAttr.ConstructorArguments[0];
        if (sourceTypeArg.Value is not INamedTypeSymbol sourceType)
        {
            var diagnostic = Diagnostic.Create(
                InvalidSourceTypeRule,
                wrapperAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                sourceTypeArg.ToCSharpString());
            context.ReportDiagnostic(diagnostic);
            return;
        }

        if (sourceType.TypeKind == TypeKind.Error)
        {
            var diagnostic = Diagnostic.Create(
                InvalidSourceTypeRule,
                wrapperAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                sourceType.ToDisplayString());
            context.ReportDiagnostic(diagnostic);
            return;
        }

        var sourceMembers = new HashSet<string>(GetAllPublicMembers(sourceType)
            .Select(m => m.Name));

        if (wrapperAttr.ConstructorArguments.Length > 1)
        {
            var excludeArg = wrapperAttr.ConstructorArguments[1];
            if (!excludeArg.IsNull && excludeArg.Kind == TypedConstantKind.Array)
            {
                foreach (var item in excludeArg.Values)
                {
                    if (item.Value is string propertyName && !string.IsNullOrEmpty(propertyName))
                    {
                        if (!sourceMembers.Contains(propertyName))
                        {
                            ReportInvalidPropertyName(context, wrapperAttr, propertyName, "Exclude", sourceType, sourceMembers);
                        }
                    }
                }
            }
        }

        var includeArg = wrapperAttr.NamedArguments.FirstOrDefault(a => a.Key == "Include");

        if (!includeArg.Equals(default) && !includeArg.Value.IsNull && includeArg.Value.Kind == TypedConstantKind.Array)
        {
            bool hasExclude = wrapperAttr.ConstructorArguments.Length > 1 &&
                             !wrapperAttr.ConstructorArguments[1].IsNull &&
                             wrapperAttr.ConstructorArguments[1].Values.Length > 0;

            if (hasExclude)
            {
                var diagnostic = Diagnostic.Create(
                    IncludeAndExcludeBothSpecifiedRule,
                    wrapperAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation());
                context.ReportDiagnostic(diagnostic);
            }

            foreach (var item in includeArg.Value.Values)
            {
                if (item.Value is string propertyName && !string.IsNullOrEmpty(propertyName))
                {
                    if (!sourceMembers.Contains(propertyName))
                    {
                        ReportInvalidPropertyName(context, wrapperAttr, propertyName, "Include", sourceType, sourceMembers);
                    }
                }
            }
        }
    }

    private static void ReportInvalidPropertyName(
        SymbolAnalysisContext context,
        AttributeData wrapperAttr,
        string propertyName,
        string parameterName,
        INamedTypeSymbol sourceType,
        HashSet<string> validProperties)
    {
        var diagnostic = Diagnostic.Create(
            InvalidPropertyNameRule,
            wrapperAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
            propertyName,
            parameterName,
            sourceType.ToDisplayString());
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsPartialType(INamedTypeSymbol type)
    {
        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();
            if (syntax is TypeDeclarationSyntax typeDecl)
            {
                if (typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static IEnumerable<ISymbol> GetAllPublicMembers(INamedTypeSymbol type)
    {
        var visited = new HashSet<string>();
        var current = type;

        while (current != null)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.DeclaredAccessibility == Accessibility.Public &&
                    !visited.Contains(member.Name) &&
                    (member.Kind == SymbolKind.Property || member.Kind == SymbolKind.Field))
                {
                    visited.Add(member.Name);
                    yield return member;
                }
            }

            current = current.BaseType;

            if (current?.SpecialType == SpecialType.System_Object)
                break;
        }
    }
}
