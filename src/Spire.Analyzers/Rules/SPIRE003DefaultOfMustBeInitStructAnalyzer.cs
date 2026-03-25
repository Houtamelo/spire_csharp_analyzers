using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
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

        // Flow analysis suppression: if the default(T) is assigned to a local variable
        // and that variable is fully initialized before every read, suppress.
        if (IsFullyInitializedBeforeReads(operation, type))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE003_DefaultOfMustBeInitStruct,
                operation.Syntax.GetLocation(),
                type.Name));
    }

    /// Checks whether the default(T) is assigned to a local variable that becomes
    /// fully initialized (all fields set or whole-variable reassigned) before any read
    /// of that variable. Uses CFG-based analysis that walks blocks and tracks field state
    /// directly, avoiding the need for TransferFunctions (which don't unwrap
    /// IExpressionStatementOperation from CFG blocks).
    private static bool IsFullyInitializedBeforeReads(
        IDefaultValueOperation operation,
        INamedTypeSymbol type)
    {
        try
        {
            return IsFullyInitializedBeforeReadsCore(operation, type);
        }
        catch
        {
            // If anything goes wrong, fall through to report the diagnostic.
            return false;
        }
    }

    private static bool IsFullyInitializedBeforeReadsCore(
        IDefaultValueOperation operation,
        INamedTypeSymbol type)
    {
        // Only suppress for struct types with trackable fields
        if (type.TypeKind != TypeKind.Struct)
            return false;

        // Find the local variable being assigned
        var local = FindAssignmentTargetLocal(operation);
        if (local is null)
            return false;

        // Find the enclosing method declaration syntax
        var methodSyntax = FindEnclosingMethodSyntax(operation.Syntax);
        if (methodSyntax is null)
            return false;

        // Get semantic model from the operation (avoids RS1030)
        var semanticModel = operation.SemanticModel;
        if (semanticModel is null)
            return false;

        // Build CFG for the method
        var cfg = ControlFlowGraph.Create(methodSyntax, semanticModel);
        if (cfg is null)
            return false;

        // Collect instance fields of the type
        var fields = CollectInstanceFields(type);
        if (fields.Length == 0)
            return false;

        // Walk the CFG and track field initialization state for the local variable.
        // Uses a simple worklist algorithm with per-block input states.
        return CfgAllReadsInitialized(cfg, local, type, fields);
    }

    /// Collects all instance fields of a struct type, returns them as an ordered list.
    private static ImmutableArray<IFieldSymbol> CollectInstanceFields(INamedTypeSymbol type)
    {
        var builder = ImmutableArray.CreateBuilder<IFieldSymbol>();
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol { IsStatic: false } field)
                builder.Add(field);
        }

        return builder.ToImmutable();
    }

    /// Per-block field initialization state. Tracks which fields have been initialized
    /// for a specific local variable. Uses a simple bool array (one per field).
    /// Also tracks whether the whole variable was reassigned (all fields initialized at once).
    private readonly struct LocalFieldState
    {
        public bool[] FieldInitialized { get; }
        public bool IsFullyInitialized { get; }

        private LocalFieldState(bool[] fieldInitialized)
        {
            FieldInitialized = fieldInitialized;
            var allInit = true;
            for (int i = 0; i < fieldInitialized.Length; i++)
            {
                if (!fieldInitialized[i])
                {
                    allInit = false;
                    break;
                }
            }

            IsFullyInitialized = allInit;
        }

        public static LocalFieldState AllDefault(int fieldCount)
        {
            return new LocalFieldState(new bool[fieldCount]);
        }

        public static LocalFieldState AllInitialized(int fieldCount)
        {
            var arr = new bool[fieldCount];
            for (int i = 0; i < fieldCount; i++)
                arr[i] = true;
            return new LocalFieldState(arr);
        }

        public LocalFieldState WithFieldInitialized(int ordinal)
        {
            var arr = (bool[])FieldInitialized.Clone();
            arr[ordinal] = true;
            return new LocalFieldState(arr);
        }

        public static LocalFieldState Merge(LocalFieldState a, LocalFieldState b)
        {
            // Conservative merge: a field is only initialized if BOTH paths initialized it
            var arr = new bool[a.FieldInitialized.Length];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = a.FieldInitialized[i] && b.FieldInitialized[i];
            return new LocalFieldState(arr);
        }

        public bool Equals(LocalFieldState other)
        {
            if (FieldInitialized.Length != other.FieldInitialized.Length)
                return false;
            for (int i = 0; i < FieldInitialized.Length; i++)
            {
                if (FieldInitialized[i] != other.FieldInitialized[i])
                    return false;
            }

            return true;
        }
    }

    /// Walks the CFG with a worklist algorithm, tracking field initialization state
    /// for the target local variable. Returns true only if at every read point,
    /// all fields are initialized.
    private static bool CfgAllReadsInitialized(
        ControlFlowGraph cfg,
        ILocalSymbol local,
        INamedTypeSymbol type,
        ImmutableArray<IFieldSymbol> fields)
    {
        var blocks = cfg.Blocks;
        var blockCount = blocks.Length;

        // Per-block input state: field initialization status for the local variable.
        // null means "not yet visited".
        var blockInputs = new LocalFieldState?[blockCount];
        blockInputs[0] = LocalFieldState.AllDefault(fields.Length);

        // Worklist
        var worklist = new Queue<int>();
        var inWorklist = new bool[blockCount];
        worklist.Enqueue(0);
        inWorklist[0] = true;

        var foundAnyRead = false;

        while (worklist.Count > 0)
        {
            var blockOrdinal = worklist.Dequeue();
            inWorklist[blockOrdinal] = false;

            var block = blocks[blockOrdinal];
            if (block.Kind == BasicBlockKind.Entry || block.Kind == BasicBlockKind.Exit)
            {
                PropagateState(block, blockInputs[blockOrdinal]!.Value,
                    blockInputs, worklist, inWorklist);
                continue;
            }

            var state = blockInputs[blockOrdinal]!.Value;

            // Process each operation in the block
            foreach (var topOp in block.Operations)
            {
                var op = UnwrapExpressionStatement(topOp);

                // Check for reads of the variable BEFORE applying the transfer
                if (IsReadOfLocal(op, local))
                {
                    foundAnyRead = true;
                    if (!state.IsFullyInitialized)
                        return false;
                }

                // Apply transfers
                state = ApplyTransfer(op, state, local, type, fields);
            }

            // Propagate to successors
            PropagateState(block, state, blockInputs, worklist, inWorklist);
        }

        return foundAnyRead;
    }

    private static IOperation UnwrapExpressionStatement(IOperation op)
    {
        return op is IExpressionStatementOperation exprStmt ? exprStmt.Operation : op;
    }

    /// Determines if an operation reads the local variable (as a value, not as a write target).
    private static bool IsReadOfLocal(IOperation operation, ILocalSymbol local)
    {
        if (operation is ISimpleAssignmentOperation assignment)
        {
            // Value side may contain reads
            if (ContainsLocalReference(assignment.Value, local))
                return true;

            // Target side: field write (s.Field = val) is not a read of s
            // Direct write (s = expr) is not a read of s
            return false;
        }

        // Any other operation that references the local is a read
        return ContainsLocalReference(operation, local);
    }

    /// Applies a transfer function to update the field initialization state.
    private static LocalFieldState ApplyTransfer(
        IOperation operation,
        LocalFieldState state,
        ILocalSymbol local,
        INamedTypeSymbol type,
        ImmutableArray<IFieldSymbol> fields)
    {
        if (operation is ISimpleAssignmentOperation assignment)
        {
            // Field write: s.Field = val
            if (assignment.Target is IFieldReferenceOperation fieldRef
                && fieldRef.Instance is ILocalReferenceOperation instanceRef
                && SymbolEqualityComparer.Default.Equals(instanceRef.Local, local))
            {
                var ordinal = FindFieldOrdinal(fields, fieldRef.Field);
                if (ordinal >= 0)
                    return state.WithFieldInitialized(ordinal);
            }

            // Whole-variable write: s = expr
            if (assignment.Target is ILocalReferenceOperation directRef
                && SymbolEqualityComparer.Default.Equals(directRef.Local, local))
            {
                return DetermineStateFromValue(assignment.Value, fields.Length);
            }
        }

        return state;
    }

    /// Determines the field state after assigning a value to the variable.
    private static LocalFieldState DetermineStateFromValue(IOperation value, int fieldCount)
    {
        // Unwrap conversions
        while (value is IConversionOperation conv)
            value = conv.Operand;

        // default(T) or default literal → all fields Default
        if (value is IDefaultValueOperation)
            return LocalFieldState.AllDefault(fieldCount);

        // new T(...) with arguments or initializer → all fields Initialized
        if (value is IObjectCreationOperation objCreate)
        {
            if (objCreate.Arguments.Length > 0 || objCreate.Initializer is not null)
                return LocalFieldState.AllInitialized(fieldCount);

            // new T() with no args — same as default for structs
            return LocalFieldState.AllDefault(fieldCount);
        }

        // Unknown source → conservatively assume all initialized (don't flag unknown assignments)
        return LocalFieldState.AllInitialized(fieldCount);
    }

    private static int FindFieldOrdinal(ImmutableArray<IFieldSymbol> fields, IFieldSymbol field)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(fields[i], field))
                return i;
        }

        return -1;
    }

    private static void PropagateState(
        BasicBlock block,
        LocalFieldState state,
        LocalFieldState?[] blockInputs,
        Queue<int> worklist,
        bool[] inWorklist)
    {
        if (block.FallThroughSuccessor?.Destination is { } fallThrough)
            MergeAndEnqueue(fallThrough.Ordinal, state, blockInputs, worklist, inWorklist);

        if (block.ConditionalSuccessor?.Destination is { } conditional)
            MergeAndEnqueue(conditional.Ordinal, state, blockInputs, worklist, inWorklist);
    }

    private static void MergeAndEnqueue(
        int destOrdinal,
        LocalFieldState incoming,
        LocalFieldState?[] blockInputs,
        Queue<int> worklist,
        bool[] inWorklist)
    {
        if (blockInputs[destOrdinal] is { } existing)
        {
            var merged = LocalFieldState.Merge(existing, incoming);
            if (merged.Equals(existing))
                return;
            blockInputs[destOrdinal] = merged;
        }
        else
        {
            blockInputs[destOrdinal] = incoming;
        }

        if (!inWorklist[destOrdinal])
        {
            worklist.Enqueue(destOrdinal);
            inWorklist[destOrdinal] = true;
        }
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
    /// Returns null if the default(T) is not in a local variable assignment context.
    private static ILocalSymbol? FindAssignmentTargetLocal(IDefaultValueOperation operation)
    {
        IOperation? parent = operation.Parent;

        // Skip implicit conversions
        while (parent is IConversionOperation { IsImplicit: true })
            parent = parent.Parent;

        // Case 1: var s = default(T) — initializer in variable declaration
        if (parent is IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator })
            return declarator.Symbol;

        // Case 2: s = default(T) — simple assignment to local
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
