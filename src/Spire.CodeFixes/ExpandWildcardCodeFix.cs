using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Spire.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp)]
[Shared]
public sealed class ExpandWildcardRefactoring : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null) return;

        // Find the node at the cursor
        var node = root.FindNode(context.Span);

        // Find a discard pattern at or near the cursor
        var discardPattern = node.AncestorsAndSelf()
            .OfType<DiscardPatternSyntax>()
            .FirstOrDefault();

        // Also check if cursor is on the switch expression arm containing a discard
        var arm = node.AncestorsAndSelf()
            .OfType<SwitchExpressionArmSyntax>()
            .FirstOrDefault();

        if (discardPattern is null && arm?.Pattern is DiscardPatternSyntax)
            discardPattern = (DiscardPatternSyntax)arm.Pattern;

        if (discardPattern is null) return;

        // Walk up to the switch expression
        var switchExpr = discardPattern.Ancestors()
            .OfType<SwitchExpressionSyntax>()
            .FirstOrDefault();
        if (switchExpr is null) return;

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken);
        if (model is null) return;

        // Get the switch subject type
        var subjectType = model.GetTypeInfo(switchExpr.GoverningExpression,
            context.CancellationToken).Type as INamedTypeSymbol;
        if (subjectType is null) return;

        // Check if it's a discriminated union
        var duAttr = model.Compilation.GetTypeByMetadataName("Spire.DiscriminatedUnionAttribute");
        if (duAttr is null) return;
        if (!subjectType.GetAttributes().Any(a =>
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, duAttr)))
            return;

        // Find all variant names from the Kind enum (struct) or nested types (record/class)
        ImmutableArray<string> allVariants;
        bool isStruct = subjectType.IsValueType;

        if (isStruct)
        {
            var kindEnum = subjectType.GetTypeMembers("Kind")
                .FirstOrDefault(t => t.TypeKind == TypeKind.Enum);
            if (kindEnum is null) return;
            allVariants = kindEnum.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => f.HasConstantValue)
                .Select(f => f.Name)
                .ToImmutableArray();
        }
        else
        {
            allVariants = subjectType.GetTypeMembers()
                .Where(nested => nested.IsSealed &&
                    SymbolEqualityComparer.Default.Equals(
                        nested.BaseType?.OriginalDefinition,
                        subjectType.OriginalDefinition))
                .Select(v => v.Name)
                .ToImmutableArray();
        }

        if (allVariants.IsEmpty) return;

        // Analyze which variants are already covered by non-wildcard arms
        var operation = model.GetOperation(switchExpr, context.CancellationToken);
        if (operation is not ISwitchExpressionOperation switchOp) return;

        var coveredVariants = new HashSet<string>();
        foreach (var switchArm in switchOp.Arms)
        {
            if (switchArm.Pattern is IDiscardPatternOperation) continue;
            CollectCoveredVariants(switchArm.Pattern, subjectType, isStruct, coveredVariants);
        }

        var missingVariants = allVariants.Where(v => !coveredVariants.Contains(v)).ToList();
        if (missingVariants.Count == 0) return;

        context.RegisterRefactoring(
            CodeAction.Create(
                title: "Replace wildcard with explicit variants",
                createChangedDocument: ct => ExpandWildcardAsync(
                    context.Document, switchExpr, subjectType, missingVariants, isStruct, ct),
                equivalenceKey: "ExpandWildcard"));
    }

    private static void CollectCoveredVariants(
        IPatternOperation pattern, INamedTypeSymbol unionType, bool isStruct,
        HashSet<string> covered)
    {
        switch (pattern)
        {
            case IRecursivePatternOperation recursive:
                if (!recursive.DeconstructionSubpatterns.IsEmpty)
                {
                    var firstSub = recursive.DeconstructionSubpatterns[0];
                    CollectCoveredVariants(firstSub, unionType, isStruct, covered);
                }
                if (isStruct && !recursive.PropertySubpatterns.IsEmpty)
                {
                    foreach (var prop in recursive.PropertySubpatterns)
                    {
                        if (prop.Member is IFieldReferenceOperation fieldRef &&
                            fieldRef.Field.Name == "kind")
                        {
                            CollectCoveredVariants(prop.Pattern, unionType, isStruct, covered);
                        }
                    }
                }
                if (!isStruct && recursive.MatchedType is INamedTypeSymbol matchedType)
                {
                    var name = matchedType.Name;
                    if (unionType.GetTypeMembers(name).Any())
                        covered.Add(name);
                }
                break;

            case IConstantPatternOperation constant:
                if (constant.Value.ConstantValue.HasValue &&
                    constant.Value.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum })
                {
                    if (constant.Value is IFieldReferenceOperation fieldRef)
                        covered.Add(fieldRef.Field.Name);
                }
                break;

            case IBinaryPatternOperation binary:
                CollectCoveredVariants(binary.LeftPattern, unionType, isStruct, covered);
                CollectCoveredVariants(binary.RightPattern, unionType, isStruct, covered);
                break;

            case ITypePatternOperation typePattern:
                if (!isStruct && typePattern.MatchedType is INamedTypeSymbol typeMatched)
                {
                    var name = typeMatched.Name;
                    if (unionType.GetTypeMembers(name).Any())
                        covered.Add(name);
                }
                break;

            case IDeclarationPatternOperation declPattern:
                if (!isStruct && declPattern.MatchedType is INamedTypeSymbol declMatched)
                {
                    var name = declMatched.Name;
                    if (unionType.GetTypeMembers(name).Any())
                        covered.Add(name);
                }
                break;
        }
    }

    private static async Task<Document> ExpandWildcardAsync(
        Document document, SwitchExpressionSyntax switchExpr,
        INamedTypeSymbol subjectType, List<string> missingVariants,
        bool isStruct, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        var model = await document.GetSemanticModelAsync(ct);
        if (root is null || model is null) return document;

        // Find the wildcard arm
        var wildcardArm = switchExpr.Arms
            .FirstOrDefault(arm => arm.Pattern is DiscardPatternSyntax);
        if (wildcardArm is null) return document;

        var bodyExpr = wildcardArm.Expression;
        var kindPrefix = $"{subjectType.Name}.Kind";

        var newArms = new List<SwitchExpressionArmSyntax>();
        foreach (var variant in missingVariants)
        {
            var subpatterns = new List<SubpatternSyntax>();

            if (isStruct)
            {
                // First element: Kind constant
                subpatterns.Add(SyntaxFactory.Subpattern(
                    SyntaxFactory.ConstantPattern(
                        SyntaxFactory.ParseExpression($"{kindPrefix}.{variant}"))));

                // Remaining: typed declarations from factory method
                var factory = subjectType.GetMembers(variant)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.IsStatic);

                if (factory is not null)
                {
                    foreach (var param in factory.Parameters)
                    {
                        var typeName = param.Type.ToDisplayString(
                            SymbolDisplayFormat.MinimallyQualifiedFormat);
                        subpatterns.Add(SyntaxFactory.Subpattern(
                            SyntaxFactory.DeclarationPattern(
                                SyntaxFactory.ParseTypeName(typeName),
                                SyntaxFactory.SingleVariableDesignation(
                                    SyntaxFactory.Identifier(param.Name)))));
                    }
                }

                // Fieldless variant with shared Deconstruct needs one discard
                if ((factory?.Parameters.Length ?? 0) == 0)
                {
                    var hasShared = subjectType.GetMembers("Deconstruct")
                        .OfType<IMethodSymbol>()
                        .Any(m => m.Parameters.Length == 2);
                    if (hasShared)
                    {
                        subpatterns.Add(SyntaxFactory.Subpattern(
                            SyntaxFactory.DiscardPattern()));
                    }
                }
            }
            else
            {
                // Record/class: type pattern — handled differently
                // For now, just create a type pattern
                subpatterns.Add(SyntaxFactory.Subpattern(
                    SyntaxFactory.ConstantPattern(
                        SyntaxFactory.ParseExpression($"{subjectType.Name}.{variant}"))));
            }

            var pattern = SyntaxFactory.RecursivePattern()
                .WithPositionalPatternClause(
                    SyntaxFactory.PositionalPatternClause(
                        SyntaxFactory.SeparatedList(subpatterns)));

            var arm = SyntaxFactory.SwitchExpressionArm(pattern, bodyExpr);
            newArms.Add(arm);
        }

        var updatedSwitch = switchExpr
            .RemoveNode(wildcardArm, SyntaxRemoveOptions.KeepNoTrivia)!
            .AddArms(newArms.ToArray());

        var newRoot = root.ReplaceNode(switchExpr, updatedSwitch);
        return document.WithSyntaxRoot(newRoot);
    }
}
