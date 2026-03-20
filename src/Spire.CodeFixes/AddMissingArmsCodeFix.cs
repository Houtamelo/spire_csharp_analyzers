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
public sealed class AddMissingArmsCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("SPIRE009");

    public override FixAllProvider? GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        var missingStr = diagnostic.Properties.GetValueOrDefault("MissingVariants");
        if (string.IsNullOrEmpty(missingStr)) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add missing variant arms",
                createChangedDocument: ct => AddMissingArmsAsync(context.Document, diagnostic, ct),
                equivalenceKey: "AddMissingArms"),
            diagnostic);
    }

    private static async Task<Document> AddMissingArmsAsync(
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

        // Get union type from the switch subject
        var subjectType = model.GetTypeInfo(switchExpr.GoverningExpression, ct).Type as INamedTypeSymbol;
        if (subjectType is null) return document;

        // Get Kind enum type name prefix (e.g. "Shape.Kind")
        var kindPrefix = $"{subjectType.Name}.Kind";

        // Generate new arms
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

            // Remaining elements: one discard per field
            for (int i = 0; i < fieldCount; i++)
            {
                subpatterns.Add(SyntaxFactory.Subpattern(
                    SyntaxFactory.DiscardPattern()));
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

            var body = SyntaxFactory.ThrowExpression(
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.ParseTypeName("System.NotImplementedException"))
                .WithArgumentList(SyntaxFactory.ArgumentList()));

            var arm = SyntaxFactory.SwitchExpressionArm(pattern, body);
            newArms.Add(arm);
        }

        // Add new arms to existing switch expression
        var updatedSwitch = switchExpr.AddArms(newArms.ToArray());
        var newRoot = root.ReplaceNode(switchExpr, updatedSwitch);
        return document.WithSyntaxRoot(newRoot);
    }
}
