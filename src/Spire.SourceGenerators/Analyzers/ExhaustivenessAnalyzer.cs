using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Spire.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExhaustivenessAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            AnalyzerDescriptors.SPIRE009_SwitchNotExhaustive,
            AnalyzerDescriptors.SPIRE010_WildcardInsteadOfExhaustive);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationCtx =>
        {
            var duAttr = compilationCtx.Compilation
                .GetTypeByMetadataName("Spire.DiscriminatedUnionAttribute");
            if (duAttr is null) return;

            compilationCtx.RegisterOperationAction(
                ctx => AnalyzeSwitchExpression(ctx, duAttr),
                OperationKind.SwitchExpression);

            compilationCtx.RegisterOperationAction(
                ctx => AnalyzeSwitchStatement(ctx, duAttr),
                OperationKind.Switch);
        });
    }

    private static void AnalyzeSwitchExpression(
        OperationAnalysisContext ctx, INamedTypeSymbol duAttr)
    {
        var switchOp = (ISwitchExpressionOperation)ctx.Operation;
        var subjectType = switchOp.Value.Type;
        if (subjectType is null) return;

        var unionInfo = UnionTypeInfo.TryCreate(subjectType, duAttr);
        if (unionInfo is null) return;

        var coverage = PatternAnalyzer.AnalyzeExpression(switchOp, unionInfo);
        ReportDiagnostics(ctx, subjectType, unionInfo, coverage, switchOp.Syntax.GetLocation());
    }

    private static void AnalyzeSwitchStatement(
        OperationAnalysisContext ctx, INamedTypeSymbol duAttr)
    {
        var switchOp = (ISwitchOperation)ctx.Operation;
        var subjectType = switchOp.Value.Type;
        if (subjectType is null) return;

        var unionInfo = UnionTypeInfo.TryCreate(subjectType, duAttr);
        if (unionInfo is null) return;

        var coverage = PatternAnalyzer.AnalyzeStatement(switchOp, unionInfo);
        ReportDiagnostics(ctx, subjectType, unionInfo, coverage, switchOp.Syntax.GetLocation());
    }

    private static void ReportDiagnostics(
        OperationAnalysisContext ctx,
        ITypeSymbol subjectType,
        UnionTypeInfo unionInfo,
        SwitchCoverage coverage,
        Location location)
    {
        var missing = coverage.GetMissingVariants(unionInfo.VariantNames);
        if (missing.IsEmpty) return;

        var missingStr = string.Join(", ", missing.Select(v => $"'{v}'"));

        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add("MissingVariants", string.Join(",", missing));

        if (coverage.HasWildcard)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDescriptors.SPIRE010_WildcardInsteadOfExhaustive,
                location, properties.ToImmutable(),
                subjectType.Name, missingStr));
        }
        else
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDescriptors.SPIRE009_SwitchNotExhaustive,
                location, properties.ToImmutable(),
                subjectType.Name, missingStr));
        }
    }
}
