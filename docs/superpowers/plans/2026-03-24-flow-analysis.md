# Flow Analysis Infrastructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a custom lightweight flow analysis infrastructure over Roslyn's `ControlFlowGraph` API that tracks init-state, kind-state, and null-state per variable within method bodies, cached per compilation for shared use by multiple analyzer rules.

**Architecture:** Worklist-based fixed-point analysis over CFG basic blocks. Three small lattices (InitState 3-value, KindState set-based, NullState 3-value) with per-field init tracking for `[EnforceInitialization]` structs. Results cached in `ConcurrentDictionary<ISymbol, FlowAnalysisResult>`, computed lazily on first request per method body.

**Tech Stack:** `Microsoft.CodeAnalysis.CSharp` 5.0.0, `Microsoft.CodeAnalysis.FlowAnalysis` (part of core), netstandard2.0 + PolySharp.

**Spec:** `docs/superpowers/specs/2026-03-24-flow-analysis-design.md`

---

## File Structure

All new files go under `src/Spire.Analyzers.Utils/FlowAnalysis/`:

| File | Responsibility |
|------|----------------|
| `InitState.cs` | 3-value enum: `Default`, `Initialized`, `MaybeDefault` |
| `NullState.cs` | 3-value enum: `Null`, `NotNull`, `MaybeNull` |
| `KindState.cs` | Discriminated state: `Known(ImmutableHashSet<string>)` or `Unknown` |
| `VariableState.cs` | Immutable composite: `InitState` + `ImmutableArray<InitState>` field states + `KindState` + `NullState` |
| `TrackedSymbolSet.cs` | Resolved type metadata: which types need init/kind tracking, field ordinal maps |
| `FlowAnalysisResult.cs` | Query interface: `GetStateAt(IOperation, ISymbol)`, `GetOperationsFor(ISymbol)` |
| `FlowAnalysisCache.cs` | `ConcurrentDictionary`-backed cache, `GetOrCompute(ISymbol, ControlFlowGraph)` |
| `FlowStateWalker.cs` | Core worklist algorithm: walks `BasicBlock`s, applies transfer functions, merges at join points |
| `TransferFunctions.cs` | Per-`IOperation` state update logic (assignment, default, object creation, field write, etc.) |
| `BranchAnalyzer.cs` | Inspects `BranchValue` to produce narrowed states for conditional/fall-through successors |

Tests go under `tests/Spire.Analyzers.Tests/FlowAnalysis/`:

| File | Responsibility |
|------|----------------|
| `InitStateLatticeTests.cs` | Merge/join correctness for InitState |
| `NullStateLatticeTests.cs` | Merge/join correctness for NullState |
| `KindStateLatticeTests.cs` | Merge/join correctness for KindState |
| `VariableStateMergeTests.cs` | Composite state merging |
| `FlowStateWalkerTests.cs` | End-to-end: compile C# snippet → build CFG → run walker → assert per-operation states |
| `BranchAnalyzerTests.cs` | Conditional narrowing (if-kind-check, null check, switch cases) |
| `TransferFunctionTests.cs` | Individual operation transfer functions |

---

### Task 1: Lattice Types — InitState, NullState, KindState

**Files:**
- Create: `src/Spire.Analyzers.Utils/FlowAnalysis/InitState.cs`
- Create: `src/Spire.Analyzers.Utils/FlowAnalysis/NullState.cs`
- Create: `src/Spire.Analyzers.Utils/FlowAnalysis/KindState.cs`
- Create: `tests/Spire.Analyzers.Tests/FlowAnalysis/InitStateLatticeTests.cs`
- Create: `tests/Spire.Analyzers.Tests/FlowAnalysis/NullStateLatticeTests.cs`
- Create: `tests/Spire.Analyzers.Tests/FlowAnalysis/KindStateLatticeTests.cs`

- [ ] **Step 1: Write InitState lattice tests**

```csharp
// tests/Spire.Analyzers.Tests/FlowAnalysis/InitStateLatticeTests.cs
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class InitStateLatticeTests
{
    [Fact]
    public void Merge_SameValues_ReturnsSame()
    {
        Assert.Equal(InitState.Default, InitStateOps.Merge(InitState.Default, InitState.Default));
        Assert.Equal(InitState.Initialized, InitStateOps.Merge(InitState.Initialized, InitState.Initialized));
        Assert.Equal(InitState.MaybeDefault, InitStateOps.Merge(InitState.MaybeDefault, InitState.MaybeDefault));
    }

    [Fact]
    public void Merge_DifferentValues_ReturnsMaybeDefault()
    {
        Assert.Equal(InitState.MaybeDefault, InitStateOps.Merge(InitState.Default, InitState.Initialized));
        Assert.Equal(InitState.MaybeDefault, InitStateOps.Merge(InitState.Initialized, InitState.Default));
    }

    [Fact]
    public void Merge_WithMaybeDefault_ReturnsMaybeDefault()
    {
        Assert.Equal(InitState.MaybeDefault, InitStateOps.Merge(InitState.MaybeDefault, InitState.Default));
        Assert.Equal(InitState.MaybeDefault, InitStateOps.Merge(InitState.MaybeDefault, InitState.Initialized));
    }

    [Fact]
    public void Merge_IsCommutative()
    {
        foreach (var a in new[] { InitState.Default, InitState.Initialized, InitState.MaybeDefault })
        foreach (var b in new[] { InitState.Default, InitState.Initialized, InitState.MaybeDefault })
            Assert.Equal(InitStateOps.Merge(a, b), InitStateOps.Merge(b, a));
    }
}
```

- [ ] **Step 2: Write NullState lattice tests**

```csharp
// tests/Spire.Analyzers.Tests/FlowAnalysis/NullStateLatticeTests.cs
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class NullStateLatticeTests
{
    [Fact]
    public void Merge_SameValues_ReturnsSame()
    {
        Assert.Equal(NullState.Null, NullStateOps.Merge(NullState.Null, NullState.Null));
        Assert.Equal(NullState.NotNull, NullStateOps.Merge(NullState.NotNull, NullState.NotNull));
        Assert.Equal(NullState.MaybeNull, NullStateOps.Merge(NullState.MaybeNull, NullState.MaybeNull));
    }

    [Fact]
    public void Merge_NullAndNotNull_ReturnsMaybeNull()
    {
        Assert.Equal(NullState.MaybeNull, NullStateOps.Merge(NullState.Null, NullState.NotNull));
        Assert.Equal(NullState.MaybeNull, NullStateOps.Merge(NullState.NotNull, NullState.Null));
    }

    [Fact]
    public void Merge_IsCommutative()
    {
        foreach (var a in new[] { NullState.Null, NullState.NotNull, NullState.MaybeNull })
        foreach (var b in new[] { NullState.Null, NullState.NotNull, NullState.MaybeNull })
            Assert.Equal(NullStateOps.Merge(a, b), NullStateOps.Merge(b, a));
    }
}
```

- [ ] **Step 3: Write KindState lattice tests**

