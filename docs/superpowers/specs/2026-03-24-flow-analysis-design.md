# Flow Analysis Infrastructure for Spire.Analyzers

## Goal

Track variable state changes within method bodies using Roslyn's `ControlFlowGraph` API. Provide cached, per-operation state snapshots that multiple analyzer rules can query to produce more accurate diagnostics — reducing false positives and enabling cross-statement reasoning.

## Use Cases

### 1. EnforceInitialization Struct Init-State Tracking (SPIRE003, SPIRE004)

Currently, SPIRE003 flags every `default(T)` for `[EnforceInitialization]` structs regardless of subsequent reassignment. With flow analysis, SPIRE003 **suppresses** the diagnostic when all fields are initialized before any read of the variable:

```csharp
var s = default(MyStruct);  // s.InitState = Default
s.Field1 = 42;
s.Field2 = "hello";         // all fields assigned → s.InitState = Initialized
Use(s);                      // SPIRE003 suppressed — s is fully initialized before use
```

SPIRE003 still flags `default(T)` at the expression site. Flow analysis is used to suppress when subsequent assignments make it safe — not to move the diagnostic location.

### 2. Discriminated Union Kind-State Tracking (SPIRE013, SPIRE014)

Currently, `FieldAccessSafetyAnalyzer` only detects guards via parent-chain walking (immediate if-condition or switch arm). With flow analysis, kind state persists across statements:

```csharp
if (shape.Kind == Kind.Circle) {
    var r = shape.Radius;    // currently works (parent-chain guard)
}

var kind = shape.Kind;
if (kind == Kind.Circle) {
    var r = shape.Radius;    // currently NOT detected — flow analysis enables this
}
```

Switch arms and cases narrow KindState to the matched variant(s):

```csharp
switch (shape.Kind) {
    case Kind.Circle:
        shape.Radius;        // KindState = {Circle}
        break;
    case Kind.Rectangle:
        shape.Width;         // KindState = {Rectangle}
        break;
}
```

### 3. Null-State Tracking (SPIRE009, SPIRE015)

Track null state for nullable union variables and enum values. Complements the compiler's nullable analysis with Spire-specific knowledge. When the compiler's nullable analysis result is available (via `NullableFlowState`), Spire defers to it. Spire's own null tracking is used when the compiler has no opinion (`NullableFlowState.None` — oblivious context) or for Spire-specific narrowing (e.g., after pattern matching on union variants).

```csharp
Shape? s = GetShape();
if (s != null) {
    switch (s.Value.Kind) { ... }  // NullState = NotNull, safe to access
}
```

## Architecture

### Location

`src/Spire.Analyzers.Utils/FlowAnalysis/` — shared utilities referenced by `Spire.Analyzers`.

`Spire.SourceGenerators` does **not** reference `Spire.Analyzers.Utils` (architectural constraint: no compile-time references between analyzer/generator/codefix projects). Generator-coupled analyzers (SPIRE013/014) will integrate flow analysis via a separate mechanism in a future iteration (shared source linking or local copy).

### Components

#### FlowStateWalker

Core worklist-based fixed-point analyzer over `ControlFlowGraph`.

- Input: `ControlFlowGraph` + `TrackedSymbolSet` (resolved types, field lists)
- Algorithm: standard worklist iteration — initialize entry block state, propagate through basic blocks, merge at join points, repeat until fixed point
- Output: `FlowAnalysisResult`

#### VariableState

Immutable state tracked per variable at each program point.

```
VariableState {
    InitState    : Default | Initialized | MaybeDefault
    FieldStates  : ImmutableArray<InitState>  // indexed by field ordinal, for [EnforceInitialization] structs
    KindState    : KindStateValue             // see below
    NullState    : Null | NotNull | MaybeNull
}
```

`FieldStates` uses `ImmutableArray<InitState>` indexed by field ordinal (not a dictionary). Field ordinals are assigned during `CompilationStartAction` when resolving the type's field list via `INamedTypeSymbol.GetMembers()`. For non-`[EnforceInitialization]` types, `FieldStates` is empty (default `ImmutableArray`). Struct field counts are typically small (2-8), making array indexing both fast and memory-efficient.

**InitState lattice** (for `[EnforceInitialization]` structs):
- `Default` — assigned via `default`, `new T()` without user-defined ctor, etc.
- `Initialized` — assigned via constructor with args, or all fields individually initialized
- `MaybeDefault` — join of `Default` and `Initialized` branches

