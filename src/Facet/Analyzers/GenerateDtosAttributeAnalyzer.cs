using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Analyzers;

/// <summary>
/// Analyzer that validates proper usage of the [GenerateDtos] and [GenerateAuditableDtos] attributes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GenerateDtosAttributeAnalyzer : DiagnosticAnalyzer
{
    // FAC011: GenerateDtos on non-class type
    public static readonly DiagnosticDescriptor NonClassTypeRule = new DiagnosticDescriptor(
        "FAC011",
        "[GenerateDtos] can only be applied to classes",
        "[GenerateDtos] attribute can only be applied to class types, not {0}",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [GenerateDtos] attribute is designed for class types and cannot be applied to structs, interfaces, or other type kinds.");

    // FAC012: Invalid ExcludeProperties
    public static readonly DiagnosticDescriptor InvalidExcludePropertyRule = new DiagnosticDescriptor(
        "FAC012",
        "Excluded property does not exist",
        "Property '{0}' in ExcludeProperties does not exist in type '{1}'",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Properties specified in ExcludeProperties should exist in the source type.");

    // FAC013: DtoTypes.None specified
    public static readonly DiagnosticDescriptor NoDtoTypesRule = new DiagnosticDescriptor(
        "FAC013",
        "No DTO types selected for generation",
        "DtoTypes is set to None, which will not generate any DTOs",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Setting DtoTypes to None will not generate any DTOs. Consider specifying the DTO types you want to generate.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        NonClassTypeRule,
        InvalidExcludePropertyRule,
        NoDtoTypesRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        // Find [GenerateDtos] or [GenerateAuditableDtos] attributes
        var dtoAttributes = namedType.GetAttributes()
            .Where(attr =>
                attr.AttributeClass?.ToDisplayString() == "Facet.GenerateDtosAttribute" ||
                attr.AttributeClass?.ToDisplayString() == "Facet.GenerateAuditableDtosAttribute")
            .ToList();

        if (!dtoAttributes.Any())
            return;

        // Check if type is a class
        if (namedType.TypeKind != TypeKind.Class)
        {
            foreach (var attr in dtoAttributes)
            {
                var diagnostic = Diagnostic.Create(
                    NonClassTypeRule,
                    attr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                    namedType.TypeKind.ToString().ToLowerInvariant());
                context.ReportDiagnostic(diagnostic);
            }
            return;
        }

        // Get all public properties from the type
        var typeProperties = new HashSet<string>(namedType.GetMembers()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public && m.Kind == SymbolKind.Property)
            .Select(m => m.Name));

        // Analyze each attribute
        foreach (var attr in dtoAttributes)
        {
            AnalyzeGenerateDtosAttribute(context, namedType, attr, typeProperties);
        }
    }

    private static void AnalyzeGenerateDtosAttribute(
        SymbolAnalysisContext context,
        INamedTypeSymbol targetType,
        AttributeData attr,
        HashSet<string> typeProperties)
    {
        // Check DtoTypes parameter
        var typesArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "Types");
        if (!typesArg.Equals(default) && typesArg.Value.Value is int typesValue)
        {
            if (typesValue == 0) // DtoTypes.None
            {
                var diagnostic = Diagnostic.Create(
                    NoDtoTypesRule,
                    attr.ApplicationSyntaxReference?.GetSyntax().GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        // Check ExcludeProperties parameter
        var excludeArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "ExcludeProperties");
        if (!excludeArg.Equals(default) && !excludeArg.Value.IsNull && excludeArg.Value.Kind == TypedConstantKind.Array)
        {
            foreach (var item in excludeArg.Value.Values)
            {
                if (item.Value is string propertyName && !string.IsNullOrEmpty(propertyName))
                {
                    if (!typeProperties.Contains(propertyName))
                    {
                        var diagnostic = Diagnostic.Create(
                            InvalidExcludePropertyRule,
                            attr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                            propertyName,
                            targetType.ToDisplayString());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
