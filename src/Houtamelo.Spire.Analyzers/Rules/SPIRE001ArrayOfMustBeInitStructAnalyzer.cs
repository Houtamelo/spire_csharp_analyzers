using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE001ArrayOfEnforceInitializationStructAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE001_ArrayOfEnforceInitializationStruct);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var enforceInitializationType = compilationContext.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.Core.EnforceInitializationAttribute");

            if (enforceInitializationType is null)
                return;

            var systemArrayType = compilationContext.Compilation
                .GetTypeByMetadataName("System.Array");

            var gcType = compilationContext.Compilation
                .GetTypeByMetadataName("System.GC");

            var arrayPoolType = compilationContext.Compilation
                .GetTypeByMetadataName("System.Buffers.ArrayPool`1");

            var immutableBuilderType = compilationContext.Compilation
                .GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray`1+Builder");

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeArrayCreation(operationContext, enforceInitializationType),
                OperationKind.ArrayCreation);

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(
                    operationContext, enforceInitializationType, systemArrayType, gcType, arrayPoolType),
                OperationKind.Invocation);

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeSimpleAssignment(
                    operationContext, enforceInitializationType, immutableBuilderType),
                OperationKind.SimpleAssignment);

            compilationContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeStackAlloc(syntaxContext, enforceInitializationType),
                SyntaxKind.StackAllocArrayCreationExpression);
        });
    }

    private static void AnalyzeArrayCreation(
        OperationAnalysisContext context,
        INamedTypeSymbol enforceInitializationType)
    {
        var operation = (IArrayCreationOperation)context.Operation;

        // Skip arrays with an explicit initializer — all elements are provided
        if (operation.Initializer != null)
            return;

        // Skip if any dimension is known to be zero — the array will be empty
        foreach (var dimension in operation.DimensionSizes)
        {
            if (OperationUtilities.IsKnownToBeZero(dimension))
                return;
        }

        // Get element type
        if (operation.Type is not IArrayTypeSymbol arrayType)
            return;

        var elementType = arrayType.ElementType;

        // Check if element type has [EnforceInitialization]
        if (!EnforceInitializationChecks.IsDefaultValueInvalid(elementType, enforceInitializationType))
            return;

        // For reference types, skip if element is nullable-annotated (T?[])
        if (elementType.IsReferenceType && arrayType.ElementNullableAnnotation == NullableAnnotation.Annotated)
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE001_ArrayOfEnforceInitializationStruct,
                operation.Syntax.GetLocation(),
                elementType.Name));
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol enforceInitializationType,
        INamedTypeSymbol? systemArrayType,
        INamedTypeSymbol? gcType,
        INamedTypeSymbol? arrayPoolType)
    {
        var operation = (IInvocationOperation)context.Operation;
        var method = operation.TargetMethod;

        // Array.CreateInstance(typeof(T), ...)
        if (systemArrayType != null
            && method.Name == "CreateInstance"
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, systemArrayType))
        {
            AnalyzeArrayCreateInstance(context, operation, enforceInitializationType);
            return;
        }

        // Array.Resize<T>(ref arr, n)
        if (systemArrayType != null
            && method.Name == "Resize"
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, systemArrayType))
        {
            AnalyzeArrayResize(context, operation, method, enforceInitializationType);
            return;
        }

        // GC.AllocateArray<T>(n) and GC.AllocateUninitializedArray<T>(n)
        if (gcType != null
            && (method.Name == "AllocateArray" || method.Name == "AllocateUninitializedArray")
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, gcType))
        {
            AnalyzeGCAllocate(context, operation, method, enforceInitializationType);
            return;
        }

        // ArrayPool<T>.Rent(n)
        if (arrayPoolType != null
            && method.Name == "Rent"
            && SymbolEqualityComparer.Default.Equals(
                method.ContainingType?.OriginalDefinition, arrayPoolType))
        {
            AnalyzeArrayPoolRent(context, operation, method, enforceInitializationType);
            return;
        }
    }

    private static void AnalyzeArrayCreateInstance(
        OperationAnalysisContext context,
        IInvocationOperation operation,
        INamedTypeSymbol enforceInitializationType)
    {
        // First argument must be typeof(T) — if not, we can't determine the element type
        if (operation.Arguments.Length == 0)
            return;

        var firstArg = operation.Arguments[0].Value;

        if (firstArg is not ITypeOfOperation typeOfOp)
            return;

        var elementType = typeOfOp.TypeOperand;

        // Check size arguments (all after index 0)
        // If ANY is known to be zero, skip
        for (int i = 1; i < operation.Arguments.Length; i++)
        {
            if (OperationUtilities.IsKnownToBeZero(operation.Arguments[i].Value))
                return;
        }

        if (!EnforceInitializationChecks.IsDefaultValueInvalid(elementType, enforceInitializationType))
            return;

        // typeof() never carries nullable annotations — always flag
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE001_ArrayOfEnforceInitializationStruct,
                operation.Syntax.GetLocation(),
                elementType.Name));
    }

    private static void AnalyzeArrayResize(
        OperationAnalysisContext context,
        IInvocationOperation operation,
        IMethodSymbol method,
        INamedTypeSymbol enforceInitializationType)
    {
        // Array.Resize<T>(ref T[] array, int newSize)
        if (method.TypeArguments.Length == 0)
            return;

        var elementType = method.TypeArguments[0];

        // newSize is Arguments[1]
        if (operation.Arguments.Length < 2)
            return;

        if (OperationUtilities.IsKnownToBeZero(operation.Arguments[1].Value))
            return;

        if (!EnforceInitializationChecks.IsDefaultValueInvalid(elementType, enforceInitializationType))
            return;

        if (EnforceInitializationChecks.IsNullableAnnotatedReference(elementType))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE001_ArrayOfEnforceInitializationStruct,
                operation.Syntax.GetLocation(),
                elementType.Name));
    }

    private static void AnalyzeGCAllocate(
        OperationAnalysisContext context,
        IInvocationOperation operation,
        IMethodSymbol method,
        INamedTypeSymbol enforceInitializationType)
    {
        // GC.AllocateArray<T>(int length, ...) / GC.AllocateUninitializedArray<T>(int length, ...)
        if (method.TypeArguments.Length == 0)
            return;

        var elementType = method.TypeArguments[0];

        if (operation.Arguments.Length == 0)
            return;

        if (OperationUtilities.IsKnownToBeZero(operation.Arguments[0].Value))
            return;

        if (!EnforceInitializationChecks.IsDefaultValueInvalid(elementType, enforceInitializationType))
            return;

        if (EnforceInitializationChecks.IsNullableAnnotatedReference(elementType))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE001_ArrayOfEnforceInitializationStruct,
                operation.Syntax.GetLocation(),
                elementType.Name));
    }

    private static void AnalyzeArrayPoolRent(
        OperationAnalysisContext context,
        IInvocationOperation operation,
        IMethodSymbol method,
        INamedTypeSymbol enforceInitializationType)
    {
        // ArrayPool<T>.Rent(int minimumLength)
        var containingType = method.ContainingType;
        if (containingType is not { TypeArguments.Length: > 0 })
            return;

        var elementType = containingType.TypeArguments[0];

        if (operation.Arguments.Length == 0)
            return;

        if (OperationUtilities.IsKnownToBeZero(operation.Arguments[0].Value))
            return;

        if (!EnforceInitializationChecks.IsDefaultValueInvalid(elementType, enforceInitializationType))
            return;

        if (EnforceInitializationChecks.IsNullableAnnotatedReference(elementType))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE001_ArrayOfEnforceInitializationStruct,
                operation.Syntax.GetLocation(),
                elementType.Name));
    }

    private static void AnalyzeSimpleAssignment(
        OperationAnalysisContext context,
        INamedTypeSymbol enforceInitializationType,
        INamedTypeSymbol? immutableBuilderType)
    {
        if (immutableBuilderType is null)
            return;

        var operation = (ISimpleAssignmentOperation)context.Operation;

        // Check if target is a property reference to ImmutableArray<T>.Builder.Count
        if (operation.Target is not IPropertyReferenceOperation propRef)
            return;

        if (propRef.Property.Name != "Count")
            return;

        var containingType = propRef.Property.ContainingType;
        if (!SymbolEqualityComparer.Default.Equals(
                containingType?.OriginalDefinition, immutableBuilderType))
            return;

        // The Builder is ImmutableArray<T>.Builder, so T is in its ContainingType.TypeArguments[0]
        // containingType is ImmutableArray<T>.Builder (a nested type)
        // containingType.ContainingType is ImmutableArray<T>
        var immutableArrayType = containingType.ContainingType;
        if (immutableArrayType is null || immutableArrayType.TypeArguments.Length == 0)
            return;

        var elementType = immutableArrayType.TypeArguments[0];

        if (OperationUtilities.IsKnownToBeZero(operation.Value))
            return;

        if (!EnforceInitializationChecks.IsDefaultValueInvalid(elementType, enforceInitializationType))
            return;

        if (EnforceInitializationChecks.IsNullableAnnotatedReference(elementType))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE001_ArrayOfEnforceInitializationStruct,
                operation.Syntax.GetLocation(),
                elementType.Name));
    }

    private static void AnalyzeStackAlloc(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol enforceInitializationType)
    {
        var node = (StackAllocArrayCreationExpressionSyntax)context.Node;

        // Skip if there is an initializer — all elements are explicitly provided
        if (StackAllocHelper.HasInitializer(node))
            return;

        // Skip if size expression is null (omitted size implies an initializer)
        var sizeExpression = StackAllocHelper.GetSizeExpression(node);
        if (sizeExpression is null)
            return;

        // Skip if the size is known to be zero
        var sizeOperation = context.SemanticModel.GetOperation(sizeExpression);
        if (sizeOperation != null && OperationUtilities.IsKnownToBeZero(sizeOperation))
            return;

        // Get element type
        var elementType = StackAllocHelper.GetElementType(node, context.SemanticModel);
        if (elementType is null)
            return;

        // Check if element type has [EnforceInitialization]
        if (!EnforceInitializationChecks.IsDefaultValueInvalid(elementType, enforceInitializationType))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE001_ArrayOfEnforceInitializationStruct,
                node.GetLocation(),
                elementType.Name));
    }

}
