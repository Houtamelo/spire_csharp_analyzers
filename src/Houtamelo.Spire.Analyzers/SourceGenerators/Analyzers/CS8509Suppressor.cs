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
    private static readonly SuppressionDescriptor CS8509Descriptor = new(
        id: "SPIRE_SUP001",
        suppressedDiagnosticId: "CS8509",
        justification: "All cases are handled (discriminated union or [EnforceExhaustiveness] type)");

    private static readonly SuppressionDescriptor CS8524Descriptor = new(
        id: "SPIRE_SUP002",
        suppressedDiagnosticId: "CS8524",
        justification: "All named enum members are handled ([EnforceExhaustiveness] enum)");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        ImmutableArray.Create(CS8509Descriptor, CS8524Descriptor);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        var enforceAttr = context.Compilation
            .GetTypeByMetadataName("Houtamelo.Spire.EnforceExhaustivenessAttribute");
        if (enforceAttr is null) return;

        var enforceOnAllEnums = GlobalConfigHelper.ReadEnforceExhaustivenessOnAllEnumTypes(context.Options);

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

            if (!IsExhaustivenessCheckedType(subjectType, enforceAttr, enforceOnAllEnums))
                continue;

            var result = ExhaustivenessChecker.Check(context.Compilation, switchOp);
            if (result.MissingCases.IsEmpty)
            {
                var descriptor = diagnostic.Id == "CS8524" ? CS8524Descriptor : CS8509Descriptor;
                context.ReportSuppression(
                    Suppression.Create(descriptor, diagnostic));
            }
        }
    }

    private static bool IsExhaustivenessCheckedType(
        ITypeSymbol subjectType, INamedTypeSymbol enforceAttr, bool enforceOnAllEnums)
    {
        // Unwrap Nullable<T>
        if (subjectType is INamedTypeSymbol nullable
            && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && nullable.TypeArguments.Length == 1)
            subjectType = nullable.TypeArguments[0];

        if (subjectType is not INamedTypeSymbol named)
            return false;

        if (named.TypeKind == TypeKind.Enum)
            return enforceOnAllEnums || AttributeHelper.HasOrInheritsAttribute(named, enforceAttr);

        return AttributeHelper.HasOrInheritsAttribute(named, enforceAttr);
    }
}
