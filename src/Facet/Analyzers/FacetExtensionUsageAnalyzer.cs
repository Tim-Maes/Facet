using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FacetExtensionUsageAnalyzer : DiagnosticAnalyzer
{
    // Diagnostic descriptors for different error scenarios
    public static readonly DiagnosticDescriptor ToFacetTargetNotFacetRule = new DiagnosticDescriptor(
        "FAC001",
        "ToFacet target type must be annotated with [Facet]",
        "Type '{0}' must be annotated with [Facet] attribute to be used as target in ToFacet method",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The target type in ToFacet<TTarget> or ToFacet<TSource, TTarget> must be a type annotated with [Facet] attribute.");

    public static readonly DiagnosticDescriptor BackToFacetNotFacetRule = new DiagnosticDescriptor(
        "FAC002",
        "BackTo facet type must be annotated with [Facet]",
        "Type '{0}' must be annotated with [Facet] attribute to be used as facet in BackTo method",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The facet type in BackTo<TFacet, TFacetSource> must be a type annotated with [Facet] attribute.");

    public static readonly DiagnosticDescriptor BackToObjectNotFacetRule = new DiagnosticDescriptor(
        "FAC003",
        "BackTo object must be a facet type",
        "The object used with BackTo<TFacetSource> must be of a type annotated with [Facet] attribute",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When using BackTo<TFacetSource>(this object facet), the object must be of a type annotated with [Facet] attribute.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(ToFacetTargetNotFacetRule, BackToFacetNotFacetRule, BackToObjectNotFacetRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess == null) return;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
        if (symbolInfo.Symbol is not IMethodSymbol method) return;

        // Check if this is a call to one of the Facet extension methods
        if (method.ContainingType?.ToDisplayString() != "Facet.Extensions.FacetExtensions") return;

        switch (method.Name)
        {
            case "ToFacet":
                AnalyzeToFacetCall(context, method, invocation);
                break;
            case "BackTo":
                AnalyzeBackToCall(context, method, invocation, memberAccess);
                break;
        }
    }

    private static void AnalyzeToFacetCall(SyntaxNodeAnalysisContext context, IMethodSymbol method, InvocationExpressionSyntax invocation)
    {
        // Check both ToFacet<TTarget> and ToFacet<TSource, TTarget>
        if (method.TypeArguments.Length == 0) return;

        ITypeSymbol targetType;
        if (method.TypeArguments.Length == 1)
        {
            // ToFacet<TTarget>(this object source)
            targetType = method.TypeArguments[0];
        }
        else if (method.TypeArguments.Length == 2)
        {
            // ToFacet<TSource, TTarget>(this TSource source)
            targetType = method.TypeArguments[1];
        }
        else
        {
            return;
        }

        if (!HasFacetAttribute(targetType))
        {
            var diagnostic = Diagnostic.Create(
                ToFacetTargetNotFacetRule,
                invocation.GetLocation(),
                targetType.ToDisplayString());
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeBackToCall(SyntaxNodeAnalysisContext context, IMethodSymbol method, InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax memberAccess)
    {
        if (method.TypeArguments.Length == 0) return;

        if (method.TypeArguments.Length == 2)
        {
            // BackTo<TFacet, TFacetSource>(this TFacet facet)
            var facetType = method.TypeArguments[0];
            if (!HasFacetAttribute(facetType))
            {
                var diagnostic = Diagnostic.Create(
                    BackToFacetNotFacetRule,
                    invocation.GetLocation(),
                    facetType.ToDisplayString());
                context.ReportDiagnostic(diagnostic);
            }
        }
        else if (method.TypeArguments.Length == 1)
        {
            // BackTo<TFacetSource>(this object facet)
            // We need to check the actual type of the object being called on
            var objectExpression = memberAccess.Expression;
            var objectTypeInfo = context.SemanticModel.GetTypeInfo(objectExpression);
            
            if (objectTypeInfo.Type != null && !HasFacetAttribute(objectTypeInfo.Type))
            {
                var diagnostic = Diagnostic.Create(
                    BackToObjectNotFacetRule,
                    invocation.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool HasFacetAttribute(ITypeSymbol type)
    {
        return type.GetAttributes().Any(attr => 
            attr.AttributeClass?.ToDisplayString() == "Facet.FacetAttribute");
    }
}
