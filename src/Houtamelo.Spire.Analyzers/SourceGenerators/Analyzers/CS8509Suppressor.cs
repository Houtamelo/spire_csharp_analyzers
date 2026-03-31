using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.Utils;
using Houtamelo.Spire.PatternAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CS8509Suppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor Descriptor = new(
        id: "SPIRE_SUP001",
        suppressedDiagnosticId: "CS8509",
        justification: "All cases are handled (discriminated union or [EnforceExhaustiveness] type)");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        ImmutableArray.Create(Descriptor);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        var enforceAttr = context.Compilation
            .GetTypeByMetadataName("Houtamelo.Spire.EnforceExhaustivenessAttribute");
        if (enforceAttr is null) return;

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

            if (!IsExhaustivenessCheckedType(subjectType, enforceAttr))
                continue;

            var result = ExhaustivenessChecker.Check(context.Compilation, switchOp);
            if (result.MissingCases.IsEmpty)
            {
                context.ReportSuppression(
                    Suppression.Create(Descriptor, diagnostic));
            }
        }
    }

    private static bool IsExhaustivenessCheckedType(
        ITypeSymbol subjectType, INamedTypeSymbol enforceAttr)
    {
        // Unwrap Nullable<T>
        if (subjectType is INamedTypeSymbol nullable
            && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && nullable.TypeArguments.Length == 1)
            subjectType = nullable.TypeArguments[0];

        // [EnforceExhaustiveness] (or subclass like [DiscriminatedUnion]) on non-enum types
        return subjectType is INamedTypeSymbol named
            && named.TypeKind != TypeKind.Enum
            && AttributeHelper.HasOrInheritsAttribute(named, enforceAttr);
    }
}