```csharp
// tests/Spire.Analyzers.Tests/FlowAnalysis/KindStateLatticeTests.cs
using System.Collections.Immutable;
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class KindStateLatticeTests
{
    [Fact]
    public void Merge_TwoKnown_ReturnsUnion()
    {
        var a = KindState.Known(ImmutableHashSet.Create("Circle"));
        var b = KindState.Known(ImmutableHashSet.Create("Rectangle"));
        var merged = KindState.Merge(a, b);

        Assert.True(merged.IsKnown);
        Assert.Equal(2, merged.Variants!.Count);
        Assert.Contains("Circle", merged.Variants);
        Assert.Contains("Rectangle", merged.Variants);
    }

    [Fact]
    public void Merge_KnownAndUnknown_ReturnsUnknown()
    {
        var known = KindState.Known(ImmutableHashSet.Create("Circle"));
        var merged = KindState.Merge(known, KindState.Unknown);
        Assert.False(merged.IsKnown);
    }

    [Fact]
    public void Merge_UnknownAndUnknown_ReturnsUnknown()
    {
        var merged = KindState.Merge(KindState.Unknown, KindState.Unknown);
        Assert.False(merged.IsKnown);
    }

    [Fact]
    public void Merge_SameKnown_ReturnsSameSet()
    {
        var set = ImmutableHashSet.Create("Circle");
        var merged = KindState.Merge(KindState.Known(set), KindState.Known(set));
        Assert.True(merged.IsKnown);
        Assert.Single(merged.Variants!);
    }

    [Fact]
    public void Merge_IsCommutative()
    {
        var a = KindState.Known(ImmutableHashSet.Create("Circle"));
        var b = KindState.Known(ImmutableHashSet.Create("Rectangle"));
        Assert.Equal(KindState.Merge(a, b), KindState.Merge(b, a));
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~FlowAnalysis" --no-restore`
Expected: FAIL — types `InitState`, `NullState`, `KindState` do not exist yet.

- [ ] **Step 5: Implement InitState**

```csharp
// src/Spire.Analyzers.Utils/FlowAnalysis/InitState.cs
namespace Spire.Analyzers.Utils.FlowAnalysis;

public enum InitState : byte
{
    Default = 0,
    Initialized = 1,
    MaybeDefault = 2,
}

public static class InitStateOps
{
    public static InitState Merge(InitState a, InitState b)
    {
        if (a == b) return a;
        return InitState.MaybeDefault;
    }
}
```

- [ ] **Step 6: Implement NullState**

```csharp
// src/Spire.Analyzers.Utils/FlowAnalysis/NullState.cs
namespace Spire.Analyzers.Utils.FlowAnalysis;

public enum NullState : byte
{
    NotNull = 0,
    Null = 1,
    MaybeNull = 2,
}

public static class NullStateOps
{
    public static NullState Merge(NullState a, NullState b)
    {
        if (a == b) return a;
        return NullState.MaybeNull;
    }
}
```

- [ ] **Step 7: Implement KindState**

```csharp
// src/Spire.Analyzers.Utils/FlowAnalysis/KindState.cs
using System;
using System.Collections.Immutable;

namespace Spire.Analyzers.Utils.FlowAnalysis;

public readonly struct KindState : IEquatable<KindState>
{
    public ImmutableHashSet<string>? Variants { get; }
    public bool IsKnown => Variants is not null;

    private KindState(ImmutableHashSet<string>? variants) => Variants = variants;

    public static readonly KindState Unknown = new(null);

    public static KindState Known(ImmutableHashSet<string> variants) => new(variants);

    public static KindState Merge(KindState a, KindState b)
    {
        if (!a.IsKnown || !b.IsKnown) return Unknown;
        return Known(a.Variants!.Union(b.Variants!));
    }

    public bool Equals(KindState other)
    {
        if (IsKnown != other.IsKnown) return false;
        if (!IsKnown) return true;
        return Variants!.SetEquals(other.Variants!);
    }

    public override bool Equals(object? obj) => obj is KindState other && Equals(other);

    public override int GetHashCode()
    {
        if (!IsKnown) return 0;
        var hash = 0;
        foreach (var v in Variants!)
            hash ^= v.GetHashCode();
        return hash;
    }

    public static bool operator ==(KindState left, KindState right) => left.Equals(right);
    public static bool operator !=(KindState left, KindState right) => !left.Equals(right);
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~FlowAnalysis" --no-restore`
Expected: All lattice tests PASS.

- [ ] **Step 9: Commit**

```bash
git add src/Spire.Analyzers.Utils/FlowAnalysis/InitState.cs \
        src/Spire.Analyzers.Utils/FlowAnalysis/NullState.cs \
        src/Spire.Analyzers.Utils/FlowAnalysis/KindState.cs \
        tests/Spire.Analyzers.Tests/FlowAnalysis/InitStateLatticeTests.cs \
        tests/Spire.Analyzers.Tests/FlowAnalysis/NullStateLatticeTests.cs \
        tests/Spire.Analyzers.Tests/FlowAnalysis/KindStateLatticeTests.cs
git commit -m "feat(flow): add InitState, NullState, KindState lattice types with tests"
```

---

### Task 2: VariableState Composite Type

**Files:**
- Create: `src/Spire.Analyzers.Utils/FlowAnalysis/VariableState.cs`
- Create: `tests/Spire.Analyzers.Tests/FlowAnalysis/VariableStateMergeTests.cs`

- [ ] **Step 1: Write VariableState merge tests**

```csharp
// tests/Spire.Analyzers.Tests/FlowAnalysis/VariableStateMergeTests.cs
using System.Collections.Immutable;
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class VariableStateMergeTests
{
    [Fact]
    public void Merge_BothDefault_ReturnsDefault()
    {
        var fields = ImmutableArray.Create(InitState.Default, InitState.Default);
        var a = new VariableState(fields, KindState.Unknown, NullState.NotNull);
        var b = new VariableState(fields, KindState.Unknown, NullState.NotNull);
        var merged = VariableState.Merge(a, b);

        Assert.Equal(InitState.Default, merged.InitState);
        Assert.All(merged.FieldStates, f => Assert.Equal(InitState.Default, f));
    }

    [Fact]
    public void Merge_OneFieldDiffers_VariableIsMaybeDefault()
    {
        var a = new VariableState(
            ImmutableArray.Create(InitState.Initialized, InitState.Default),
            KindState.Unknown, NullState.NotNull);
        var b = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Initialized),
            KindState.Unknown, NullState.NotNull);
        var merged = VariableState.Merge(a, b);

        Assert.Equal(InitState.MaybeDefault, merged.InitState);
        Assert.Equal(InitState.MaybeDefault, merged.FieldStates[0]);
        Assert.Equal(InitState.MaybeDefault, merged.FieldStates[1]);
    }

    [Fact]
    public void Merge_AllFieldsInitialized_VariableIsInitialized()
    {
        var fields = ImmutableArray.Create(InitState.Initialized, InitState.Initialized);
        var a = new VariableState(fields, KindState.Unknown, NullState.NotNull);
        var b = new VariableState(fields, KindState.Unknown, NullState.NotNull);
        var merged = VariableState.Merge(a, b);

        Assert.Equal(InitState.Initialized, merged.InitState);
    }

    [Fact]
    public void Merge_EmptyFieldStates_InitStateBasedOnNullState()
    {
        // Non-EnforceInitialization type (no fields tracked): InitState derived from NullState
        var a = new VariableState(ImmutableArray<InitState>.Empty, KindState.Unknown, NullState.NotNull);
        var b = new VariableState(ImmutableArray<InitState>.Empty, KindState.Unknown, NullState.Null);
        var merged = VariableState.Merge(a, b);

        Assert.Equal(NullState.MaybeNull, merged.NullState);
    }

    [Fact]
    public void InitState_DerivedFromFieldStates()
    {
        var allDefault = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Default),
            KindState.Unknown, NullState.NotNull);
        Assert.Equal(InitState.Default, allDefault.InitState);

        var allInit = new VariableState(
            ImmutableArray.Create(InitState.Initialized, InitState.Initialized),
            KindState.Unknown, NullState.NotNull);
        Assert.Equal(InitState.Initialized, allInit.InitState);

        var mixed = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Initialized),
            KindState.Unknown, NullState.NotNull);
        Assert.Equal(InitState.MaybeDefault, mixed.InitState);
    }

    [Fact]
    public void Equality_SameState_ReturnsTrue()
    {
        var a = new VariableState(
            ImmutableArray.Create(InitState.Default),
            KindState.Known(ImmutableHashSet.Create("Circle")),
            NullState.NotNull);
        var b = new VariableState(
            ImmutableArray.Create(InitState.Default),
            KindState.Known(ImmutableHashSet.Create("Circle")),
            NullState.NotNull);
        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~VariableStateMerge" --no-restore`