Variable-level `InitState` is derived from `FieldStates`:
- All fields `Default` → variable `Default`
- All fields `Initialized` → variable `Initialized`
- Mixed → variable `MaybeDefault`

Field state transitions:
- `s = default` → all fields `Default`
- `s.Field1 = val` → `Field1` becomes `Initialized`, others unchanged
- `s = new T(args)` → all fields `Initialized`
- `s = unknownSource` → all fields `MaybeDefault`

The type's field list is resolved at `CompilationStartAction` time via `INamedTypeSymbol.GetMembers()`.

**KindState**:
- `Known(ImmutableHashSet<string>)` — known possible variants, identified by name (matching `PatternAnalyzer.CollectVariants` convention)
- `Unknown` — no information (initial state, after reassignment from unknown source)

Merge: `Known({A}) ∪ Known({B}) = Known({A, B})`. `Unknown ∪ anything = Unknown`.

Note: variant sets are typically small (2-8 members). `ImmutableHashSet<string>` merge cost is negligible at this scale — unlike AnalyzerUtilities' unbounded `PointsToAbstractValue` sets.

**NullState lattice**:
- `Null` — provably null
- `NotNull` — provably not null
- `MaybeNull` — join of `Null` and `NotNull`

#### FlowAnalysisResult

Query interface for cached results.

```
FlowAnalysisResult {
    GetStateAt(IOperation op, ISymbol variable) → VariableState?
    GetOperationsFor(ISymbol variable) → IReadOnlyList<(IOperation, ReadOrWrite, VariableState)>
}
```

- `GetStateAt`: returns variable state at the program point of the given operation. For nested operations (e.g., `IDefaultValueOperation` inside an assignment), walks `IOperation.Parent` to find the enclosing top-level `BasicBlock` operation, then returns state at that block point. Returns null if variable is not tracked.
- `GetOperationsFor`: returns all operations affecting a variable, in execution order, with the state *after* the operation. Enables forward/backward lookups.

#### FlowAnalysisCache

Per-compilation cache shared across all rules.

```
FlowAnalysisCache {
    TrackedSymbolSet Symbols           // computed once in CompilationStartAction
    GetOrCompute(ISymbol containingMember, ControlFlowGraph cfg) → FlowAnalysisResult
}
```

- Keyed by `ISymbol` (the containing member — `IMethodSymbol`, property accessor, constructor, etc.)
- Stored in `ConcurrentDictionary<ISymbol, FlowAnalysisResult>` (using `SymbolEqualityComparer.Default`)
- Created in `CompilationStartAction` with the full `TrackedSymbolSet` (union of all types/symbols needed by all rules). Individual `GetOrCompute` calls do not pass tracked symbols — the set is fixed at construction.
- Local functions and lambdas: keyed by their own `IMethodSymbol`, with separate CFGs obtained via `ControlFlowGraph.GetLocalFunctionControlFlowGraph` / `GetAnonymousFunctionControlFlowGraph`.

### CFG Acquisition

Rules use `RegisterOperationBlockStartAction` to access the CFG:

```
1. RegisterOperationBlockStartAction(blockStartContext):
   a. Check if any tracked types are relevant to this block (cheap pre-filter)
   b. If relevant, get CFG: cfg = blockStartContext.GetControlFlowGraph(operationBlock)
   c. Compute or retrieve: result = cache.GetOrCompute(blockStartContext.OwningSymbol, cfg)
   d. Register inner OperationAction callbacks with captured result

2. Inner OperationAction (e.g., SPIRE003 on IDefaultValueOperation):
   a. Cheap pre-filter: is this a [EnforceInitialization] struct? If not, skip.
   b. Query: result.GetStateAt(operation, targetVariable)
   c. If InitState == Initialized → suppress diagnostic
   d. If InitState == Default → report diagnostic
   e. If MaybeDefault → report diagnostic (conservative)
```

`RegisterOperationBlockStartAction` provides `OperationBlocks` and `OwningSymbol` directly — no need to walk `IOperation.Parent` to find the enclosing method.

### CFG Transfer Functions

Operations that update state:

