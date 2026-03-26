using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Spire.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CS8509Suppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor Descriptor = new(
        id: "SPIRE_SUP001",
        suppressedDiagnosticId: "CS8509",
        justification: "All variants of discriminated union are handled");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        ImmutableArray.Create(Descriptor);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var tree = diagnostic.Location.SourceTree;
            if (tree is null) continue;

            var model = context.GetSemanticModel(tree);

            var root = tree.GetRoot(context.CancellationToken);
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            // Walk up to find the SwitchExpressionSyntax
            var switchSyntax = node.AncestorsAndSelf()
                .OfType<SwitchExpressionSyntax>()
                .FirstOrDefault();
            if (switchSyntax is null) continue;

            var operation = model.GetOperation(switchSyntax, context.CancellationToken);
            if (operation is not ISwitchExpressionOperation switchOp) continue;

            var subjectType = switchOp.Value.Type;
            if (subjectType is null) continue;

            var duAttr = context.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.DiscriminatedUnionAttribute");
            if (duAttr is null) continue;

            var unionInfo = UnionTypeInfo.TryCreateWithNullableUnwrap(
                subjectType, duAttr, out var isNullable);
            if (unionInfo is null) continue;

            var coverage = PatternAnalyzer.AnalyzeExpression(switchOp, unionInfo);
            var missing = coverage.GetMissingVariants(unionInfo.VariantNames);

            bool allVariantsCovered = missing.IsEmpty || coverage.HasWildcard;
            bool nullSatisfied = !isNullable || coverage.CoversNull || coverage.HasWildcard;

            if (allVariantsCovered && nullSatisfied)
            {
                context.ReportSuppression(
                    Suppression.Create(Descriptor, diagnostic));
            }
        }
    }
}
