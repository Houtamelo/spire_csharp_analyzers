using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Spire.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeSafetyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            AnalyzerDescriptors.SPIRE011_FieldTypeMismatch,
            AnalyzerDescriptors.SPIRE012_FieldCountMismatch);

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
                ctx => AnalyzeRecursivePattern(ctx, duAttr),
                OperationKind.RecursivePattern);
        });
    }

    private static void AnalyzeRecursivePattern(
        OperationAnalysisContext ctx, INamedTypeSymbol duAttr)
    {
        var recursive = (IRecursivePatternOperation)ctx.Operation;

        // Must have a Deconstruct method
        if (recursive.DeconstructSymbol is not IMethodSymbol method)
            return;

        var containingType = method.ContainingType;
        if (containingType is null)
            return;

        // Only analyze struct unions with [DiscriminatedUnion]
        var unionInfo = UnionTypeInfo.TryCreate(containingType, duAttr);
        if (unionInfo is null || !unionInfo.IsStructUnion)
            return;

        var subpatterns = recursive.DeconstructionSubpatterns;
        var parameters = method.Parameters;

        // Field count check — Roslyn usually catches this at compile time,
        // but report it if it somehow gets through
        if (subpatterns.Length != parameters.Length)
        {
            var variantName = ResolveVariantName(subpatterns, unionInfo);
            var expectedFieldCount = parameters.Length - 1;
            var actualFieldCount = subpatterns.Length - 1;

            ctx.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDescriptors.SPIRE012_FieldCountMismatch,
                recursive.Syntax.GetLocation(),
                variantName ?? containingType.Name,
                expectedFieldCount,
                actualFieldCount));
            return;
        }

        // Resolve variant name and its actual field types from the factory method
        var resolvedVariant = ResolveVariantName(subpatterns, unionInfo);
        var factoryParams = resolvedVariant is not null
            ? GetVariantFieldTypes(containingType, resolvedVariant)
            : null;

        // Type check each field subpattern (skip index 0, which is the Kind param)
        for (int i = 1; i < subpatterns.Length; i++)
        {
            var subpattern = subpatterns[i];
            var deconstructParamType = parameters[i].Type;

            // Determine the expected type: use the factory method's param type if available,
            // fall back to the Deconstruct param type.
            // This handles the case where Deconstruct uses `object?` for boxed single-field variants.
            var fieldIndex = i - 1; // 0-based field index (excluding Kind)
            var expectedType = factoryParams is not null && fieldIndex < factoryParams.Value.Length
                ? factoryParams.Value[fieldIndex]
                : deconstructParamType;

            ITypeSymbol? actualType = null;

            switch (subpattern)
            {
                case IDiscardPatternOperation:
                    // Untyped discard — always OK
                    continue;

                case IDeclarationPatternOperation declPattern:
                    // `var x` — infers type from context, always OK
                    if (declPattern.InputType is not null &&
                        SymbolEqualityComparer.Default.Equals(
                            declPattern.InputType, declPattern.MatchedType))
                        continue;

                    // `object? val` — boxing is always valid for any field type
                    if (IsObjectOrNullableObject(declPattern.MatchedType))
                        continue;

                    actualType = declPattern.MatchedType;
                    break;

                case IConstantPatternOperation constPattern:
                    actualType = constPattern.Value?.Type;
                    break;

                default:
                    continue;
            }

            if (actualType is null)
                continue;

            if (SymbolEqualityComparer.Default.Equals(actualType, expectedType))
                continue;

            // Also allow matching against the Deconstruct param type directly
            // (e.g., `object? val` against `object?` param)
            if (SymbolEqualityComparer.Default.Equals(actualType, deconstructParamType))
                continue;

            ctx.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDescriptors.SPIRE011_FieldTypeMismatch,
                subpattern.Syntax.GetLocation(),
                resolvedVariant ?? containingType.Name,
                fieldIndex,
                expectedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                actualType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    /// Returns true if the type is `object` or `object?`.
    private static bool IsObjectOrNullableObject(ITypeSymbol? type)
    {
        if (type is null)
            return false;
        return type.SpecialType == SpecialType.System_Object;
    }

    /// Resolves the variant name from the first subpattern (Kind constant).
    private static string? ResolveVariantName(
        ImmutableArray<IPatternOperation> subpatterns, UnionTypeInfo unionInfo)
    {
        if (subpatterns.IsEmpty)
            return null;

        if (subpatterns[0] is not IConstantPatternOperation constant)
            return null;

        var valueOp = constant.Value;
        if (valueOp is null || valueOp.Type is null)
            return null;

        var kindEnum = unionInfo.KindEnumType;
        if (kindEnum is null)
            return null;

        if (!SymbolEqualityComparer.Default.Equals(valueOp.Type, kindEnum))
            return null;

        if (!valueOp.ConstantValue.HasValue)
            return null;

        var value = valueOp.ConstantValue.Value;
        if (value is int ordinal && ordinal >= 0 && ordinal < unionInfo.VariantNames.Length)
            return unionInfo.VariantNames[ordinal];

        return null;
    }

    /// Gets the actual field types for a variant from its factory method.
    /// Returns the parameter types of the static factory method matching the variant name.
    private static ImmutableArray<ITypeSymbol>? GetVariantFieldTypes(
        INamedTypeSymbol unionType, string variantName)
    {
        // Look for the static factory method (e.g., `public static partial Shape Circle(double radius)`)
        var factoryMethod = unionType.GetMembers(variantName)
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsStatic && m.MethodKind == MethodKind.Ordinary);

        if (factoryMethod is null)
            return null;

        return factoryMethod.Parameters
            .Select(p => p.Type)
            .ToImmutableArray();
    }
}
