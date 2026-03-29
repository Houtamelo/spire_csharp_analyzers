using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.PatternAnalysis;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Houtamelo.Spire.PatternAnalysis.Domains.DiscriminatedUnion;
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

        var unionInfo = UnionTypeInfo.TryCreateWithNullableUnwrap(subjectType, duAttr, out _);
        if (unionInfo is null) return;

        var result = ExhaustivenessChecker.Check(ctx.Compilation, switchOp);
        ReportDiagnostics(ctx, subjectType, result, switchOp.Syntax.GetLocation());
    }

    private static void AnalyzeSwitchStatement(
        OperationAnalysisContext ctx, INamedTypeSymbol duAttr)
    {
        var switchOp = (ISwitchOperation)ctx.Operation;
        var subjectType = switchOp.Value.Type;
        if (subjectType is null) return;

        var unionInfo = UnionTypeInfo.TryCreateWithNullableUnwrap(subjectType, duAttr, out _);
        if (unionInfo is null) return;

        var result = ExhaustivenessChecker.Check(ctx.Compilation, switchOp);
        ReportDiagnostics(ctx, subjectType, result, switchOp.Syntax.GetLocation());
    }

    private static void ReportDiagnostics(
        OperationAnalysisContext ctx,
        ITypeSymbol subjectType,
        ExhaustivenessResult result,
        Location location)
    {
        if (result.MissingCases.IsEmpty)
            return;

        var missingNames = ExtractMissingVariantNames(result);
        if (missingNames.Count == 0)
            return;

        var missingStr = string.Join(", ", missingNames.Select(n => $"'{n}'"));

        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add("MissingVariants", string.Join(",", missingNames));

        ctx.ReportDiagnostic(Diagnostic.Create(
            AnalyzerDescriptors.SPIRE009_SwitchNotExhaustive,
            location, properties.ToImmutable(),
            subjectType.Name, missingStr));
    }

    /// Walks the missing-case constraints and extracts human-readable variant names
    /// from the remaining value domains.
    private static List<string> ExtractMissingVariantNames(ExhaustivenessResult result)
    {
        var names = new List<string>();

        foreach (var missingCase in result.MissingCases)
        {
            foreach (var constraint in missingCase.Constraints)
            {
                CollectNamesFromDomain(constraint.Remaining, names);
            }
        }

        return names;
    }

    /// Recursively extracts variant names from a value domain.
    private static void CollectNamesFromDomain(IValueDomain domain, List<string> names)
    {
        switch (domain)
        {
            case EnumDomain enumDomain:
                foreach (var member in enumDomain.Members)
                {
                    if (!names.Contains(member.Name))
                        names.Add(member.Name);
                }
                break;

            case EnforceExhaustiveDomain enfDomain:
                foreach (var type in enfDomain.RemainingTypes)
                {
                    if (!names.Contains(type.Name))
                        names.Add(type.Name);
                }
                break;

            case NullableDomain nullableDomain:
                if (nullableDomain.HasNull && !names.Contains("null"))
                    names.Add("null");
                CollectNamesFromDomain(nullableDomain.Inner, names);
                break;

            case StructuralDomain structural:
                // DU structural domains (DUPropertyPatternDomain, DUTupleDomain)
                // contain slots — dig into each slot's domain to find the Kind enum
                foreach (var (_, slotDomain) in structural.InternalSlots)
                {
                    CollectNamesFromDomain(slotDomain, names);
                }
                break;
        }
    }
}
