using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp)]
[Shared]
public sealed class IsDefinedRefactoring : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null) return;

        var node = root.FindNode(context.Span);

        var invocation = node.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();
        if (invocation is null) return;

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken);
        if (model is null) return;

        var symbolInfo = model.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method) return;

        if (method.ContainingType?.SpecialType != SpecialType.System_Enum) return;
        if (method.Name != "IsDefined") return;

        // Case 1: Enum.IsDefined<TEnum>(value) — generic overload
        if (method.IsGenericMethod && method.TypeArguments.Length == 1)
        {
            var enumType = method.TypeArguments[0];
            if (enumType.TypeKind != TypeKind.Enum) return;

            var valueArg = invocation.ArgumentList.Arguments[0].Expression;

            context.RegisterRefactoring(
                CodeAction.Create(
                    title: $"Replace with SpireEnum<{enumType.Name}>.TryFrom(value, out var result)",
                    createChangedDocument: ct =>
                        ReplaceWithTryFromAsync(context.Document, invocation,
                            SyntaxFactory.ParseTypeName(enumType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)),
                            valueArg, ct),
                    equivalenceKey: "ReplaceIsDefinedWithTryFrom"));
            return;
        }

        // Case 2: Enum.IsDefined(typeof(TEnum), value) — non-generic overload
        if (!method.IsGenericMethod && method.Parameters.Length == 2)
        {
            var args = invocation.ArgumentList.Arguments;
            if (args.Count != 2) return;

            if (args[0].Expression is not TypeOfExpressionSyntax typeOfExpr) return;

            var typeInfo = model.GetTypeInfo(typeOfExpr.Type, context.CancellationToken);
            if (typeInfo.Type is not { TypeKind: TypeKind.Enum }) return;

            var valueArg = args[1].Expression;

            context.RegisterRefactoring(
                CodeAction.Create(
                    title: $"Replace with SpireEnum<{typeInfo.Type.Name}>.TryFrom(value, out var result)",
                    createChangedDocument: ct =>
                        ReplaceWithTryFromAsync(context.Document, invocation, typeOfExpr.Type, valueArg, ct),
                    equivalenceKey: "ReplaceIsDefinedWithTryFrom"));
        }
    }

    private static async Task<Document> ReplaceWithTryFromAsync(
        Document document, InvocationExpressionSyntax invocation,
        TypeSyntax enumTypeSyntax, ExpressionSyntax valueArg, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null) return document;

        // Build: SpireEnum<TEnum>.TryFrom(value, out var result)
        var spireEnumType = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("SpireEnum"),
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SingletonSeparatedList(enumTypeSyntax.WithoutTrivia())));

        var memberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            spireEnumType,
            SyntaxFactory.IdentifierName("TryFrom"));

        var outArg = SyntaxFactory.Argument(
            SyntaxFactory.DeclarationExpression(
                SyntaxFactory.IdentifierName("var"),
                SyntaxFactory.SingleVariableDesignation(
                    SyntaxFactory.Identifier("result"))))
            .WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.OutKeyword));

        var newInvocation = SyntaxFactory.InvocationExpression(
            memberAccess,
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(valueArg.WithoutTrivia()),
                    outArg
                })));

        var newRoot = root.ReplaceNode(invocation, newInvocation.WithTriviaFrom(invocation));
        return document.WithSyntaxRoot(newRoot);
    }
}
