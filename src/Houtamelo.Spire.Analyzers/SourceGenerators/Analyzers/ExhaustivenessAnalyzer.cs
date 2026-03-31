using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.Utils;
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
            var enforceAttr = compilationCtx.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.EnforceExhaustivenessAttribute");

            if (duAttr is null && enforceAttr is null) return;

            compilationCtx.RegisterOperationAction(
                ctx => AnalyzeSwitch(ctx, duAttr, enforceAttr),
                OperationKind.SwitchExpression, OperationKind.Switch);
        });
    }

    private static void AnalyzeSwitch(
        OperationAnalysisContext ctx, INamedTypeSymbol? duAttr, INamedTypeSymbol? enforceAttr)
    {
        ITypeSymbol? subjectType;
        Location location;

        switch (ctx.Operation)
        {
            case ISwitchExpressionOperation exprOp:
                subjectType = exprOp.Value.Type;
                location = exprOp.Syntax.GetLocation();
                break;
            case ISwitchOperation stmtOp:
                subjectType = stmtOp.Value.Type;
                location = stmtOp.Syntax.GetLocation();
                break;
            default:
                return;
        }

        if (subjectType is null) return;

        // Try DU first — provides variant ordering for the code fix
        UnionTypeInfo? unionInfo = null;
        if (duAttr is not null)
            unionInfo = UnionTypeInfo.TryCreate(UnwrapNullable(subjectType), duAttr);

        if (unionInfo is null)
        {
            // Not a DU — check [EnforceExhaustiveness] on non-enum types
            if (enforceAttr is null) return;
            var actualType = UnwrapNullable(subjectType);
            if (actualType is not INamedTypeSymbol named) return;
            if (named.TypeKind == TypeKind.Enum) return; // SPIRE015 handles enums
            if (!AttributeHelper.HasOrInheritsAttribute(named, enforceAttr)) return;
        }

        ExhaustivenessResult result;
        if (ctx.Operation is ISwitchExpressionOperation switchExprOp)
            result = ExhaustivenessChecker.Check(ctx.Compilation, switchExprOp);
        else
            result = ExhaustivenessChecker.Check(ctx.Compilation, (ISwitchOperation)ctx.Operation);

        ReportDiagnostics(ctx, subjectType, unionInfo, result, location);
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
            return named.TypeArguments[0];
        return type;
    }

    private static void ReportDiagnostics(
        OperationAnalysisContext ctx,
        ITypeSymbol subjectType,
        UnionTypeInfo? unionInfo,
        ExhaustivenessResult result,
        Location location)
    {
        if (result.MissingCases.IsEmpty)
            return;

        var missingNames = ExtractMissingVariantNames(result);
        if (missingNames.Count == 0)
            return;

        // Sort by declaration order when we have DU variant info
        if (unionInfo is not null)
            SortByVariantOrder(missingNames, unionInfo.VariantNames);

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

    /// Sorts names to match declaration order in the union's VariantNames.
    /// Names not in VariantNames (e.g., "null") are placed at the end.
    private static void SortByVariantOrder(List<string> names, ImmutableArray<string> variantNames)
    {
        names.Sort((a, b) =>
        {
            int ia = variantNames.IndexOf(a);
            int ib = variantNames.IndexOf(b);
            if (ia < 0) ia = int.MaxValue;
            if (ib < 0) ib = int.MaxValue;
            return ia.CompareTo(ib);
        });
    }
}
