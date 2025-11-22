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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SourceSignatureCodeFixProvider)), Shared]
public class SourceSignatureCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("FAC022");

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the attribute syntax
        var node = root.FindNode(diagnosticSpan);
        var attributeSyntax = node.AncestorsAndSelf().OfType<AttributeSyntax>().FirstOrDefault();

        if (attributeSyntax == null) return;

        // Extract the new signature from the diagnostic message
        // Message format: "Source entity '{0}' structure has changed. Update SourceSignature to '{1}' to acknowledge this change."
        var message = diagnostic.GetMessage();
        var newSignature = ExtractSignatureFromMessage(message);

        if (string.IsNullOrEmpty(newSignature)) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Update SourceSignature to '{newSignature}'",
                createChangedDocument: c => UpdateSourceSignatureAsync(context.Document, attributeSyntax, newSignature, c),
                equivalenceKey: "UpdateSourceSignature"),
            diagnostic);
    }

    private static string? ExtractSignatureFromMessage(string message)
    {
        // Look for "Update SourceSignature to 'XXXXXXXX'"
        const string marker = "Update SourceSignature to '";
        var startIndex = message.IndexOf(marker);
        if (startIndex < 0) return null;

        startIndex += marker.Length;
        var endIndex = message.IndexOf("'", startIndex);
        if (endIndex < 0) return null;

        return message.Substring(startIndex, endIndex - startIndex);
    }

    private static async Task<Document> UpdateSourceSignatureAsync(
        Document document,
        AttributeSyntax attributeSyntax,
        string newSignature,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Find existing SourceSignature argument
        var existingArg = attributeSyntax.ArgumentList?.Arguments
            .FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == "SourceSignature");

        AttributeSyntax newAttributeSyntax;

        if (existingArg != null)
        {
            // Update existing argument
            var newArg = existingArg.WithExpression(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(newSignature)));

            newAttributeSyntax = attributeSyntax.ReplaceNode(existingArg, newArg);
        }
        else
        {
            // Add new argument (shouldn't happen for FAC022, but handle gracefully)
            var newArg = SyntaxFactory.AttributeArgument(
                SyntaxFactory.NameEquals("SourceSignature"),
                null,
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(newSignature)));

            var newArgList = attributeSyntax.ArgumentList == null
                ? SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(newArg))
                : attributeSyntax.ArgumentList.AddArguments(newArg);

            newAttributeSyntax = attributeSyntax.WithArgumentList(newArgList);
        }

        var newRoot = root.ReplaceNode(attributeSyntax, newAttributeSyntax);
        return document.WithSyntaxRoot(newRoot);
    }
}
