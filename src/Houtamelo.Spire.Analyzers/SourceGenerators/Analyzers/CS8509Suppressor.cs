using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.PatternAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CS8509Suppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor CS8509Descriptor = new(
        id: "SPIRE_SUP001",
        suppressedDiagnosticId: "CS8509",
        justification: "Spire's exhaustiveness checker proved the switch covers every reachable case.");

    private static readonly SuppressionDescriptor CS8524Descriptor = new(
        id: "SPIRE_SUP002",
        suppressedDiagnosticId: "CS8524",
        justification: "Spire's exhaustiveness checker proved every named enum member is handled.");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        ImmutableArray.Create(CS8509Descriptor, CS8524Descriptor);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var tree = diagnostic.Location.SourceTree;
            if (tree is null) continue;

            var model = context.GetSemanticModel(tree);

            var root = tree.GetRoot(context.CancellationToken);
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            var switchSyntax = node.AncestorsAndSelf()
                .OfType<SwitchExpressionSyntax>()
                .FirstOrDefault();
            if (switchSyntax is null) continue;

            var operation = model.GetOperation(switchSyntax, context.CancellationToken);
            if (operation is not ISwitchExpressionOperation switchOp) continue;

            var subjectType = switchOp.Value.Type;
            if (subjectType is null) continue;

            // The Maranget checker is the source of truth — it understands enums, bools, numeric ranges,
            // tuples, property patterns, type hierarchies, and nullable variants. Run it on every switch
            // and trust its verdict.
            var result = ExhaustivenessChecker.Check(context.Compilation, switchOp);
            if (result.MissingCases.IsEmpty)
            {
                var descriptor = diagnostic.Id == "CS8524" ? CS8524Descriptor : CS8509Descriptor;
                context.ReportSuppression(
                    Suppression.Create(descriptor, diagnostic));
            }
        }
    }
}
