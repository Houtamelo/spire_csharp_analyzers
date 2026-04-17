using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Emit;

/// Rewrites the original host body so that invocations on an [Inlinable] parameter
/// (or a tracked var-alias of it) become `.Invoke(...)` calls.
internal static class InlinableBodyRewriter
{
    public static string Rewrite(InlinableHostDecl decl)
    {
        var body = decl.OriginalBody ?? "";
        if (body.Length == 0) return body;

        var trimmed = body.TrimStart();
        var isBlock = trimmed.StartsWith("{");
        var isExpressionBody = trimmed.StartsWith("=>");

        // Build initial alias sets (paramName -> isNullable).
        var names = new Dictionary<string, bool>();
        foreach (var p in decl.InlinableParams)
            names[p.Name] = p.IsNullable;

        if (isBlock)
        {
            if (SyntaxFactory.ParseStatement(body) is not BlockSyntax block)
                return body;

            PropagateAliases(block, names);

            var rewriter = new Rewriter(names);
            var newBlock = rewriter.Visit(block);
            return newBlock.ToFullString();
        }
        else if (isExpressionBody)
        {
            // The body text starts with "=>". Parse the expression after "=>".
            // Strip the "=>" prefix, parse the expression, and retain any trailing ";".
            var after = trimmed.Substring(2);
            bool endsWithSemicolon = false;
            var exprText = after;
            var rstrip = exprText.TrimEnd();
            if (rstrip.EndsWith(";"))
            {
                endsWithSemicolon = true;
                // Drop trailing ";" and any trailing whitespace from that side only.
                exprText = rstrip.Substring(0, rstrip.Length - 1);
            }

            var expr = SyntaxFactory.ParseExpression(exprText);
            if (expr is null) return body;

            var rewriter = new Rewriter(names);
            var newExpr = (ExpressionSyntax)rewriter.Visit(expr);
            var result = "=> " + newExpr.ToFullString();
            if (endsWithSemicolon) result += ";";
            return result;
        }

        return body;
    }

    private static void PropagateAliases(BlockSyntax block, Dictionary<string, bool> names)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var stmt in block.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                var declaration = stmt.Declaration;
                if (declaration.Type is not IdentifierNameSyntax idType || idType.Identifier.Text != "var")
                    continue;
                if (declaration.Variables.Count != 1)
                    continue;
                var variable = declaration.Variables[0];
                var initializer = variable.Initializer;
                if (initializer is null) continue;
                var localName = variable.Identifier.Text;
                if (names.ContainsKey(localName))
                    continue;

                var value = initializer.Value;
                if (value is IdentifierNameSyntax valueId)
                {
                    if (names.TryGetValue(valueId.Identifier.Text, out var isNullable))
                    {
                        names[localName] = isNullable;
                        changed = true;
                    }
                }
                else if (value is ConditionalExpressionSyntax cond)
                {
                    if (cond.WhenTrue is IdentifierNameSyntax t && cond.WhenFalse is IdentifierNameSyntax f)
                    {
                        if (names.TryGetValue(t.Identifier.Text, out var tNullable)
                            && names.TryGetValue(f.Identifier.Text, out var fNullable))
                        {
                            names[localName] = tNullable || fNullable;
                            changed = true;
                        }
                    }
                }
            }
        }
    }

    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, bool> _names;

        public Rewriter(Dictionary<string, bool> names) : base(visitIntoStructuredTrivia: false)
        {
            _names = names;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

            if (visited.Expression is IdentifierNameSyntax id && _names.ContainsKey(id.Identifier.Text))
            {
                var invokeAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    id.WithoutTrailingTrivia(),
                    SyntaxFactory.IdentifierName("Invoke"));
                return visited.WithExpression(invokeAccess);
            }

            return visited;
        }

        public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            var visited = (ConditionalAccessExpressionSyntax)base.VisitConditionalAccessExpression(node)!;

            if (visited.Expression is IdentifierNameSyntax id
                && _names.TryGetValue(id.Identifier.Text, out var isNullable))
            {
                if (!isNullable)
                {
                    var rewritten = StripConditionalAccess(id, visited.WhenNotNull);
                    if (rewritten is not null)
                        return rewritten.WithTriviaFrom(visited);
                }
            }

            return visited;
        }

        private static ExpressionSyntax? StripConditionalAccess(IdentifierNameSyntax receiver, ExpressionSyntax whenNotNull)
        {
            switch (whenNotNull)
            {
                case InvocationExpressionSyntax inv when inv.Expression is MemberBindingExpressionSyntax mb:
                {
                    var memberAccess = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        receiver.WithoutTrivia(),
                        mb.Name);
                    return SyntaxFactory.InvocationExpression(memberAccess, inv.ArgumentList);
                }
                case MemberBindingExpressionSyntax mb:
                {
                    return SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        receiver.WithoutTrivia(),
                        mb.Name);
                }
                default:
                    return null;
            }
        }
    }
}
