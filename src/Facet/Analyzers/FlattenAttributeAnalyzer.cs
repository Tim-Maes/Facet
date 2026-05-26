using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Analyzers;

/// <summary>
/// Analyzer that validates proper usage of the [Flatten] attribute.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FlattenAttributeAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor MissingPartialKeywordRule = new DiagnosticDescriptor(
        "FAC014",
        "Type with [Flatten] attribute must be declared as partial",
        "Type '{0}' is marked with [Flatten] but is not declared as partial",
        "Declaration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types marked with [Flatten] must be partial to allow the source generator to add generated members.");

    public static readonly DiagnosticDescriptor InvalidSourceTypeRule = new DiagnosticDescriptor(
        "FAC015",
        "Source type is not accessible or does not exist",
        "Source type '{0}' could not be resolved or is not accessible",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The source type specified in the [Flatten] attribute must be a valid, accessible type.");

    public static readonly DiagnosticDescriptor MaxDepthWarningRule = new DiagnosticDescriptor(
        "FAC016",
        "MaxDepth value is unusual",
        "MaxDepth is set to {0}: {1}",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "MaxDepth values should typically be between 1 and 5 for flatten scenarios.");

    public static readonly DiagnosticDescriptor LeafOnlyCollisionWarningRule = new DiagnosticDescriptor(
        "FAC017",
        "LeafOnly naming strategy may cause property name collisions",
        "Using LeafOnly naming strategy may cause property name collisions if nested objects have properties with the same name",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "The LeafOnly naming strategy can cause name collisions when multiple nested objects have properties with the same name. Consider using Prefix strategy instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        MissingPartialKeywordRule,
        InvalidSourceTypeRule,
        MaxDepthWarningRule,
        LeafOnlyCollisionWarningRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        var flattenAttributes = namedType.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() == "Facet.FlattenAttribute")
            .ToList();

        if (!flattenAttributes.Any())
            return;

        if (!IsPartialType(namedType))
        {
            var diagnostic = Diagnostic.Create(
                MissingPartialKeywordRule,
                namedType.Locations.FirstOrDefault(),
                namedType.Name);
            context.ReportDiagnostic(diagnostic);
        }

        foreach (var flattenAttr in flattenAttributes)
        {
            AnalyzeFlattenAttribute(context, namedType, flattenAttr);
        }
    }

    private static void AnalyzeFlattenAttribute(SymbolAnalysisContext context, INamedTypeSymbol targetType, AttributeData flattenAttr)
    {
        if (flattenAttr.ConstructorArguments.Length == 0)
            return;

        var sourceTypeArg = flattenAttr.ConstructorArguments[0];
        if (sourceTypeArg.Value is not INamedTypeSymbol sourceType)
        {
            var diagnostic = Diagnostic.Create(
                InvalidSourceTypeRule,
                flattenAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                sourceTypeArg.ToCSharpString());
            context.ReportDiagnostic(diagnostic);
            return;
        }

        if (sourceType.TypeKind == TypeKind.Error)
        {
            var diagnostic = Diagnostic.Create(
                InvalidSourceTypeRule,
                flattenAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                sourceType.ToDisplayString());
            context.ReportDiagnostic(diagnostic);
            return;
        }

        var maxDepthArg = flattenAttr.NamedArguments.FirstOrDefault(a => a.Key == "MaxDepth");
        if (!maxDepthArg.Equals(default) && maxDepthArg.Value.Value is int maxDepthValue)
        {
            if (maxDepthValue < 0)
            {
                var diagnostic = Diagnostic.Create(
                    MaxDepthWarningRule,
                    flattenAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                    maxDepthValue,
                    "MaxDepth cannot be negative");
                context.ReportDiagnostic(diagnostic);
            }
            else if (maxDepthValue > 10)
            {
                var diagnostic = Diagnostic.Create(
                    MaxDepthWarningRule,
                    flattenAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                    maxDepthValue,
                    "This value is unusually large for flattening. Consider using a value between 1 and 5");
                context.ReportDiagnostic(diagnostic);
            }
        }

        var namingStrategyArg = flattenAttr.NamedArguments.FirstOrDefault(a => a.Key == "NamingStrategy");
        if (!namingStrategyArg.Equals(default) && namingStrategyArg.Value.Value is int namingStrategyValue)
        {
            if (namingStrategyValue == 1) 
            {
                var diagnostic = Diagnostic.Create(
                    LeafOnlyCollisionWarningRule,
                    flattenAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
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
}