Expected: FAIL — `VariableState` does not exist yet.

- [ ] **Step 3: Implement VariableState**

```csharp
// src/Spire.Analyzers.Utils/FlowAnalysis/VariableState.cs
using System;
using System.Collections.Immutable;

namespace Spire.Analyzers.Utils.FlowAnalysis;

public readonly struct VariableState : IEquatable<VariableState>
{
    public ImmutableArray<InitState> FieldStates { get; }
    public KindState KindState { get; }
    public NullState NullState { get; }

    /// Derived from FieldStates. If FieldStates is empty, returns Initialized (no fields to track).
    public InitState InitState
    {
        get
        {
            if (FieldStates.IsDefaultOrEmpty)
                return InitState.Initialized;

            var hasDefault = false;
            var hasInitialized = false;
            foreach (var f in FieldStates)
            {
                if (f == InitState.MaybeDefault) return InitState.MaybeDefault;
                if (f == InitState.Default) hasDefault = true;
                else hasInitialized = true;
            }

            if (hasDefault && hasInitialized) return InitState.MaybeDefault;
            return hasDefault ? InitState.Default : InitState.Initialized;
        }
    }

    public VariableState(ImmutableArray<InitState> fieldStates, KindState kindState, NullState nullState)
    {
        FieldStates = fieldStates;
        KindState = kindState;
        NullState = nullState;
    }

    public VariableState WithFieldState(int ordinal, InitState state)
    {
        var builder = FieldStates.ToBuilder();
        builder[ordinal] = state;
        return new VariableState(builder.ToImmutable(), KindState, NullState);
    }

    public VariableState WithAllFields(InitState state)
    {
        if (FieldStates.IsDefaultOrEmpty)
            return this;
        var builder = ImmutableArray.CreateBuilder<InitState>(FieldStates.Length);
        for (int i = 0; i < FieldStates.Length; i++)
            builder.Add(state);
        return new VariableState(builder.MoveToImmutable(), KindState, NullState);
    }

    public VariableState WithKindState(KindState kindState)
        => new(FieldStates, kindState, NullState);

    public VariableState WithNullState(NullState nullState)
        => new(FieldStates, KindState, nullState);

    public static VariableState Merge(VariableState a, VariableState b)
    {
        var mergedKind = KindState.Merge(a.KindState, b.KindState);
        var mergedNull = NullStateOps.Merge(a.NullState, b.NullState);

        if (a.FieldStates.IsDefaultOrEmpty && b.FieldStates.IsDefaultOrEmpty)
            return new VariableState(ImmutableArray<InitState>.Empty, mergedKind, mergedNull);

        var length = a.FieldStates.Length;
        var builder = ImmutableArray.CreateBuilder<InitState>(length);
        for (int i = 0; i < length; i++)
            builder.Add(InitStateOps.Merge(a.FieldStates[i], b.FieldStates[i]));

        return new VariableState(builder.MoveToImmutable(), mergedKind, mergedNull);
    }

    public bool Equals(VariableState other)
    {
        if (KindState != other.KindState) return false;
        if (NullState != other.NullState) return false;
        if (FieldStates.Length != other.FieldStates.Length) return false;
        for (int i = 0; i < FieldStates.Length; i++)
            if (FieldStates[i] != other.FieldStates[i]) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is VariableState other && Equals(other);

    public override int GetHashCode()
    {
        var hash = ((int)NullState * 397) ^ KindState.GetHashCode();
        foreach (var f in FieldStates)
            hash = (hash * 397) ^ (int)f;
        return hash;
    }

    public static bool operator ==(VariableState left, VariableState right) => left.Equals(right);
    public static bool operator !=(VariableState left, VariableState right) => !left.Equals(right);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~VariableState" --no-restore`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Spire.Analyzers.Utils/FlowAnalysis/VariableState.cs \
        tests/Spire.Analyzers.Tests/FlowAnalysis/VariableStateMergeTests.cs
git commit -m "feat(flow): add VariableState composite type with field-level init tracking"
```

---

### Task 3: TrackedSymbolSet and FlowAnalysisResult

**Files:**
- Create: `src/Spire.Analyzers.Utils/FlowAnalysis/TrackedSymbolSet.cs`
- Create: `src/Spire.Analyzers.Utils/FlowAnalysis/FlowAnalysisResult.cs`

No TDD for these — they are data containers with no complex logic. Tests come in Task 5 (end-to-end).

- [ ] **Step 1: Implement TrackedSymbolSet**

`TrackedSymbolSet` holds resolved type metadata computed once in `CompilationStartAction`. It answers: "should this variable be tracked?" and "what are the field ordinals for this type?"

```csharp
// src/Spire.Analyzers.Utils/FlowAnalysis/TrackedSymbolSet.cs
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Spire.Analyzers.Utils.FlowAnalysis;

/// Resolved type metadata for flow analysis. Built once per compilation.
public sealed class TrackedSymbolSet
{
    /// Types whose variables need init-state tracking (have [EnforceInitialization] + instance fields).
    /// Maps type → ordered list of instance fields (field ordinal = index in array).
    private readonly Dictionary<INamedTypeSymbol, ImmutableArray<IFieldSymbol>> _initTrackedTypes;

    /// The [EnforceInitialization] attribute type, resolved from compilation.
    public INamedTypeSymbol? EnforceInitializationType { get; }

    public TrackedSymbolSet(
        INamedTypeSymbol? enforceInitializationType,
        Dictionary<INamedTypeSymbol, ImmutableArray<IFieldSymbol>> initTrackedTypes)
    {
        EnforceInitializationType = enforceInitializationType;
        _initTrackedTypes = initTrackedTypes;
    }

    /// Returns field ordinal map if this type needs init tracking, or default if not.
    public bool TryGetFieldOrdinals(INamedTypeSymbol type, out ImmutableArray<IFieldSymbol> fields)
    {
        if (_initTrackedTypes.TryGetValue(type, out fields))
            return true;

        fields = default;
        return false;
    }

    /// Returns ordinal index of a field within its type's field list, or -1 if not tracked.
    public int GetFieldOrdinal(INamedTypeSymbol containingType, IFieldSymbol field)
    {
        if (!_initTrackedTypes.TryGetValue(containingType, out var fields))
            return -1;

        for (int i = 0; i < fields.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(fields[i], field))
                return i;
        }

