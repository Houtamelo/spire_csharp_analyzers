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
public sealed class AddMissingArmsCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("SPIRE009");

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        var missingStr = diagnostic.Properties.GetValueOrDefault("MissingVariants");
        if (missingStr is null or { Length: 0 }) return;

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

        // Detect whether existing arms use property patterns or positional patterns
        bool usePropertyPattern = ExistingArmsUsePropertyPattern(switchExpr);

        // Get Kind enum type name prefix (e.g. "Shape.Kind")
        var kindPrefix = $"{subjectType.Name}.Kind";

        // Generate new arms
        var newArms = new List<SwitchExpressionArmSyntax>();
        foreach (var variant in missingVariants)
        {
            var trimmed = variant.Trim();

            // Find factory method to determine field parameters
            var factory = subjectType.GetMembers(trimmed)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsStatic);

            SwitchExpressionArmSyntax arm;
            if (usePropertyPattern)
                arm = BuildPropertyPatternArm(trimmed, kindPrefix, factory);
            else
                arm = BuildPositionalPatternArm(trimmed, kindPrefix, factory, subjectType);

            newArms.Add(arm);
        }

        // Add new arms to existing switch expression
        var updatedSwitch = switchExpr.AddArms(newArms.ToArray());
        SyntaxNode newRoot = root.ReplaceNode(switchExpr, updatedSwitch);

        return document.WithSyntaxRoot(newRoot);
    }

    /// Returns true if any existing arm uses a property pattern (RecursivePattern with PropertyPatternClause).
    private static bool ExistingArmsUsePropertyPattern(SwitchExpressionSyntax switchExpr)
    {
        foreach (var arm in switchExpr.Arms)
        {
            if (arm.Pattern is RecursivePatternSyntax recursive &&
                recursive.PropertyPatternClause is not null)
                return true;
        }
        return false;
    }

    private static SwitchExpressionArmSyntax BuildPropertyPatternArm(
        string variantName, string kindPrefix, IMethodSymbol? factory)
    {
        var subpatterns = new List<SubpatternSyntax>();

        // Kind subpattern: kind: Shape.Kind.X
        subpatterns.Add(SyntaxFactory.Subpattern(
            SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("kind")),
            SyntaxFactory.ConstantPattern(
                SyntaxFactory.ParseExpression($"{kindPrefix}.{variantName}"))));

        // Field subpatterns: fieldName: type fieldName
        if (factory is not null)
        {
            foreach (var param in factory.Parameters)
            {
                var typeName = param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                subpatterns.Add(SyntaxFactory.Subpattern(
                    SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(param.Name)),
                    SyntaxFactory.DeclarationPattern(
                        SyntaxFactory.ParseTypeName(typeName),
                        SyntaxFactory.SingleVariableDesignation(
                            SyntaxFactory.Identifier(param.Name)))));
            }
        }

        var pattern = SyntaxFactory.RecursivePattern()
            .WithPropertyPatternClause(
                SyntaxFactory.PropertyPatternClause(
                    SyntaxFactory.SeparatedList(subpatterns)));

        var body = SyntaxFactory.ThrowExpression(
            SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.ParseTypeName("System.NotImplementedException"))
            .WithArgumentList(SyntaxFactory.ArgumentList()));

        return SyntaxFactory.SwitchExpressionArm(pattern, body);
    }

    private static SwitchExpressionArmSyntax BuildPositionalPatternArm(
        string variantName, string kindPrefix, IMethodSymbol? factory, INamedTypeSymbol subjectType)
    {
        int fieldCount = factory?.Parameters.Length ?? 0;

        var subpatterns = new List<SubpatternSyntax>();

        // First element: the Kind constant pattern
        subpatterns.Add(SyntaxFactory.Subpattern(
            SyntaxFactory.ConstantPattern(
                SyntaxFactory.ParseExpression($"{kindPrefix}.{variantName}"))));

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

        var body = SyntaxFactory.ThrowExpression(
            SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.ParseTypeName("System.NotImplementedException"))
            .WithArgumentList(SyntaxFactory.ArgumentList()));

        return SyntaxFactory.SwitchExpressionArm(pattern, body);
    }

}
