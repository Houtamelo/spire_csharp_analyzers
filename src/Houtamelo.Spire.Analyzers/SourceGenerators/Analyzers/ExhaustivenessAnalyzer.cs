using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExhaustivenessAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            AnalyzerDescriptors.SPIRE009_SwitchNotExhaustive);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationCtx =>
        {
            var duAttr = compilationCtx.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.DiscriminatedUnionAttribute");
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

        var unionInfo = UnionTypeInfo.TryCreateWithNullableUnwrap(subjectType, duAttr, out var isNullable);
        if (unionInfo is null) return;

        var coverage = PatternAnalyzer.AnalyzeExpression(switchOp, unionInfo);
        ReportDiagnostics(ctx, subjectType, unionInfo, coverage, isNullable, switchOp.Syntax.GetLocation());
    }

    private static void AnalyzeSwitchStatement(
        OperationAnalysisContext ctx, INamedTypeSymbol duAttr)
    {
        var switchOp = (ISwitchOperation)ctx.Operation;
        var subjectType = switchOp.Value.Type;
        if (subjectType is null) return;

        var unionInfo = UnionTypeInfo.TryCreateWithNullableUnwrap(subjectType, duAttr, out var isNullable);
        if (unionInfo is null) return;

        var coverage = PatternAnalyzer.AnalyzeStatement(switchOp, unionInfo);
        ReportDiagnostics(ctx, subjectType, unionInfo, coverage, isNullable, switchOp.Syntax.GetLocation());
    }

    private static void ReportDiagnostics(
        OperationAnalysisContext ctx,
        ITypeSymbol subjectType,
        UnionTypeInfo unionInfo,
        SwitchCoverage coverage,
        bool isNullable,
        Location location)
    {
        // Wildcard covers both all variants and null — nothing to report
        if (coverage.HasWildcard)
            return;

        var missing = coverage.GetMissingVariants(unionInfo.VariantNames);
        bool needsNull = isNullable && !coverage.CoversNull;

        if (missing.IsEmpty && !needsNull)
            return;

        var missingParts = missing.Select(v => $"'{v}'").ToList();
        if (needsNull)
            missingParts.Add("'null'");
        var missingStr = string.Join(", ", missingParts);

        var propertyParts = missing.ToList();
        if (needsNull)
            propertyParts.Add("null");

        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add("MissingVariants", string.Join(",", propertyParts));

        ctx.ReportDiagnostic(Diagnostic.Create(
            AnalyzerDescriptors.SPIRE009_SwitchNotExhaustive,
            location, properties.ToImmutable(),
            subjectType.Name, missingStr));
    }
}
