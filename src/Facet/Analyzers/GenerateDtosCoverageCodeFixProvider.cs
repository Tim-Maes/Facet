using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Facet.Analyzers;

/// <summary>
/// Code fix provider for <c>FAC108</c>. Offers "Generate DTOs for {EntityName}", which adds
/// an <c>[assembly: GenerateDtosFor(typeof(Entity), ...)]</c> attribute to the file that already
/// contains other <c>[assembly: GenerateDtosFor]</c> attributes, or creates a new
/// <c>FacetGeneration.cs</c> file if none exists.
/// </summary>
/// <remarks>
/// Default attribute values: <c>Types = DtoTypes.Create | DtoTypes.Update</c>,
/// <c>OutputType = OutputType.PartialClass</c>, <c>ExcludeAuditFields = true</c>.
/// The user can refine <c>Suffix</c> and <c>ExcludeProperties</c> after the fix is applied.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GenerateDtosCoverageCodeFixProvider)), Shared]
public class GenerateDtosCoverageCodeFixProvider : CodeFixProvider
{
    private const string TitleFormat = "Generate DTOs for {0}";

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("FAC108");

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var entityName = ExtractEntityName(diagnostic.GetMessage());
        if (entityName == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: string.Format(TitleFormat, entityName),
                createChangedDocument: c => AddGenerateDtosForAttributeAsync(context.Document, entityName, c),
                equivalenceKey: $"GenerateDtosFor_{entityName}"),
            diagnostic);
    }

    /// <summary>
    /// Extracts the entity simple name from the FAC108 message:
    /// "Entity 'Computer' has no DTOs configured (expected Create, Update)"
    /// → "Computer"
    /// </summary>
    private static string? ExtractEntityName(string message)
    {
        const string marker = "Entity '";
        var startIndex = message.IndexOf(marker);
        if (startIndex < 0) return null;

        startIndex += marker.Length;
        var endIndex = message.IndexOf("'", startIndex);
        if (endIndex < 0) return null;

        return message.Substring(startIndex, endIndex - startIndex);
    }

    private static async Task<Document> AddGenerateDtosForAttributeAsync(
        Document document,
        string entitySimpleName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null) return document;

        // Find the entity type symbol by searching for a type with this simple name.
        // The FAC108 diagnostic carries only the simple name; we need the full name for typeof().
        var entityType = ResolveEntityType(compilation, entitySimpleName);
        if (entityType == null) return document;

        var entityFullName = entityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Build the new attribute: [assembly: GenerateDtosFor(typeof(global::Ns.Entity), Types = DtoTypes.Create | DtoTypes.Update, OutputType = OutputType.PartialClass, ExcludeAuditFields = true)]
        var newAttribute = SyntaxFactory.Attribute(
            SyntaxFactory.ParseName("Facet.GenerateDtosFor"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.ParseExpression($"typeof({entityFullName})")),
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.NameEquals("Types"),
                        null,
                        SyntaxFactory.ParseExpression("Facet.DtoTypes.Create | Facet.DtoTypes.Update")),
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.NameEquals("OutputType"),
                        null,
                        SyntaxFactory.ParseExpression("Facet.OutputType.PartialClass")),
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.NameEquals("ExcludeAuditFields"),
                        null,
                        SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)),
                })));

        var assemblyAttribute = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(newAttribute))
            .WithTarget(SyntaxFactory.AttributeTargetSpecifier(
                SyntaxFactory.Token(SyntaxKind.AssemblyKeyword)));

        // Find the compilation unit root.
        var compilationRoot = (CompilationUnitSyntax)root;

        // If the file already has [assembly: GenerateDtosFor] attributes, add after the last one.
        var existingAssemblyAttrs = compilationRoot.AttributeLists
            .Where(al => al.Target?.Identifier.IsKind(SyntaxKind.AssemblyKeyword) == true)
            .ToList();

        CompilationUnitSyntax newRoot;
        if (existingAssemblyAttrs.Count > 0)
        {
            // Insert after the last assembly-level attribute.
            var lastAttr = existingAssemblyAttrs.Last();
            var insertIndex = compilationRoot.AttributeLists.IndexOf(lastAttr) + 1;
            newRoot = compilationRoot.WithAttributeLists(
                compilationRoot.AttributeLists.Insert(insertIndex, assemblyAttribute));
        }
        else
        {
            // No assembly-level attributes in this file — just prepend.
            newRoot = compilationRoot.AddAttributeLists(assemblyAttribute);
        }

        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Resolves an entity type symbol from its simple name by scanning all referenced
    /// assemblies and the current compilation. Returns the first match — entity simple names
    /// are typically unique within a solution's domain model.
    /// </summary>
    private static INamedTypeSymbol? ResolveEntityType(Compilation compilation, string simpleName)
    {
        // First try an exact metadata name match in the current compilation.
        foreach (var type in compilation.GetSymbolsWithName(
                     s => s.Equals(simpleName, System.StringComparison.Ordinal),
                     SymbolFilter.Type).OfType<INamedTypeSymbol>())
        {
            if (type.TypeKind == TypeKind.Class && !type.IsAbstract)
                return type;
        }

        // Then scan referenced assemblies via namespace walk.
        foreach (var module in compilation.SourceModule.ReferencedAssemblySymbols
                     .SelectMany(a => a.Modules))
        {
            foreach (var ns in module.GlobalNamespace.GetNamespaceNames())
            {
                var fullName = $"{ns}.{simpleName}";
                var symbol = compilation.GetTypeByMetadataName(fullName);
                if (symbol != null && symbol.TypeKind == TypeKind.Class && !symbol.IsAbstract)
                    return symbol;
            }
        }

        return null;
    }
}

internal static class NamespaceExtensions
{
    public static System.Collections.Generic.IEnumerable<string> GetNamespaceNames(
        this INamespaceSymbol namespaceSymbol)
    {
        yield return namespaceSymbol.ToDisplayString();
        foreach (var child in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var name in child.GetNamespaceNames())
                yield return name;
        }
    }
}
