using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Facet.Analyzers;

/// <summary>
/// Analyzer that validates proper usage of the [Facet] attribute.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FacetAttributeAnalyzer : DiagnosticAnalyzer
{
    // FAC003: Missing partial keyword
    public static readonly DiagnosticDescriptor MissingPartialKeywordRule = new DiagnosticDescriptor(
        "FAC003",
        "Type with [Facet] attribute must be declared as partial",
        "Type '{0}' is marked with [Facet] but is not declared as partial",
        "Declaration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types marked with [Facet] must be partial to allow the source generator to add generated members.");

    // FAC004: Invalid Exclude/Include property names
    public static readonly DiagnosticDescriptor InvalidPropertyNameRule = new DiagnosticDescriptor(
        "FAC004",
        "Property name does not exist in source type",
        "Property '{0}' in {1} does not exist in source type '{2}'",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Property names in Exclude or Include parameters must match properties in the source type.");

    // FAC005: Invalid source type
    public static readonly DiagnosticDescriptor InvalidSourceTypeRule = new DiagnosticDescriptor(
        "FAC005",
        "Source type is not accessible or does not exist",
        "Source type '{0}' could not be resolved or is not accessible",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The source type specified in the [Facet] attribute must be a valid, accessible type.");

    // FAC006: Invalid Configuration type
    public static readonly DiagnosticDescriptor InvalidConfigurationTypeRule = new DiagnosticDescriptor(
        "FAC006",
        "Configuration type does not implement required interface",
        "Configuration type '{0}' must implement IFacetMapConfiguration or have a static Map method",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Configuration types must implement the appropriate IFacetMapConfiguration interface or provide a static Map method.");

    // FAC007: Invalid NestedFacets type
    public static readonly DiagnosticDescriptor InvalidNestedFacetRule = new DiagnosticDescriptor(
        "FAC007",
        "Nested facet type is not marked with [Facet] attribute",
        "Type '{0}' in NestedFacets must be marked with [Facet] attribute",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "All types specified in the NestedFacets array must be marked with the [Facet] attribute.");

    // FAC008: Circular reference warning
    public static readonly DiagnosticDescriptor CircularReferenceWarningRule = new DiagnosticDescriptor(
        "FAC008",
        "Potential stack overflow with circular references",
        "MaxDepth is 0 and PreserveReferences is false, which may cause stack overflow",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When working with nested facets, either MaxDepth or PreserveReferences should be enabled to prevent stack overflow.");

    // FAC009: Both Include and Exclude specified
    public static readonly DiagnosticDescriptor IncludeAndExcludeBothSpecifiedRule = new DiagnosticDescriptor(
        "FAC009",
        "Cannot specify both Include and Exclude",
        "Cannot specify both Include and Exclude parameters",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Include and Exclude parameters are mutually exclusive.");

    // FAC010: MaxDepth warning
    public static readonly DiagnosticDescriptor MaxDepthWarningRule = new DiagnosticDescriptor(
        "FAC010",
        "MaxDepth value is unusual",
        "MaxDepth is set to {0}: {1}",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "MaxDepth values should typically be between 1 and 10 for most scenarios.");

    // FAC022: Source signature mismatch
    public static readonly DiagnosticDescriptor SourceSignatureMismatchRule = new DiagnosticDescriptor(
        "FAC022",
        "Source entity structure changed",
        "Source entity '{0}' structure has changed. Update SourceSignature to '{1}' to acknowledge this change.",
        "Facet.SourceTracking",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The source entity's structure has changed since the SourceSignature was set. Review the changes and update the signature to acknowledge them.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        MissingPartialKeywordRule,
        InvalidPropertyNameRule,
        InvalidSourceTypeRule,
        InvalidConfigurationTypeRule,
        InvalidNestedFacetRule,
        CircularReferenceWarningRule,
        IncludeAndExcludeBothSpecifiedRule,
        MaxDepthWarningRule,
        SourceSignatureMismatchRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        // Find all [Facet] attributes on this type
        var facetAttributes = namedType.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() == "Facet.FacetAttribute")
            .ToList();

        if (!facetAttributes.Any())
            return;

        // Check if type is partial
        if (!IsPartialType(namedType))
        {
            var diagnostic = Diagnostic.Create(
                MissingPartialKeywordRule,
                namedType.Locations.FirstOrDefault(),
                namedType.Name);
            context.ReportDiagnostic(diagnostic);
        }

        // Analyze each [Facet] attribute
        foreach (var facetAttr in facetAttributes)
        {
            AnalyzeFacetAttribute(context, namedType, facetAttr);
        }
    }

    private static void AnalyzeFacetAttribute(SymbolAnalysisContext context, INamedTypeSymbol targetType, AttributeData facetAttr)
    {
        // Get the source type (first constructor argument)
        if (facetAttr.ConstructorArguments.Length == 0)
            return;

        var sourceTypeArg = facetAttr.ConstructorArguments[0];
        if (sourceTypeArg.Value is not INamedTypeSymbol sourceType)
        {
            // Invalid source type
            var diagnostic = Diagnostic.Create(
                InvalidSourceTypeRule,
                facetAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                sourceTypeArg.ToCSharpString());
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Check if source type is accessible
        if (sourceType.TypeKind == TypeKind.Error)
        {
            var diagnostic = Diagnostic.Create(
                InvalidSourceTypeRule,
                facetAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                sourceType.ToDisplayString());
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Get all public properties/fields from source type (including inherited)
        var sourceMembers = new HashSet<string>(GetAllPublicMembers(sourceType)
            .Select(m => m.Name));

        // Check Exclude parameter (constructor parameter)
        if (facetAttr.ConstructorArguments.Length > 1)
        {
            var excludeArg = facetAttr.ConstructorArguments[1];
            if (!excludeArg.IsNull && excludeArg.Kind == TypedConstantKind.Array)
            {
                foreach (var item in excludeArg.Values)
                {
                    if (item.Value is string propertyName && !string.IsNullOrEmpty(propertyName))
                    {
                        if (!sourceMembers.Contains(propertyName))
                        {
                            ReportInvalidPropertyName(context, facetAttr, propertyName, "Exclude", sourceType, sourceMembers);
                        }
                    }
                }
            }
        }

        // Check named arguments
        var includeArg = facetAttr.NamedArguments.FirstOrDefault(a => a.Key == "Include");
        var configurationArg = facetAttr.NamedArguments.FirstOrDefault(a => a.Key == "Configuration");
        var nestedFacetsArg = facetAttr.NamedArguments.FirstOrDefault(a => a.Key == "NestedFacets");
        var maxDepthArg = facetAttr.NamedArguments.FirstOrDefault(a => a.Key == "MaxDepth");
        var preserveReferencesArg = facetAttr.NamedArguments.FirstOrDefault(a => a.Key == "PreserveReferences");
        var sourceSignatureArg = facetAttr.NamedArguments.FirstOrDefault(a => a.Key == "SourceSignature");
        var includeFieldsArg = facetAttr.NamedArguments.FirstOrDefault(a => a.Key == "IncludeFields");

        // Check Include parameter
        if (!includeArg.Equals(default) && !includeArg.Value.IsNull && includeArg.Value.Kind == TypedConstantKind.Array)
        {
            // Check if both Include and Exclude are specified
            bool hasExclude = facetAttr.ConstructorArguments.Length > 1 &&
                             !facetAttr.ConstructorArguments[1].IsNull &&
                             facetAttr.ConstructorArguments[1].Values.Length > 0;

            if (hasExclude)
            {
                var diagnostic = Diagnostic.Create(
                    IncludeAndExcludeBothSpecifiedRule,
                    facetAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation());
                context.ReportDiagnostic(diagnostic);
            }

            foreach (var item in includeArg.Value.Values)
            {
                if (item.Value is string propertyName && !string.IsNullOrEmpty(propertyName))
                {
                    if (!sourceMembers.Contains(propertyName))
                    {
                        ReportInvalidPropertyName(context, facetAttr, propertyName, "Include", sourceType, sourceMembers);
                    }
                }
            }
        }

        // Check Configuration type
        if (!configurationArg.Equals(default) && !configurationArg.Value.IsNull)
        {
            if (configurationArg.Value.Value is INamedTypeSymbol configurationType)
            {
                if (!ImplementsConfigurationInterface(configurationType, sourceType, targetType))
                {
                    var diagnostic = Diagnostic.Create(
                        InvalidConfigurationTypeRule,
                        facetAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                        configurationType.ToDisplayString(),
                        sourceType.ToDisplayString(),
                        targetType.ToDisplayString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // Check NestedFacets
        if (!nestedFacetsArg.Equals(default) && !nestedFacetsArg.Value.IsNull && nestedFacetsArg.Value.Kind == TypedConstantKind.Array)
        {
            foreach (var item in nestedFacetsArg.Value.Values)
            {
                if (item.Value is INamedTypeSymbol nestedFacetType)
                {
                    if (!HasFacetAttribute(nestedFacetType))
                    {
                        var diagnostic = Diagnostic.Create(
                            InvalidNestedFacetRule,
                            facetAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                            nestedFacetType.ToDisplayString());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        // Check MaxDepth and PreserveReferences for circular reference safety
        int maxDepth = 10; // default
        bool preserveReferences = true; // default

        if (!maxDepthArg.Equals(default) && maxDepthArg.Value.Value is int maxDepthValue)
        {
            maxDepth = maxDepthValue;

            // Validate MaxDepth range
            if (maxDepthValue < 0)
            {
                var diagnostic = Diagnostic.Create(
                    MaxDepthWarningRule,
                    facetAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                    maxDepthValue,
                    "MaxDepth cannot be negative");
                context.ReportDiagnostic(diagnostic);
            }
            else if (maxDepthValue > 100)
            {
                var diagnostic = Diagnostic.Create(
                    MaxDepthWarningRule,
                    facetAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                    maxDepthValue,
                    "This value is unusually large and may indicate a configuration error. Consider using a value between 1 and 10");
                context.ReportDiagnostic(diagnostic);
            }
        }

        if (!preserveReferencesArg.Equals(default) && preserveReferencesArg.Value.Value is bool preserveReferencesValue)
        {
            preserveReferences = preserveReferencesValue;
        }

        // Check for circular reference risk
        bool hasNestedFacets = !nestedFacetsArg.Equals(default) &&
                              !nestedFacetsArg.Value.IsNull &&
                              nestedFacetsArg.Value.Kind == TypedConstantKind.Array &&
                              nestedFacetsArg.Value.Values.Length > 0;

        if (hasNestedFacets && maxDepth == 0 && !preserveReferences)
        {
            var diagnostic = Diagnostic.Create(
                CircularReferenceWarningRule,
                facetAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        // Check SourceSignature
        if (!sourceSignatureArg.Equals(default) && !sourceSignatureArg.Value.IsNull)
        {
            if (sourceSignatureArg.Value.Value is string expectedSignature && !string.IsNullOrEmpty(expectedSignature))
            {
                // Get IncludeFields value
                bool includeFields = false;
                if (!includeFieldsArg.Equals(default) && includeFieldsArg.Value.Value is bool includeFieldsValue)
                {
                    includeFields = includeFieldsValue;
                }

                // Get exclude values from constructor
                var excludeValues = facetAttr.ConstructorArguments.Length > 1
                    ? facetAttr.ConstructorArguments[1].Values
                    : ImmutableArray<TypedConstant>.Empty;

                // Get include value
                var includeValue = !includeArg.Equals(default) ? includeArg.Value : default;

                // Compute actual signature
                var actualSignature = ComputeSourceSignature(sourceType, excludeValues, includeValue, includeFields);

                // Compare signatures
                if (!string.Equals(expectedSignature, actualSignature, StringComparison.OrdinalIgnoreCase))
                {
                    var diagnostic = Diagnostic.Create(
                        SourceSignatureMismatchRule,
                        facetAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                        sourceType.ToDisplayString(),
                        actualSignature);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static void ReportInvalidPropertyName(
        SymbolAnalysisContext context,
        AttributeData facetAttr,
        string propertyName,
        string parameterName,
        INamedTypeSymbol sourceType,
        HashSet<string> validProperties)
    {
        var diagnostic = Diagnostic.Create(
            InvalidPropertyNameRule,
            facetAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
            propertyName,
            parameterName,
            sourceType.ToDisplayString());
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsPartialType(INamedTypeSymbol type)
    {
        // A type is partial if any of its declarations has the partial modifier
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

    private static bool HasFacetAttribute(ITypeSymbol type)
    {
        return type.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() == "Facet.FacetAttribute");
    }

    private static bool ImplementsConfigurationInterface(INamedTypeSymbol configurationType, INamedTypeSymbol sourceType, INamedTypeSymbol targetType)
    {
        // Check for IFacetMapConfiguration<TSource, TTarget>
        var syncInterface = configurationType.AllInterfaces.FirstOrDefault(i =>
            i.IsGenericType &&
            i.ConstructedFrom.ToDisplayString() == "Facet.Mapping.IFacetMapConfiguration<TSource, TTarget>" &&
            SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], sourceType) &&
            SymbolEqualityComparer.Default.Equals(i.TypeArguments[1], targetType));

        if (syncInterface != null)
            return true;

        // Check for IFacetMapConfigurationAsync<TSource, TTarget>
        var asyncInterface = configurationType.AllInterfaces.FirstOrDefault(i =>
            i.IsGenericType &&
            i.ConstructedFrom.ToDisplayString() == "Facet.Mapping.IFacetMapConfigurationAsync<TSource, TTarget>" &&
            SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], sourceType) &&
            SymbolEqualityComparer.Default.Equals(i.TypeArguments[1], targetType));

        if (asyncInterface != null)
            return true;

        // Also check for static Map method (alternative approach without interface)
        var mapMethod = configurationType.GetMembers("Map")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsStatic &&
                                m.Parameters.Length == 2 &&
                                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, sourceType) &&
                                SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, targetType));

        return mapMethod != null;
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

    private static string ComputeSourceSignature(
        INamedTypeSymbol sourceType,
        ImmutableArray<TypedConstant> excludeValues,
        TypedConstant includeValue,
        bool includeFields)
    {
        // Get all public members from source type
        var allMembers = GetAllPublicMembers(sourceType).ToList();

        // Build exclude set
        var excludeSet = new HashSet<string>();
        foreach (var item in excludeValues)
        {
            if (item.Value is string name && !string.IsNullOrEmpty(name))
                excludeSet.Add(name);
        }

        // Build include set if specified
        HashSet<string>? includeSet = null;
        if (!includeValue.IsNull && includeValue.Kind == TypedConstantKind.Array)
        {
            includeSet = new HashSet<string>();
            foreach (var item in includeValue.Values)
            {
                if (item.Value is string name && !string.IsNullOrEmpty(name))
                    includeSet.Add(name);
            }
        }

        // Filter and format members
        var filteredMembers = allMembers
            .Where(m =>
            {
                if (m.Kind == SymbolKind.Field && !includeFields)
                    return false;

                if (includeSet != null)
                    return includeSet.Contains(m.Name);

                return !excludeSet.Contains(m.Name);
            })
            .OrderBy(m => m.Name)
            .Select(m =>
            {
                var typeName = m switch
                {
                    IPropertySymbol prop => prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    IFieldSymbol field => field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    _ => "unknown"
                };
                return $"{m.Name}:{typeName}";
            });

        var combined = string.Join("|", filteredMembers);

        // Compute short hash
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8).ToLowerInvariant();
    }
}