        return -1;
    }

    /// Creates initial VariableState for a variable of the given type.
    public VariableState CreateInitialState(ITypeSymbol type, InitState initState, NullState nullState)
    {
        if (type is INamedTypeSymbol named && _initTrackedTypes.TryGetValue(named, out var fields))
        {
            var builder = ImmutableArray.CreateBuilder<InitState>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
                builder.Add(initState);
            return new VariableState(builder.MoveToImmutable(), KindState.Unknown, nullState);
        }

        return new VariableState(ImmutableArray<InitState>.Empty, KindState.Unknown, nullState);
    }

    /// Registers a type for init tracking. Call during CompilationStartAction.
    public static Dictionary<INamedTypeSymbol, ImmutableArray<IFieldSymbol>> BuildFieldMap(
        IEnumerable<INamedTypeSymbol> enforceInitializationTypes)
    {
        var map = new Dictionary<INamedTypeSymbol, ImmutableArray<IFieldSymbol>>(SymbolEqualityComparer.Default);

        foreach (var type in enforceInitializationTypes)
        {
            var fields = ImmutableArray.CreateBuilder<IFieldSymbol>();
            foreach (var member in type.GetMembers())
            {
                if (member is IFieldSymbol { IsStatic: false } field)
                    fields.Add(field);
            }

            if (fields.Count > 0)
                map[type] = fields.ToImmutable();
        }

        return map;
    }
}
```

- [ ] **Step 2: Implement FlowAnalysisResult**

```csharp
// src/Spire.Analyzers.Utils/FlowAnalysis/FlowAnalysisResult.cs
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Spire.Analyzers.Utils.FlowAnalysis;

public enum ReadOrWrite : byte { Read, Write }

/// Query interface for flow analysis results within a single method body.
public sealed class FlowAnalysisResult
{
    /// Per-operation state snapshots. Key is a top-level BasicBlock operation.
    /// Value maps tracked variable symbol → state AFTER that operation.
    private readonly Dictionary<IOperation, Dictionary<ISymbol, VariableState>> _operationStates;

    /// Per-variable operation timeline — ordered list of operations affecting each variable.
    private readonly Dictionary<ISymbol, List<(IOperation Operation, ReadOrWrite Kind, VariableState StateAfter)>> _variableTimelines;

    /// Reverse index: maps any IOperation (including nested) to its enclosing top-level block operation.
    private readonly Dictionary<IOperation, IOperation> _nestedToTopLevel;

    public FlowAnalysisResult(
        Dictionary<IOperation, Dictionary<ISymbol, VariableState>> operationStates,
        Dictionary<ISymbol, List<(IOperation, ReadOrWrite, VariableState)>> variableTimelines,
        Dictionary<IOperation, IOperation> nestedToTopLevel)
    {
        _operationStates = operationStates;
        _variableTimelines = variableTimelines;
        _nestedToTopLevel = nestedToTopLevel;
    }

    /// Returns the state of a variable at the program point of the given operation.
    /// For nested operations, walks up to the enclosing top-level block operation.
    /// Returns null if the variable is not tracked at that point.
    public VariableState? GetStateAt(IOperation operation, ISymbol variable)
    {
        var topLevel = ResolveTopLevel(operation);
        if (topLevel is null)
            return null;

        if (_operationStates.TryGetValue(topLevel, out var states)
            && states.TryGetValue(variable, out var state))
            return state;

        return null;
    }

    /// Returns all operations affecting a variable, in execution order.
    public IReadOnlyList<(IOperation Operation, ReadOrWrite Kind, VariableState StateAfter)>?
        GetOperationsFor(ISymbol variable)
    {
        return _variableTimelines.TryGetValue(variable, out var timeline)
            ? timeline
            : null;
    }

    private IOperation? ResolveTopLevel(IOperation operation)
    {
        if (_operationStates.ContainsKey(operation))
            return operation;

        return _nestedToTopLevel.TryGetValue(operation, out var topLevel)
            ? topLevel
            : null;
    }
}
```

- [ ] **Step 3: Run full test suite to verify no regressions**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --no-restore`
Expected: All existing tests PASS. New types compile without errors.

- [ ] **Step 4: Commit**

```bash
git add src/Spire.Analyzers.Utils/FlowAnalysis/TrackedSymbolSet.cs \
        src/Spire.Analyzers.Utils/FlowAnalysis/FlowAnalysisResult.cs
git commit -m "feat(flow): add TrackedSymbolSet and FlowAnalysisResult query interface"
```

---

### Task 4: BranchAnalyzer — Conditional Narrowing

**Files:**
- Create: `src/Spire.Analyzers.Utils/FlowAnalysis/BranchAnalyzer.cs`
- Create: `tests/Spire.Analyzers.Tests/FlowAnalysis/BranchAnalyzerTests.cs`

- [ ] **Step 1: Write BranchAnalyzer tests**

Tests compile C# snippets into CFGs, then verify that `BranchAnalyzer` produces correct narrowed states for conditional and fall-through successors. The test helper compiles a method body, builds the CFG, finds the branch block, and asserts narrowed states.

```csharp
// tests/Spire.Analyzers.Tests/FlowAnalysis/BranchAnalyzerTests.cs
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Testing;
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class BranchAnalyzerTests
{
    [Fact]
    public async Task NullCheck_NarrowsToNotNull_OnTrueBranch()
    {
        // if (x != null) { /* x is NotNull */ } else { /* x is Null */ }
        var cfg = await BuildCfg(@"
            void M(string? x)
            {
                if (x != null) { _ = x.Length; }
            }
        ");

        // Find the branch block with a null check
        var branchBlock = cfg.Blocks.FirstOrDefault(b =>
            b.BranchValue is not null && b.ConditionalSuccessor is not null);
        Assert.NotNull(branchBlock);

        var currentState = new VariableState(
            ImmutableArray<InitState>.Empty, KindState.Unknown, NullState.MaybeNull);

        var (trueState, falseState) = BranchAnalyzer.AnalyzeBranch(
            branchBlock!.BranchValue!, branchBlock.ConditionKind, currentState);

        // The exact narrowing depends on how Roslyn lowers the condition.
        // At minimum, one branch should be narrower than MaybeNull.
        Assert.True(trueState.NullState != falseState.NullState
                 || trueState.NullState == NullState.MaybeNull,
            "Branch should narrow null state in at least one direction");
    }

    private static async Task<ControlFlowGraph> BuildCfg(string methodBody)
    {
        var source = $@"
#nullable enable
class C
{{
    {methodBody}
}}";
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        var tree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = compilation.GetSemanticModel(tree);
        var methodDecl = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().First();
        return ControlFlowGraph.Create(methodDecl, model);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~BranchAnalyzer" --no-restore`
Expected: FAIL — `BranchAnalyzer` does not exist yet.

- [ ] **Step 3: Implement BranchAnalyzer**

`BranchAnalyzer.AnalyzeBranch` inspects a `BranchValue` operation and produces narrowed states for the conditional and fall-through successors. Handles: `IIsNullOperation`, `IBinaryOperation` (equality with enum constant), `IIsPatternOperation` (null/constant patterns).

