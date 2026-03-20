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

namespace Spire.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class ExpandWildcardCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("SPIRE010");

    public override FixAllProvider? GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        var missingStr = diagnostic.Properties.GetValueOrDefault("MissingVariants");
        if (string.IsNullOrEmpty(missingStr)) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace wildcard with explicit variants",
                createChangedDocument: ct => ExpandWildcardAsync(context.Document, diagnostic, ct),
                equivalenceKey: "ExpandWildcard"),
            diagnostic);
    }

    private static async Task<Document> ExpandWildcardAsync(
        Document document, Diagnostic diagnostic, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        var model = await document.GetSemanticModelAsync(ct);
        if (root is null || model is null) return document;

        var missingVariantsStr = diagnostic.Properties["MissingVariants"];
        if (missingVariantsStr is null) return document;

        var missingVariants = missingVariantsStr.Split(',');

        // Find switch expression
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var switchExpr = node.AncestorsAndSelf()
            .OfType<SwitchExpressionSyntax>()
            .FirstOrDefault();
        if (switchExpr is null) return document;

        // Find the wildcard arm (discard pattern)
        var wildcardArm = switchExpr.Arms
            .FirstOrDefault(arm => arm.Pattern is DiscardPatternSyntax);
        if (wildcardArm is null) return document;

        // Get the wildcard's body expression
        var bodyExpr = wildcardArm.Expression;

        // Get union type from the switch subject
        var subjectType = model.GetTypeInfo(switchExpr.GoverningExpression, ct).Type as INamedTypeSymbol;
        if (subjectType is null) return document;

        // Get Kind enum type name prefix (e.g. "Shape.Kind")
        var kindPrefix = $"{subjectType.Name}.Kind";

        // Generate replacement arms using the wildcard's body
        var newArms = new List<SwitchExpressionArmSyntax>();
        foreach (var variant in missingVariants)
        {
            var trimmed = variant.Trim();

            // Find factory method to determine field count
            var factory = subjectType.GetMembers(trimmed)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsStatic);

            int fieldCount = factory?.Parameters.Length ?? 0;

            // Build positional pattern: (Kind.Variant, _, _, ...)
            var subpatterns = new List<SubpatternSyntax>();

            // First element: the Kind constant pattern
            subpatterns.Add(SyntaxFactory.Subpattern(
                SyntaxFactory.ConstantPattern(
                    SyntaxFactory.ParseExpression($"{kindPrefix}.{trimmed}"))));

            // Remaining elements: typed declaration per field
            if (factory is not null)
            {
                foreach (var param in factory.Parameters)
                {
                    var typeName = param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    subpatterns.Add(SyntaxFactory.Subpattern(
                        SyntaxFactory.DeclarationPattern(
                            SyntaxFactory.ParseTypeName(typeName),
                            SyntaxFactory.SingleVariableDesignation(
                                SyntaxFactory.Identifier(param.Name)))));
                }
            }

            // Fieldless variant with shared-arity Deconstruct needs one discard
            if (fieldCount == 0)
            {
                var hasSharedDeconstruct = subjectType.GetMembers("Deconstruct")
                    .OfType<IMethodSymbol>()
                    .Any(m => m.Parameters.Length == 2);
                if (hasSharedDeconstruct)
                {
                    subpatterns.Add(SyntaxFactory.Subpattern(
                        SyntaxFactory.DiscardPattern()));
                }
            }

            var pattern = SyntaxFactory.RecursivePattern()
                .WithPositionalPatternClause(
                    SyntaxFactory.PositionalPatternClause(
                        SyntaxFactory.SeparatedList(subpatterns)));

            // Use the wildcard's body expression (not throw)
            var arm = SyntaxFactory.SwitchExpressionArm(pattern, bodyExpr);
            newArms.Add(arm);
        }

        // Remove wildcard arm and add new explicit arms
        var updatedSwitch = switchExpr
            .RemoveNode(wildcardArm, SyntaxRemoveOptions.KeepNoTrivia)!
            .AddArms(newArms.ToArray());

        var newRoot = root.ReplaceNode(switchExpr, updatedSwitch);
        return document.WithSyntaxRoot(newRoot);
    }
}
