using System.Collections.Generic;
using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.Utils;
using Houtamelo.Spire.Analyzers.Utils.FlowAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE003DefaultOfEnforceInitializationStructAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE003_DefaultOfEnforceInitializationStruct);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var enforceInitializationType = compilationContext.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.EnforceInitializationAttribute");

            if (enforceInitializationType is null)
                return;

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeDefaultValue(operationContext, enforceInitializationType),
                OperationKind.DefaultValue);
        });
    }

    private static void AnalyzeDefaultValue(
        OperationAnalysisContext context,
        INamedTypeSymbol enforceInitializationType)
    {
        var operation = (IDefaultValueOperation)context.Operation;

        var type = operation.Type as INamedTypeSymbol;
        if (type is null)
            return;

        if (type.TypeKind != TypeKind.Struct && type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Enum)
            return;

        if (type.IsReferenceType && IsNullableDefault(operation))
            return;

        if (!EnforceInitializationChecks.IsDefaultValueInvalid(type, enforceInitializationType))
            return;

        if (IsInsideEqualityComparison(operation))
            return;

        if (IsInsideIsPattern(operation))
            return;

        if (IsFullyInitializedBeforeReads(operation, type, enforceInitializationType))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE003_DefaultOfEnforceInitializationStruct,
                operation.Syntax.GetLocation(),
                type.Name));
    }

    /// Checks whether the default(T) is assigned to a local variable that becomes
    /// fully initialized before every read. Uses shared FlowStateWalker infrastructure.
    private static bool IsFullyInitializedBeforeReads(
        IDefaultValueOperation operation,
        INamedTypeSymbol type,
        INamedTypeSymbol enforceInitializationType)
    {
        try
        {
            return IsFullyInitializedBeforeReadsCore(operation, type, enforceInitializationType);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFullyInitializedBeforeReadsCore(
        IDefaultValueOperation operation,
        INamedTypeSymbol type,
        INamedTypeSymbol enforceInitializationType)
    {
        if (type.TypeKind != TypeKind.Struct)
            return false;

        var local = FindAssignmentTargetLocal(operation);
        if (local is null)
            return false;

        var methodSyntax = FindEnclosingMethodSyntax(operation.Syntax);
        if (methodSyntax is null)
            return false;

        var semanticModel = operation.SemanticModel;
        if (semanticModel is null)
            return false;

        var cfg = ControlFlowGraph.Create(methodSyntax, semanticModel)!;

        // Build tracked symbol set for this type
        var fieldMap = TrackedSymbolSet.BuildFieldMap(new[] { type });
        var symbols = new TrackedSymbolSet(enforceInitializationType, fieldMap);

        var owningSymbol = semanticModel.GetDeclaredSymbol(methodSyntax);
        if (owningSymbol is null)
            return false;

        var result = FlowStateWalker.Analyze(cfg, symbols, owningSymbol);

        return AllReadsInitialized(cfg, result, local);
    }

    /// Walks CFG blocks, finds all reads of the local variable, and checks that
    /// the flow analysis state is Initialized at each read point.
    private static bool AllReadsInitialized(
        ControlFlowGraph cfg, FlowAnalysisResult result, ILocalSymbol local)
    {
        var foundRead = false;
        foreach (var block in cfg.Blocks)
        {
            if (block.Kind != BasicBlockKind.Block)
                continue;

            foreach (var op in block.Operations)
            {
                if (IsReadOfLocal(op, local))
                {
                    foundRead = true;
                    var state = result.GetStateAt(op, local);
                    if (state is null || state.Value.InitState != InitState.Initialized)
                        return false;
                }
            }
        }

        return foundRead;
    }

    /// Determines if an operation reads the local variable (as a value, not as a write target).
    private static bool IsReadOfLocal(IOperation operation, ILocalSymbol local)
    {
        // Unwrap IExpressionStatementOperation from CFG blocks
        if (operation is IExpressionStatementOperation exprStmt)
            operation = exprStmt.Operation;

        if (operation is ISimpleAssignmentOperation assignment)
        {
            // Value side may contain reads
            if (ContainsLocalReference(assignment.Value, local))
                return true;

            // Target side: field write (s.Field = val) or direct write (s = expr) is not a read
            return false;
        }

        return ContainsLocalReference(operation, local);
    }

    /// Recursively checks if any descendant is an ILocalReferenceOperation for the local.
    private static bool ContainsLocalReference(IOperation operation, ILocalSymbol local)
    {
        if (operation is ILocalReferenceOperation localRef
            && SymbolEqualityComparer.Default.Equals(localRef.Local, local))
            return true;

        foreach (var child in operation.ChildOperations)
        {
            if (ContainsLocalReference(child, local))
                return true;
        }

        return false;
    }

    /// Finds the local variable that the default(T) is being assigned to.
    private static ILocalSymbol? FindAssignmentTargetLocal(IDefaultValueOperation operation)
    {
        IOperation? parent = operation.Parent;

        while (parent is IConversionOperation { IsImplicit: true })
            parent = parent.Parent;

        if (parent is IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator })
            return declarator.Symbol;

        if (parent is ISimpleAssignmentOperation assignment
            && assignment.Target is ILocalReferenceOperation localRef)
            return localRef.Local;

        return null;
    }

    /// Walks up the syntax tree to find the enclosing method-like declaration.
    private static SyntaxNode? FindEnclosingMethodSyntax(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current is MethodDeclarationSyntax
                or ConstructorDeclarationSyntax
                or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax)
                return current;

            current = current.Parent;
        }

        return null;
    }

    private static bool IsNullableDefault(IDefaultValueOperation operation)
    {
        if (operation.Syntax is DefaultExpressionSyntax defaultExpr)
            return defaultExpr.Type is NullableTypeSyntax;

        IOperation? parent = operation.Parent;
        while (parent is IConversionOperation { IsImplicit: true } conv)
        {
            if (conv.Type is not null && EnforceInitializationChecks.IsNullableAnnotatedReference(conv.Type))
                return true;
            parent = conv.Parent;
        }

        return false;
    }

    private static bool IsInsideEqualityComparison(IDefaultValueOperation operation)
    {
        IOperation? parent = operation.Parent;

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
        IOperation? parent = operation.Parent;

        while (parent is IConversionOperation { IsImplicit: true })
            parent = parent.Parent;

        if (parent is IConstantPatternOperation)
            parent = parent.Parent;

        return parent is IIsPatternOperation;
    }
}
