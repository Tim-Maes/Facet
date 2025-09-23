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
    public static readonly DiagnosticDescriptor TargetNotFacetRule = new DiagnosticDescriptor(
        "FAC001",
        "Type must be annotated with [Facet]",
        "Type '{1}' must be annotated with [Facet] attribute to be used here in {0} method",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SingleGenericPerformanceRule = new DiagnosticDescriptor(
        "FAC002",
        "Consider using the two-generic variant of this method for better performance",
        "Consider using {0}<{1}, {2}> instead of {0}<{2}> for better performance",
        "Performance",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [TargetNotFacetRule, SingleGenericPerformanceRule];

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
                AnalyzeToFacetCall(context, method, invocation, memberAccess, false);
                break;
            case "BackTo":
                AnalyzeBackToCall(context, method, invocation, memberAccess, false);
                break;
            case "SelectFacets":
                AnalyzeToFacetCall(context, method, invocation, memberAccess, true);
                break;
            case "SelectFacetSources":
                AnalyzeBackToCall(context, method, invocation, memberAccess, true);
                break;
        }
    }

    private static void AnalyzeToFacetCall(SyntaxNodeAnalysisContext context, IMethodSymbol method, InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax memberAccess, bool isCollection)
    {
        // Check both ToFacet<TTarget> and ToFacet<TSource, TTarget>
        if (method.TypeArguments.Length == 0) return;

        ITypeSymbol targetType;
        if (method.TypeArguments.Length == 1)
        {
            // ToFacet<TTarget>(this object source)
            targetType = method.TypeArguments[0];
            
            // We need to check the actual type of the object being called on
            var objectExpression = memberAccess.Expression;
            var objectTypeInfo = context.SemanticModel.GetTypeInfo(objectExpression);
            var sourceElementType = isCollection ? GetCollectionElementType(objectTypeInfo.Type) : objectTypeInfo.Type;
            
            context.ReportDiagnostic(Diagnostic.Create(
                SingleGenericPerformanceRule,
                invocation.GetLocation(),
                method.Name, sourceElementType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), targetType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
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
                TargetNotFacetRule,
                invocation.GetLocation(),
                method.Name, targetType.ToDisplayString());
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeBackToCall(SyntaxNodeAnalysisContext context, IMethodSymbol method, InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax memberAccess, bool isCollection)
    {
        if (method.TypeArguments.Length == 0) return;

        if (method.TypeArguments.Length == 2)
        {
            // BackTo<TFacet, TFacetSource>(this TFacet facet)
            var facetType = method.TypeArguments[0];
            if (!HasFacetAttribute(facetType))
            {
                var diagnostic = Diagnostic.Create(
                    TargetNotFacetRule,
                    invocation.GetLocation(),
                    method.Name, facetType.ToDisplayString());
                context.ReportDiagnostic(diagnostic);
            }
        }
        else if (method.TypeArguments.Length == 1)
        {
            // BackTo<TFacetSource>(this object facet)
            // We need to check the actual type of the object being called on
            var objectExpression = memberAccess.Expression;
            var objectTypeInfo = context.SemanticModel.GetTypeInfo(objectExpression);
            var sourceElementType = isCollection ? GetCollectionElementType(objectTypeInfo.Type) : objectTypeInfo.Type;
            
            var targetType = method.TypeArguments[0];
            
            context.ReportDiagnostic(Diagnostic.Create(
                SingleGenericPerformanceRule,
                invocation.GetLocation(),
                method.Name, sourceElementType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), targetType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            
            if (sourceElementType != null && !HasFacetAttribute(sourceElementType))
            {
                var diagnostic = Diagnostic.Create(
                    TargetNotFacetRule,
                    invocation.GetLocation(),
                    method.Name, objectTypeInfo.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
    
    private static ITypeSymbol? GetCollectionElementType(ITypeSymbol? collectionType)
    {
        if (collectionType == null) return null;

        // Check if it's an array type
        if (collectionType is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType;
        }

        // Check if it's a generic type that implements IEnumerable<T>
        if (collectionType is not INamedTypeSymbol namedType || !namedType.IsGenericType) return collectionType;
        
        // First check if it's directly IEnumerable<T>
        if (namedType.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
        {
            return namedType.TypeArguments[0];
        }

        // Check if it implements IEnumerable<T>
        foreach (var iface in namedType.AllInterfaces)
        {
            if (iface.IsGenericType && 
                iface.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
            {
                return iface.TypeArguments[0];
            }
        }

        return collectionType;
    }

    private static bool HasFacetAttribute(ITypeSymbol type)
    {
        return type.GetAttributes().Any(attr => 
            attr.AttributeClass?.ToDisplayString() == "Facet.FacetAttribute");
    }
}
