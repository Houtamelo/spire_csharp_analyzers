using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class FromEnumCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("SPIRE016");

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];

        var isConstant = diagnostic.Properties.GetValueOrDefault("IsConstant");
        if (isConstant == "true")
            return;

        var isFlags = diagnostic.Properties.GetValueOrDefault("IsFlags") == "true";
        var methodName = isFlags ? "FromFlags" : "From";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Replace with SpireEnum<T>.{methodName}(value)",
                createChangedDocument: ct =>
                    ReplaceWithFromAsync(context.Document, diagnostic, methodName, ct),
                equivalenceKey: "ReplaceWithSpireEnumFrom"),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithFromAsync(
        Document document, Diagnostic diagnostic, string methodName, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null) return document;

        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        var castExpr = node.AncestorsAndSelf()
            .OfType<CastExpressionSyntax>()
            .FirstOrDefault();
        if (castExpr is null) return document;

        var targetType = castExpr.Type;
        var operand = castExpr.Expression;

        // For enum-to-enum casts, the operand is an enum value — wrap in (int) cast
        // since SpireEnum<T>.From only accepts integer types.
        var model = await document.GetSemanticModelAsync(ct);
        if (model is not null)
        {
            var operandType = model.GetTypeInfo(operand, ct).Type;
            if (operandType is { TypeKind: TypeKind.Enum })
            {
                operand = SyntaxFactory.CastExpression(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)),
                    operand.WithoutTrivia());
            }
        }

        // Build: SpireEnum<TargetType>.MethodName(operand)
        var spireEnumType = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("SpireEnum"),
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(targetType.WithoutTrivia())));

        var memberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            spireEnumType,
            SyntaxFactory.IdentifierName(methodName));

        var invocation = SyntaxFactory.InvocationExpression(
            memberAccess,
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(operand.WithoutTrivia()))));

        var newRoot = root.ReplaceNode(castExpr, invocation.WithTriviaFrom(castExpr));
        return document.WithSyntaxRoot(newRoot);
    }
}