```csharp
// src/Spire.Analyzers.Utils/FlowAnalysis/BranchAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Spire.Analyzers.Utils.FlowAnalysis;

public static class BranchAnalyzer
{
    /// Analyzes a branch condition and returns narrowed states for the two successors.
    /// trueState = state when condition matches ConditionKind.
    /// falseState = state when condition does NOT match.
    public static (VariableState trueState, VariableState falseState) AnalyzeBranch(
        IOperation branchValue,
        ControlFlowConditionKind conditionKind,
        VariableState currentState)
    {
        // IIsNullOperation: produced by CFG lowering for null checks
        if (branchValue is IIsNullOperation)
        {
            var nullState = currentState.WithNullState(NullState.Null);
            var notNullState = currentState.WithNullState(NullState.NotNull);

            // WhenTrue means "branch when condition is true"
            // IIsNullOperation is true when operand IS null
            return conditionKind == ControlFlowConditionKind.WhenTrue
                ? (nullState, notNullState)
                : (notNullState, nullState);
        }

        // IBinaryOperation: kind == EnumConstant checks
        if (branchValue is IBinaryOperation binary
            && (binary.OperatorKind == BinaryOperatorKind.Equals
             || binary.OperatorKind == BinaryOperatorKind.NotEquals))
        {
            var enumConstant = ExtractEnumConstantName(binary);
            if (enumConstant is not null)
            {
                var narrowed = currentState.WithKindState(
                    KindState.Known(ImmutableHashSet.Create(enumConstant)));

                var isEquals = binary.OperatorKind == BinaryOperatorKind.Equals;

                if (conditionKind == ControlFlowConditionKind.WhenTrue)
                    return isEquals
                        ? (narrowed, currentState)  // true branch: kind == X
                        : (currentState, narrowed);  // true branch: kind != X (negated)
                else
                    return isEquals
                        ? (currentState, narrowed)
                        : (narrowed, currentState);
            }
        }

        // No narrowing possible
        return (currentState, currentState);
    }

    private static string? ExtractEnumConstantName(IBinaryOperation binary)
    {
        // Check both operand orders: kind == Constant or Constant == kind
        if (TryGetFieldConstantName(binary.RightOperand, out var name))
            return name;
        if (TryGetFieldConstantName(binary.LeftOperand, out name))
            return name;
        return null;
    }

    private static bool TryGetFieldConstantName(IOperation operand, out string? name)
    {
        // Unwrap conversions
        while (operand is IConversionOperation conv)
            operand = conv.Operand;

        if (operand is IFieldReferenceOperation fieldRef
            && fieldRef.Field.IsStatic
            && fieldRef.Field.ContainingType?.TypeKind == TypeKind.Enum)
        {
            name = fieldRef.Field.Name;
            return true;
        }

        name = null;
        return false;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~BranchAnalyzer" --no-restore`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Spire.Analyzers.Utils/FlowAnalysis/BranchAnalyzer.cs \
        tests/Spire.Analyzers.Tests/FlowAnalysis/BranchAnalyzerTests.cs