| Operation | Effect |
|---|---|
| `ISimpleAssignmentOperation` | Update target variable/field state based on value |
| `ICompoundAssignmentOperation` | Target field → `Initialized` (field is being written) |
| `IIncrementOrDecrementOperation` | Target field → `Initialized` |
| `IObjectCreationOperation` | Target → all fields `Initialized` (if has ctor args) or `Default` (if parameterless on `[EnforceInitialization]`) |
| `IDefaultValueOperation` | Target → all fields `Default` |
| `IFieldReferenceOperation` (write) | Specific field → `Initialized` |
| `IPropertyReferenceOperation` (write) | Backing field → `Initialized` (for auto-props) |
| `IInvocationOperation` | See Cross-Method Reasoning below |
| `ISwitchExpressionArmOperation` | Narrow KindState to matched variant(s) |
| `ISwitchCaseOperation` | Narrow KindState to matched variant(s) |
| `IIsPatternOperation` | Narrow KindState/NullState based on pattern |
| `IBinaryOperation` (equality) | Narrow KindState when comparing `.Kind` to enum constant |

#### Conditional Branch Narrowing

In the CFG, conditional branches are represented via `BasicBlock.BranchValue` + `ConditionalSuccessor` / `FallThroughSuccessor`. Narrowing works as follows:

1. **Inspect `BranchValue`**: check if it's a kind comparison (`IBinaryOperation` with `.Kind == EnumConstant`) or null check (`IIsNullOperation`, `IIsPatternOperation` with null pattern).

2. **Apply narrowed state to conditional successor**: the `ConditionalSuccessor` branch gets the narrowed state (e.g., `KindState = {Circle}`).

3. **Apply negated state to fall-through successor**: the `FallThroughSuccessor` gets the complement (e.g., `KindState = Known(AllVariants - {Circle})`).

4. **Compound conditions** (`&&`, `||`): Roslyn's CFG decomposes these into multiple basic blocks with separate branch conditions. Each block gets its own narrowing — no special handling needed. The short-circuit evaluation is already represented in the CFG structure.

### Cross-Method Reasoning

Intra-method analysis only, with conservative cross-method defaults:

- Method returns `[EnforceInitialization]` struct → caller assumes `Initialized`. Rationale: SPIRE003 already flags `default(T)` inside the called method, so if the method compiles without diagnostics, its return value is properly initialized.
- Method takes `ref`/`out` parameter → after call, parameter state becomes `MaybeDefault` (conservative)
- Return type `T?` (nullable) → `MaybeNull`
- Return type `T` (non-nullable in `#nullable enable`) → `NotNull`

No interprocedural control flow graph construction.

### Aliasing

Not tracked in MVP. Each variable is independent. Correct for value-type structs (copy semantics). For reference-type unions, aliasing may produce false negatives — acceptable for MVP.

### Exception Flow

Exception edges in the CFG are walked with conservative state: all tracked variables reset to their most conservative values (`MaybeDefault`, `Unknown`, `MaybeNull`) at catch block entry points. This ensures soundness without modeling exception propagation.

## Performance

### Pre-Filtering

Flow analysis is only invoked when a relevant `OperationBlockStartAction` fires and the block contains tracked types. Most blocks are skipped entirely.

### Caching

CFG construction and fixed-point computation happen once per method body per compilation. The `ConcurrentDictionary` cache ensures no redundant computation when multiple rules analyze the same method.

### Scope Limitation

- Only variables of tracked types are included in state maps (not every local)
- Only method bodies containing tracked operations are analyzed
- No interprocedural graph construction

### Expected Cost

Lightweight compared to AnalyzerUtilities:
- Three simple lattices (3-value init state, small set-based kind state, 3-value null state) vs. AnalyzerUtilities' unbounded `PointsToAbstractValue` sets
- `ImmutableArray<InitState>` indexed by ordinal — no dictionary allocation or hash computation
- `ImmutableHashSet<string>` for KindState — negligible merge cost at typical variant counts (2-8)
- No copy analysis, no points-to analysis
- Fixed-point convergence is fast: lattices are small and monotonic

## Testing Strategy

- Unit tests for `FlowStateWalker` with small method bodies compiled to CFGs
- Integration tests via existing rule test infrastructure: add `should_pass` cases for reassignment scenarios that currently false-positive
- Performance: use existing benchmark infrastructure to measure analyzer overhead with/without flow analysis enabled

## Out of Scope (MVP)

- Aliasing / copy tracking
- Interprocedural analysis (beyond annotations)
- PointsToAnalysis
- Tracking state of fields on fields (nested struct fields)
- Loop iteration count analysis
- `foreach` / `using` / `await using` desugaring (walker traverses CFG blocks produced by Roslyn's lowering — these constructs generate try/finally regions that are walked normally, but no special semantic meaning is attached)
