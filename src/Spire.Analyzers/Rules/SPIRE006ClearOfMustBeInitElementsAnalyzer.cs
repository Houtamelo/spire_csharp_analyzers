using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Spire.Analyzers.Utils;

namespace Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE006ClearOfMustBeInitElementsAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE006_ClearOfMustBeInitElements);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var arrayType = compilationContext.Compilation
                .GetTypeByMetadataName("System.Array");

            var spanType = compilationContext.Compilation
                .GetTypeByMetadataName("System.Span`1");

            var mustBeInitType = compilationContext.Compilation
                .GetTypeByMetadataName("Spire.Analyzers.MustBeInitAttribute");

            if (mustBeInitType is null)
                return;

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, arrayType, spanType, mustBeInitType),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol? arrayType,
        INamedTypeSymbol? spanType,
        INamedTypeSymbol mustBeInitType)
    {
        var operation = (IInvocationOperation)context.Operation;
        var method = operation.TargetMethod;

        if (method.Name != "Clear")
            return;

        ITypeSymbol? elementType;
        string methodLabel;

        if (TryGetArrayClearElement(operation, method, arrayType, out elementType))
        {
            methodLabel = "Array.Clear";
        }
        else if (TryGetSpanClearElement(method, spanType, out elementType))
        {
                methodLabel = $"Span<{elementType!.Name}>.Clear";
        }
        else
        {
            return;
        }

        if (elementType is not INamedTypeSymbol namedElement)
            return;

        if (namedElement.TypeKind != TypeKind.Struct && namedElement.TypeKind != TypeKind.Class)
            return;

        if (!MustBeInitChecks.HasMustBeInitAttribute(namedElement, mustBeInitType))
            return;

        if (!MustBeInitChecks.HasInstanceFields(namedElement))
            return;

        // For reference types, skip if nullable-annotated
        if (MustBeInitChecks.IsNullableAnnotatedReference(elementType))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE006_ClearOfMustBeInitElements,
                operation.Syntax.GetLocation(),
                methodLabel,
                namedElement.Name));
    }

    private static bool TryGetArrayClearElement(
        IInvocationOperation operation,
        IMethodSymbol method,
        INamedTypeSymbol? arrayType,
        out ITypeSymbol? elementType)
    {
        elementType = null;

        if (arrayType is null)
            return false;

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, arrayType))
            return false;

        if (operation.Arguments.Length == 0)
            return false;

        var firstArg = operation.Arguments[0].Value;

        while (firstArg is IConversionOperation { IsImplicit: true } conv)
            firstArg = conv.Operand;

        if (firstArg.Type is not IArrayTypeSymbol arraySymbol)
            return false;

        elementType = arraySymbol.ElementType;
        return true;
    }

    private static bool TryGetSpanClearElement(
        IMethodSymbol method,
        INamedTypeSymbol? spanType,
        out ITypeSymbol? elementType)
    {
        elementType = null;

        if (spanType is null)
            return false;

        if (method.IsStatic || method.Parameters.Length != 0)
            return false;

        var containingType = method.ContainingType;

        if (!SymbolEqualityComparer.Default.Equals(containingType?.OriginalDefinition, spanType))
            return false;

        if (containingType.TypeArguments.Length != 1)
            return false;

        elementType = containingType.TypeArguments[0];

        // Skip unresolved generic type parameters — we can't know if they are [MustBeInit].
        if (elementType.TypeKind == TypeKind.TypeParameter)
        {
            elementType = null;
            return false;
        }

        return true;
    }

}
