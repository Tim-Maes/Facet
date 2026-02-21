using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Facet.Generators.Shared
{
    public static class NameOfResolver
    {
        // Returns resolved name and whether the left-most identifier had a leading '@'
        public static (string? resolved, bool hadLeadingAt) ResolveExpression(ExpressionSyntax? expr)
        {
            if (expr == null) return (null, false);

            // Handle invocation-based nameof: nameof(X.Y) -> unwrap argument
            if (expr is InvocationExpressionSyntax invocation)
            {
                ExpressionSyntax invokedExpr = invocation.Expression;
                var invokedName = invokedExpr switch
                {
                    IdentifierNameSyntax id => id.Identifier.ValueText,
                    MemberAccessExpressionSyntax ma when ma.Name is IdentifierNameSyntax id2 => id2.Identifier.ValueText,
                    _ => null
                };

                if (string.Equals(invokedName, "nameof", System.StringComparison.Ordinal))
                {
                    var firstArg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                    return ResolveExpression(firstArg);
                }
            }

            switch (expr)
            {
                case IdentifierNameSyntax id:
                    return (id.Identifier.ValueText, id.Identifier.Text.StartsWith("@"));

                case GenericNameSyntax gen:
                    return (gen.Identifier.ValueText, gen.Identifier.Text.StartsWith("@"));

                case MemberAccessExpressionSyntax ma:
                {
                    var (left, leftHadAt) = ResolveExpression(ma.Expression);
                    var right = ResolveSimpleName(ma.Name);
                    if (left == null || right == null) return (null, false);
                    return (left + "." + right, leftHadAt);
                }

                case QualifiedNameSyntax qn:
                {
                    var (left, leftHadAt) = ResolveQualifiedLeft(qn.Left);
                    var right = ResolveSimpleName(qn.Right);
                    if (left == null || right == null) return (null, false);
                    return (left + "." + right, leftHadAt);
                }

                case AliasQualifiedNameSyntax aq:
                {
                    var alias = aq.Alias.Identifier.ValueText;
                    var name = ResolveSimpleName(aq.Name);
                    return name == null ? (null, false) : (alias + "::" + name, aq.Alias.Identifier.Text.StartsWith("@"));
                }

                default:
                    // Fallback: only remove a leading '@' from the first token if present
                    var text = expr.ToString();
                    var firstToken = expr.GetFirstToken();
                    var hadAt = !string.IsNullOrEmpty(firstToken.Text) && firstToken.Text.StartsWith("@");
                    if (hadAt)
                    {
                        var unescapedFirst = firstToken.Text.TrimStart('@');
                        var idx = text.IndexOf(firstToken.Text, System.StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            text = text.Substring(0, idx) + unescapedFirst + text.Substring(idx + firstToken.Text.Length);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(text)) return (null, false);
                    return (text, hadAt);
            }
        }

        private static (string? left, bool hadLeadingAt) ResolveQualifiedLeft(NameSyntax left)
        {
            if (left is QualifiedNameSyntax qnLeft)
            {
                var (l, hadAt) = ResolveQualifiedLeft(qnLeft.Left);
                var right = ResolveSimpleName(qnLeft.Right);
                if (l == null || right == null) return (null, false);
                return (l + "." + right, hadAt);
            }

            if (left is SimpleNameSyntax s)
            {
                var hadAt = left.GetFirstToken().Text.StartsWith("@");
                return (ResolveSimpleName(s), hadAt);
            }

            return (null, false);
        }

        private static string? ResolveSimpleName(SimpleNameSyntax? name)
        {
            if (name == null) return null;
            return name switch
            {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                GenericNameSyntax gen => gen.Identifier.ValueText,
                _ => null
            };
        }
    }
}