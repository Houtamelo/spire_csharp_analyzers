using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Spire.Analyzers.Utils;

namespace Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE003DefaultOfMustBeInitStructAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE003_DefaultOfMustBeInitStruct);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var mustBeInitType = compilationContext.Compilation
                .GetTypeByMetadataName("Spire.MustBeInitAttribute");

            if (mustBeInitType is null)
                return;

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeDefaultValue(operationContext, mustBeInitType),
                OperationKind.DefaultValue);
        });
    }

    private static void AnalyzeDefaultValue(
        OperationAnalysisContext context,
        INamedTypeSymbol mustBeInitType)
    {
        var operation = (IDefaultValueOperation)context.Operation;

        // Get the type being defaulted
        var type = operation.Type as INamedTypeSymbol;
        if (type is null)
            return;

        if (type.TypeKind != TypeKind.Struct && type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Enum)
            return;

        // For reference types, skip if the user explicitly wrote a nullable type.
        // Can't use operation.Type.NullableAnnotation here — Roslyn marks default(T)
        // as Annotated for reference types since the result is always null.
        if (type.IsReferenceType && IsNullableDefault(operation))
            return;

        if (!MustBeInitChecks.IsDefaultValueInvalid(type, mustBeInitType))
            return;

        // Skip if inside an equality/inequality binary operation (x == default, x != default)
        if (IsInsideEqualityComparison(operation))
            return;

        // Skip if inside an is-pattern (s is default(T))
        if (IsInsideIsPattern(operation))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE003_DefaultOfMustBeInitStruct,
                operation.Syntax.GetLocation(),
                type.Name));
    }

    /// For reference types, determines whether the default expression targets a nullable type.
    /// For explicit default(T?): checks the syntax for NullableTypeSyntax.
    /// For default literal: checks the inferred type's NullableAnnotation from context.
    private static bool IsNullableDefault(IDefaultValueOperation operation)
    {
        // default(T?) — syntax explicitly uses nullable type
        if (operation.Syntax is DefaultExpressionSyntax defaultExpr)
            return defaultExpr.Type is NullableTypeSyntax;

        // default literal — type inferred from assignment/return context.
        // Walk up through implicit conversions to find the target type.
        IOperation? parent = operation.Parent;
        while (parent is IConversionOperation { IsImplicit: true } conv)
        {
            if (conv.Type is not null && MustBeInitChecks.IsNullableAnnotatedReference(conv.Type))
                return true;
            parent = conv.Parent;
        }

        return false;
    }

    private static bool IsInsideEqualityComparison(IDefaultValueOperation operation)
    {
        // Walk up through conversions to find the effective parent
        IOperation? parent = operation.Parent;

        // Skip implicit conversions wrapping the default value
        while (parent is IConversionOperation { IsImplicit: true })
            parent = parent.Parent;

        if (parent is IBinaryOperation binary)
        {
            return binary.OperatorKind == BinaryOperatorKind.Equals
                || binary.OperatorKind == BinaryOperatorKind.NotEquals;
        }

        return false;
    }

    private static bool IsInsideIsPattern(IDefaultValueOperation operation)
    {
        // Walk up through conversions and constant patterns to find if we're in a pattern match
        IOperation? parent = operation.Parent;

        while (parent is IConversionOperation { IsImplicit: true })
            parent = parent.Parent;

        // default(T) inside a constant pattern: IConstantPatternOperation -> IIsPatternOperation
        if (parent is IConstantPatternOperation)
            parent = parent.Parent;

        return parent is IIsPatternOperation;
    }
}