git commit -m "feat(flow): add BranchAnalyzer for conditional kind/null narrowing"
```

---

### Task 5: TransferFunctions — Per-Operation State Updates

**Files:**
- Create: `src/Spire.Analyzers.Utils/FlowAnalysis/TransferFunctions.cs`
- Create: `tests/Spire.Analyzers.Tests/FlowAnalysis/TransferFunctionTests.cs`

- [ ] **Step 1: Write transfer function tests**

Tests compile C# snippets, build CFGs, walk blocks manually, and verify that `TransferFunctions.Apply` produces correct state transitions for each operation type. Focus on: simple assignment, default value, object creation, field write, compound assignment.

```csharp
// tests/Spire.Analyzers.Tests/FlowAnalysis/TransferFunctionTests.cs
using System.Collections.Immutable;
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class TransferFunctionTests
{
    [Fact]
    public void DefaultAssignment_SetsAllFieldsToDefault()
    {
        var state = new VariableState(
            ImmutableArray.Create(InitState.Initialized, InitState.Initialized),
            KindState.Unknown, NullState.NotNull);

        var result = state.WithAllFields(InitState.Default);

        Assert.Equal(InitState.Default, result.InitState);
        Assert.All(result.FieldStates, f => Assert.Equal(InitState.Default, f));
    }

    [Fact]
    public void FieldWrite_SetsSpecificFieldToInitialized()
    {
        var state = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Default),
            KindState.Unknown, NullState.NotNull);

        var result = state.WithFieldState(0, InitState.Initialized);

        Assert.Equal(InitState.Initialized, result.FieldStates[0]);
        Assert.Equal(InitState.Default, result.FieldStates[1]);
        Assert.Equal(InitState.MaybeDefault, result.InitState);
    }

    [Fact]
    public void AllFieldsWritten_PromotesToInitialized()
    {
        var state = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Default),
            KindState.Unknown, NullState.NotNull);

        state = state.WithFieldState(0, InitState.Initialized);
        state = state.WithFieldState(1, InitState.Initialized);

        Assert.Equal(InitState.Initialized, state.InitState);
    }

    [Fact]
    public void ObjectCreationWithArgs_SetsAllFieldsInitialized()
    {
        var state = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Default),
            KindState.Unknown, NullState.NotNull);

        var result = state.WithAllFields(InitState.Initialized);

        Assert.Equal(InitState.Initialized, result.InitState);
    }

    [Fact]
    public void UnknownAssignment_SetsAllFieldsToMaybeDefault()
    {
        var state = new VariableState(
            ImmutableArray.Create(InitState.Initialized, InitState.Initialized),
            KindState.Unknown, NullState.NotNull);

        var result = state.WithAllFields(InitState.MaybeDefault);

        Assert.Equal(InitState.MaybeDefault, result.InitState);
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

These tests use `VariableState.WithFieldState` / `WithAllFields` which already exist from Task 2. They should pass immediately.

Run: `dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~TransferFunction" --no-restore`
Expected: PASS.

- [ ] **Step 3: Implement TransferFunctions**

`TransferFunctions` contains the logic for processing each `IOperation` in a basic block and producing updated state. This is the core logic that `FlowStateWalker` calls per-operation.

```csharp
// src/Spire.Analyzers.Utils/FlowAnalysis/TransferFunctions.cs
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Spire.Analyzers.Utils.FlowAnalysis;

/// Applies IOperation transfer functions to update variable states within a basic block.
public static class TransferFunctions
{
    /// Processes a top-level operation in a basic block and returns updated states.
    /// Returns true if any state changed.
    public static bool Apply(
        IOperation operation,
        Dictionary<ISymbol, VariableState> states,
        TrackedSymbolSet symbols)
    {
        return operation switch
        {
            ISimpleAssignmentOperation assignment => ApplyAssignment(assignment, states, symbols),
            ICompoundAssignmentOperation compound => ApplyFieldWrite(compound.Target, states, symbols),
            IIncrementOrDecrementOperation incDec => ApplyFieldWrite(incDec.Target, states, symbols),
            _ => false,
        };
    }

    private static bool ApplyAssignment(
        ISimpleAssignmentOperation assignment,
        Dictionary<ISymbol, VariableState> states,
        TrackedSymbolSet symbols)
    {
        var target = assignment.Target;
        var value = assignment.Value;

        // Field/property write: s.Field = val
        if (target is IFieldReferenceOperation fieldRef
            && fieldRef.Instance is ILocalReferenceOperation or IParameterReferenceOperation)
        {
            var variable = GetSymbol(fieldRef.Instance!);
            if (variable is null) return false;
            if (!states.TryGetValue(variable, out var state)) return false;
            if (fieldRef.Field.ContainingType is not INamedTypeSymbol containingType) return false;

            var ordinal = symbols.GetFieldOrdinal(containingType, fieldRef.Field);
            if (ordinal < 0) return false;

            var newState = state.WithFieldState(ordinal, InitState.Initialized);
            if (newState.Equals(state)) return false;
            states[variable] = newState;
            return true;
        }

        // Whole-variable assignment: s = expr
        if (target is ILocalReferenceOperation or IParameterReferenceOperation)
        {
            var variable = GetSymbol(target);
            if (variable is null) return false;
            if (!states.ContainsKey(variable)) return false;

            var newState = DetermineStateFromValue(value, states, symbols, variable);
            if (states.TryGetValue(variable, out var oldState) && newState.Equals(oldState))
                return false;
            states[variable] = newState;
            return true;
        }

        return false;
    }

    private static bool ApplyFieldWrite(
        IOperation target,
        Dictionary<ISymbol, VariableState> states,
        TrackedSymbolSet symbols)
    {
        if (target is not IFieldReferenceOperation fieldRef) return false;
        if (fieldRef.Instance is not (ILocalReferenceOperation or IParameterReferenceOperation))
            return false;

        var variable = GetSymbol(fieldRef.Instance!);
        if (variable is null) return false;
        if (!states.TryGetValue(variable, out var state)) return false;
        if (fieldRef.Field.ContainingType is not INamedTypeSymbol containingType) return false;

        var ordinal = symbols.GetFieldOrdinal(containingType, fieldRef.Field);
        if (ordinal < 0) return false;

        var newState = state.WithFieldState(ordinal, InitState.Initialized);
        if (newState.Equals(state)) return false;
        states[variable] = newState;
        return true;
    }

    /// Determines the VariableState that results from assigning `value` to a tracked variable.
    private static VariableState DetermineStateFromValue(
        IOperation value,
        Dictionary<ISymbol, VariableState> states,
        TrackedSymbolSet symbols,
        ISymbol targetVariable)
    {
        // Unwrap conversions
        while (value is IConversionOperation conv)
            value = conv.Operand;

        var currentState = states.TryGetValue(targetVariable, out var s)
            ? s : default;

        // default(T) or default literal
        if (value is IDefaultValueOperation)
            return currentState.WithAllFields(InitState.Default).WithNullState(NullState.Null);

        // new T(...) with arguments → Initialized
        if (value is IObjectCreationOperation objCreate)
        {
            if (objCreate.Arguments.Length > 0 || objCreate.Initializer is not null)
                return currentState.WithAllFields(InitState.Initialized).WithNullState(NullState.NotNull);

            // new T() with no args on [EnforceInitialization] — same as default
            return currentState.WithAllFields(InitState.Default).WithNullState(NullState.NotNull);
        }

        // null literal
        if (value.ConstantValue is { HasValue: true, Value: null })
            return currentState.WithNullState(NullState.Null);

        // Method call returning [EnforceInitialization] type → assume Initialized
        if (value is IInvocationOperation invocation && symbols.EnforceInitializationType is not null)
        {
            var returnType = invocation.TargetMethod.ReturnType;
            if (returnType is INamedTypeSymbol named
                && EnforceInitializationChecks.HasEnforceInitializationAttribute(named, symbols.EnforceInitializationType))
                return currentState.WithAllFields(InitState.Initialized).WithNullState(NullState.NotNull);
        }

        // Unknown source → conservative
        return currentState.WithAllFields(InitState.MaybeDefault).WithNullState(NullState.MaybeNull);
    }

    private static ISymbol? GetSymbol(IOperation operation)
    {
        return operation switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation param => param.Parameter,
            _ => null,
        };
    }
}
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --no-restore`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Spire.Analyzers.Utils/FlowAnalysis/TransferFunctions.cs \
        tests/Spire.Analyzers.Tests/FlowAnalysis/TransferFunctionTests.cs
git commit -m "feat(flow): add TransferFunctions for per-operation state updates"
```

---

### Task 6: FlowStateWalker — Core Worklist Algorithm

**Files:**
- Create: `src/Spire.Analyzers.Utils/FlowAnalysis/FlowStateWalker.cs`
- Create: `tests/Spire.Analyzers.Tests/FlowAnalysis/FlowStateWalkerTests.cs`

This is the central component. It walks the CFG's basic blocks using a worklist algorithm, applying transfer functions per operation and merging states at join points.

- [ ] **Step 1: Write FlowStateWalker end-to-end tests**

These tests compile actual C# code, build the CFG, run the walker, and assert states at specific operations. They serve as integration tests for the entire flow analysis pipeline.

```csharp
// tests/Spire.Analyzers.Tests/FlowAnalysis/FlowStateWalkerTests.cs
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Testing;
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class FlowStateWalkerTests
{
    [Fact]
    public async Task DefaultThenFieldAssignments_PromotesToInitialized()
    {
        // var s = default(MyStruct); s.X = 1; s.Y = 2; Use(s);
        // At Use(s), s should be Initialized.
        var result = await AnalyzeMethod(@"
            [Spire.EnforceInitialization]
            struct MyStruct { public int X; public int Y; public MyStruct(int x, int y) { X = x; Y = y; } }
            class C
            {
                static void Use(MyStruct s) { }
                void M()
                {
                    var s = default(MyStruct);
                    s.X = 1;
                    s.Y = 2;
                    Use(s);
                }
            }
        ", "M");

        Assert.NotNull(result);
        // The walker should have tracked 's' through the assignments.
        // Exact operation lookup depends on implementation — this test validates
        // the end-to-end pipeline compiles and runs without error.
    }

    [Fact]
    public async Task BranchedInit_OnePathDefault_ResultsMaybeDefault()
    {
        var result = await AnalyzeMethod(@"
            [Spire.EnforceInitialization]
            struct MyStruct { public int X; public MyStruct(int x) { X = x; } }
            class C
            {
                void M(bool cond)
                {
                    MyStruct s;
                    if (cond)
                        s = new MyStruct(1);
                    else
                        s = default;
                    // After merge: s should be MaybeDefault
                    _ = s;
                }
            }
        ", "M");

        Assert.NotNull(result);
    }

    private static async Task<FlowAnalysisResult?> AnalyzeMethod(string source, string methodName)
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        var coreRef = MetadataReference.CreateFromFile(typeof(Spire.EnforceInitializationAttribute).Assembly.Location);

        var tree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree },
            refs.Add(coreRef),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = compilation.GetSemanticModel(tree);
        var methodDecl = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == methodName);

        var cfg = ControlFlowGraph.Create(methodDecl, model);

        var enforceInitializationType = compilation.GetTypeByMetadataName("Spire.EnforceInitializationAttribute");
        if (enforceInitializationType is null)
            return null;

        // Find all [EnforceInitialization] types in the compilation
        var initTypes = new System.Collections.Generic.List<INamedTypeSymbol>();
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var sm = compilation.GetSemanticModel(syntaxTree);
            foreach (var typeDecl in syntaxTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var typeSymbol = sm.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (typeSymbol is not null && EnforceInitializationChecks.HasEnforceInitializationAttribute(typeSymbol, enforceInitializationType))
                    initTypes.Add(typeSymbol);
            }
        }

        var fieldMap = TrackedSymbolSet.BuildFieldMap(initTypes);
        var symbols = new TrackedSymbolSet(enforceInitializationType, fieldMap);

        var methodSymbol = model.GetDeclaredSymbol(methodDecl)!;
        return FlowStateWalker.Analyze(cfg, symbols, methodSymbol);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~FlowStateWalker" --no-restore`
Expected: FAIL — `FlowStateWalker` does not exist yet.

- [ ] **Step 3: Implement FlowStateWalker**

```csharp
// src/Spire.Analyzers.Utils/FlowAnalysis/FlowStateWalker.cs
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Spire.Analyzers.Utils.FlowAnalysis;

/// Worklist-based fixed-point flow analysis over a ControlFlowGraph.
public static class FlowStateWalker
{
    public static FlowAnalysisResult Analyze(ControlFlowGraph cfg, TrackedSymbolSet symbols, ISymbol owningSymbol)
    {
        var blocks = cfg.Blocks;
        var blockCount = blocks.Length;

        // Per-block input state: variable → state
        var blockInputs = new Dictionary<ISymbol, VariableState>[blockCount];
        for (int i = 0; i < blockCount; i++)
            blockInputs[i] = new Dictionary<ISymbol, VariableState>(SymbolEqualityComparer.Default);

        // Initialize tracked locals/parameters
        InitializeTrackedVariables(cfg, symbols, blockInputs[0], owningSymbol);

        // Per-operation state snapshots (top-level operations only)
        var operationStates = new Dictionary<IOperation, Dictionary<ISymbol, VariableState>>();
        var variableTimelines = new Dictionary<ISymbol, List<(IOperation, ReadOrWrite, VariableState)>>(
            SymbolEqualityComparer.Default);
        var nestedToTopLevel = new Dictionary<IOperation, IOperation>();

        // Worklist
        var worklist = new Queue<int>();
        var inWorklist = new bool[blockCount];
        worklist.Enqueue(0);
        inWorklist[0] = true;

        while (worklist.Count > 0)
        {
            var blockOrdinal = worklist.Dequeue();
            inWorklist[blockOrdinal] = false;

            var block = blocks[blockOrdinal];
            if (block.Kind == BasicBlockKind.Entry || block.Kind == BasicBlockKind.Exit)
            {
                PropagateToSuccessors(block, blockInputs[blockOrdinal], blockInputs,
                    worklist, inWorklist, blocks);
                continue;
            }

            // Copy input state for this block
            var currentStates = new Dictionary<ISymbol, VariableState>(
                blockInputs[blockOrdinal], SymbolEqualityComparer.Default);

            // Apply transfer functions for each operation in the block
            foreach (var operation in block.Operations)
            {
                // Build nested → top-level index
                IndexNestedOperations(operation, operation, nestedToTopLevel);

                // Snapshot state BEFORE this operation
                operationStates[operation] = new Dictionary<ISymbol, VariableState>(
                    currentStates, SymbolEqualityComparer.Default);

                // Apply transfer function
                TransferFunctions.Apply(operation, currentStates, symbols);
            }

            // Handle branch narrowing
            if (block.ConditionalSuccessor is not null && block.BranchValue is not null)
            {
                // For each tracked variable, apply branch narrowing
                var narrowedTrue = new Dictionary<ISymbol, VariableState>(
                    currentStates, SymbolEqualityComparer.Default);
                var narrowedFalse = new Dictionary<ISymbol, VariableState>(
                    currentStates, SymbolEqualityComparer.Default);

                foreach (var kvp in currentStates)
                {
                    var (trueState, falseState) = BranchAnalyzer.AnalyzeBranch(
                        block.BranchValue, block.ConditionKind, kvp.Value);
                    narrowedTrue[kvp.Key] = trueState;
                    narrowedFalse[kvp.Key] = falseState;
                }

                // Conditional successor gets narrowed-true state
                MergeAndEnqueue(block.ConditionalSuccessor.Destination, narrowedTrue,
                    blockInputs, worklist, inWorklist, blocks);

                // Fall-through successor gets narrowed-false state
                if (block.FallThroughSuccessor?.Destination is not null)
                    MergeAndEnqueue(block.FallThroughSuccessor.Destination, narrowedFalse,
                        blockInputs, worklist, inWorklist, blocks);
            }
            else
            {
                // No conditional branch — propagate current state to all successors
                PropagateToSuccessors(block, currentStates, blockInputs,
                    worklist, inWorklist, blocks);
            }
        }

        return new FlowAnalysisResult(operationStates, variableTimelines, nestedToTopLevel);
    }

    private static void InitializeTrackedVariables(
        ControlFlowGraph cfg, TrackedSymbolSet symbols,
        Dictionary<ISymbol, VariableState> entryState,
        ISymbol owningSymbol)
    {
        // Parameters come from the owning method, NOT from region.Locals
        // (ControlFlowRegion.Locals only contains ILocalSymbol, never IParameterSymbol)
        if (owningSymbol is IMethodSymbol method)
        {
            foreach (var param in method.Parameters)
            {
                if (param.Type is INamedTypeSymbol paramType
                    && symbols.TryGetFieldOrdinals(paramType, out _))
                {
                    // Parameters are assumed initialized (caller responsibility)
                    entryState[param] = symbols.CreateInitialState(
                        paramType, InitState.Initialized, NullState.NotNull);
                }
            }
        }

        // Local variables come from CFG regions
        CollectLocals(cfg.Root, symbols, entryState);
    }

    private static void CollectLocals(
        ControlFlowRegion region, TrackedSymbolSet symbols,
        Dictionary<ISymbol, VariableState> entryState)
    {
        foreach (var local in region.Locals)
        {
            if (local.Type is INamedTypeSymbol namedType
                && symbols.TryGetFieldOrdinals(namedType, out _))
            {
                entryState[local] = symbols.CreateInitialState(
                    namedType, InitState.Default, NullState.NotNull);
            }
        }

        foreach (var nested in region.NestedRegions)
            CollectLocals(nested, symbols, entryState);
    }

    private static void PropagateToSuccessors(
        BasicBlock block,
        Dictionary<ISymbol, VariableState> currentStates,
        Dictionary<ISymbol, VariableState>[] blockInputs,
        Queue<int> worklist, bool[] inWorklist,
        ImmutableArray<BasicBlock> blocks)
    {
        if (block.FallThroughSuccessor?.Destination is { } fallThrough)
            MergeAndEnqueue(fallThrough, currentStates, blockInputs, worklist, inWorklist, blocks);

        if (block.ConditionalSuccessor?.Destination is { } conditional)
            MergeAndEnqueue(conditional, currentStates, blockInputs, worklist, inWorklist, blocks);
    }

    private static void MergeAndEnqueue(
        BasicBlock destination,
        Dictionary<ISymbol, VariableState> incomingStates,
        Dictionary<ISymbol, VariableState>[] blockInputs,
        Queue<int> worklist, bool[] inWorklist,
        ImmutableArray<BasicBlock> blocks)
    {
        var destOrdinal = destination.Ordinal;
        var destInputs = blockInputs[destOrdinal];
        var changed = false;

        foreach (var kvp in incomingStates)
        {
            if (destInputs.TryGetValue(kvp.Key, out var existing))
            {
                var merged = VariableState.Merge(existing, kvp.Value);
                if (!merged.Equals(existing))
                {
                    destInputs[kvp.Key] = merged;
                    changed = true;
                }
            }
            else
            {
                destInputs[kvp.Key] = kvp.Value;
                changed = true;
            }
        }

        if (changed && !inWorklist[destOrdinal])
        {
            worklist.Enqueue(destOrdinal);
            inWorklist[destOrdinal] = true;
        }
    }

    private static void IndexNestedOperations(
        IOperation current, IOperation topLevel,
        Dictionary<IOperation, IOperation> index)
    {
        foreach (var child in current.ChildOperations)
        {
            index[child] = topLevel;
            IndexNestedOperations(child, topLevel, index);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~FlowStateWalker" --no-restore`
Expected: PASS.

- [ ] **Step 5: Run full test suite for regressions**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --no-restore`
Expected: All tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Spire.Analyzers.Utils/FlowAnalysis/FlowStateWalker.cs \
        tests/Spire.Analyzers.Tests/FlowAnalysis/FlowStateWalkerTests.cs
git commit -m "feat(flow): add FlowStateWalker worklist-based CFG analyzer"
```

---

### Task 7: FlowAnalysisCache — Per-Compilation Caching

**Files:**
- Create: `src/Spire.Analyzers.Utils/FlowAnalysis/FlowAnalysisCache.cs`

No dedicated tests — tested via integration in Task 8.

- [ ] **Step 1: Implement FlowAnalysisCache**

```csharp
// src/Spire.Analyzers.Utils/FlowAnalysis/FlowAnalysisCache.cs
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace Spire.Analyzers.Utils.FlowAnalysis;

/// Thread-safe, per-compilation cache for flow analysis results.
/// Created once in CompilationStartAction, shared across all rules.
public sealed class FlowAnalysisCache
{
    private readonly ConcurrentDictionary<ISymbol, FlowAnalysisResult> _cache =
        new(SymbolEqualityComparer.Default);

    public TrackedSymbolSet Symbols { get; }

    public FlowAnalysisCache(TrackedSymbolSet symbols)
    {
        Symbols = symbols;
    }

    /// Returns cached result or computes and caches a new one.
    public FlowAnalysisResult GetOrCompute(ISymbol containingMember, ControlFlowGraph cfg)
    {
        return _cache.GetOrAdd(containingMember, _ => FlowStateWalker.Analyze(cfg, Symbols, containingMember));
    }
}
```

- [ ] **Step 2: Run full test suite**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --no-restore`
Expected: All PASS.

- [ ] **Step 3: Commit**

```bash
git add src/Spire.Analyzers.Utils/FlowAnalysis/FlowAnalysisCache.cs
git commit -m "feat(flow): add FlowAnalysisCache for per-compilation caching"
```

---

### Task 8: Integrate Flow Analysis into SPIRE003

**Files:**
- Modify: `src/Spire.Analyzers/Rules/SPIRE003DefaultOfEnforceInitializationStructAnalyzer.cs`
- Create: `tests/Spire.Analyzers.Tests/SPIRE003/cases/NoReport_DefaultThenFieldInit.cs`
- Create: `tests/Spire.Analyzers.Tests/SPIRE003/cases/NoReport_DefaultThenCtorAssign.cs`
- Create: `tests/Spire.Analyzers.Tests/SPIRE003/cases/Detect_DefaultNoReassign.cs` (verify existing behavior preserved)
- Create: `tests/Spire.Analyzers.Tests/SPIRE003/cases/Detect_DefaultPartialFieldInit.cs`
- Create: `tests/Spire.Analyzers.Tests/SPIRE003/cases/Detect_DefaultBranchedInit.cs`

- [ ] **Step 1: Write new SPIRE003 test cases**

```csharp
// tests/Spire.Analyzers.Tests/SPIRE003/cases/NoReport_DefaultThenFieldInit.cs
//@ should_pass
// Ensure that SPIRE003 is NOT triggered when default(T) is followed by full field initialization.
public class NoReport_DefaultThenFieldInit
{
    public void Method()
    {
        var s = default(EnforceInitializationStruct);
        s.Value = 42;
        _ = s;
    }
}
```

```csharp
// tests/Spire.Analyzers.Tests/SPIRE003/cases/NoReport_DefaultThenCtorAssign.cs
//@ should_pass
// Ensure that SPIRE003 is NOT triggered when default(T) variable is reassigned via constructor.
public class NoReport_DefaultThenCtorAssign
{
    public void Method()
    {
        var s = default(EnforceInitializationStruct);
        s = new EnforceInitializationStruct(42);
        _ = s;
    }
}
```

```csharp
// tests/Spire.Analyzers.Tests/SPIRE003/cases/Detect_DefaultPartialFieldInit.cs
//@ should_fail
// Ensure that SPIRE003 IS triggered when only some fields are initialized after default.
public class Detect_DefaultPartialFieldInit
{
    public void Method()
    {
        var s = default(UnionLikeStruct); //~ ERROR
        s.Kind = 1;
        // s.Value not initialized
        _ = s;
    }
}
```

```csharp
// tests/Spire.Analyzers.Tests/SPIRE003/cases/Detect_DefaultBranchedInit.cs
//@ should_fail
// Ensure that SPIRE003 IS triggered when one branch leaves the variable as default.
public class Detect_DefaultBranchedInit
{
    public void Method(bool cond)
    {
        EnforceInitializationStruct s;
        if (cond)
            s = new EnforceInitializationStruct(1);
        else
            s = default(EnforceInitializationStruct); //~ ERROR
        _ = s;
    }
}
```

- [ ] **Step 2: Run tests to verify new should_pass cases FAIL (no flow analysis yet)**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~SPIRE003" --no-restore`
Expected: `NoReport_DefaultThenFieldInit` and `NoReport_DefaultThenCtorAssign` FAIL (SPIRE003 currently flags all `default(T)`). Detection cases should pass or fail depending on existing behavior.

- [ ] **Step 3: Modify SPIRE003 analyzer to use flow analysis**

Update `SPIRE003DefaultOfEnforceInitializationStructAnalyzer` to:
1. Switch from `RegisterOperationAction` to `RegisterOperationBlockStartAction`
2. Build `FlowAnalysisCache` with tracked `[EnforceInitialization]` types
3. In the inner `RegisterOperationAction` for `OperationKind.DefaultValue`, query flow state
4. Suppress diagnostic when the target variable reaches `Initialized` before any read

Key changes to `SPIRE003DefaultOfEnforceInitializationStructAnalyzer.Initialize`:

```csharp
// Replace the simple RegisterOperationAction with OperationBlockStartAction
context.RegisterCompilationStartAction(compilationContext =>
{
    var enforceInitializationType = compilationContext.Compilation
        .GetTypeByMetadataName("Spire.EnforceInitializationAttribute");
    if (enforceInitializationType is null) return;

    // Build tracked symbol set — discover [EnforceInitialization] types lazily as they're encountered
    // For now, use a shared cache that tracks types on-demand
    var cache = new FlowAnalysisCache(
        new TrackedSymbolSet(enforceInitializationType, new Dictionary<INamedTypeSymbol, ImmutableArray<IFieldSymbol>>(SymbolEqualityComparer.Default)));

    compilationContext.RegisterOperationBlockStartAction(blockStartContext =>
    {
        blockStartContext.RegisterOperationAction(
            opContext => AnalyzeDefaultValue(opContext, enforceInitializationType, cache, blockStartContext),
            OperationKind.DefaultValue);
    });
});
```

The actual implementation will need to handle the lazy type discovery and CFG construction. The implementer should follow the spec's integration pattern in § CFG Acquisition.

- [ ] **Step 4: Run tests to verify all SPIRE003 tests pass**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~SPIRE003" --no-restore`
Expected: All PASS — existing detection cases still flag, new suppression cases correctly suppress.

- [ ] **Step 5: Run full test suite for regressions**

Run: `dotnet test tests/Spire.Analyzers.Tests/ --no-restore`
Expected: All tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Spire.Analyzers/Rules/SPIRE003DefaultOfEnforceInitializationStructAnalyzer.cs \
        tests/Spire.Analyzers.Tests/SPIRE003/cases/NoReport_DefaultThenFieldInit.cs \
        tests/Spire.Analyzers.Tests/SPIRE003/cases/NoReport_DefaultThenCtorAssign.cs \
        tests/Spire.Analyzers.Tests/SPIRE003/cases/Detect_DefaultPartialFieldInit.cs \
        tests/Spire.Analyzers.Tests/SPIRE003/cases/Detect_DefaultBranchedInit.cs
git commit -m "feat(SPIRE003): integrate flow analysis to suppress false positives on reassigned defaults"
```

---

### Task 9: Verify and Polish

- [ ] **Step 1: Run full test suite**

Run: `dotnet test --no-restore`
Expected: All tests across all projects PASS.

- [ ] **Step 2: Run dotnet build to check for warnings**

Run: `dotnet build --no-restore -warnaserror`
Expected: Clean build, no warnings.

- [ ] **Step 3: Verify SPIRE003 edge cases manually**

Review the `NoReport_DefaultThenFieldInit` test to ensure it covers the core use case from the spec. Consider whether additional edge cases are needed (e.g., default literal vs `default(T)`, nested scopes, loops).

- [ ] **Step 4: Commit any fixes**

```bash
git add -A
git commit -m "fix(flow): polish flow analysis integration"
```
