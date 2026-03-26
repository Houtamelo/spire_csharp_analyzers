# Recursive Pattern Exhaustiveness Analysis

## Problem

Spire's current exhaustiveness analyzer (`PatternAnalyzer`) only handles flat, single-level patterns on discriminated unions. It cannot reason about nested/recursive patterns where union values appear inside tuples, property patterns, or other compound structures.

Example that is not handled correctly today:

```csharp
(shape, condition) switch {
    ((Shape.Kind.Circle, double radius), true) => ...,
    ((Shape.Kind.Circle, double radius), false) => ...,
    ((Shape.Kind.Rectangle, double width, double height), _) => ...,
};
```

The analyzer either misses the switch entirely or reports Circle as unhandled despite full coverage across both `condition` values.

## Prerequisites

`EnforceExhaustivenessAttribute` currently targets `AttributeTargets.Enum` only. Must expand to `AttributeTargets.Enum | AttributeTargets.Class | AttributeTargets.Interface` before this work begins, to support marking type hierarchies.

## Solution

A new standalone project (`Spire.PatternAnalysis`) implementing Maranget's decision-tree algorithm for pattern exhaustiveness checking. Operates on Roslyn's public `IPatternOperation` API and `IValueDomain` abstractions to determine whether a switch covers all possible inputs.

Replaces the existing `PatternAnalyzer` in `Spire.Analyzers` once validated.

## Scope

### Value domains supported

| Domain | Exhaustion condition |
|--------|---------------------|
| Bool | `true + false` |
| Enum | All named members covered |
| Numeric (int, double, etc.) | Full range covered via relational patterns (`>30 + <=30`) |
| Nullable (`T?`, ref type in nullable-oblivious) | `null + exhaustive coverage of T` |
| Structural (any type) | `{}`, `var`, `not null`, or exhaustive property pattern combinations |
| `[EnforceExhaustiveness]` hierarchy | All concrete derived/implementing types covered, each with its own domain |
| Discriminated union (Spire) | All variants covered via Kind enum (deconstruct) or kind property (property pattern) |
| Tuple | Cross-product of element domains |

### Pattern types handled

- Constant (`42`, `true`, `MyEnum.Foo`, `null`)
- Relational (`> 30`, `<= 50`)
- Or/And (`patA or patB`, `patA and patB`)
- Negated (`not null`, `not 42`)
- Type (`Type x`)
- Recursive/positional (`(pat1, pat2, ...)`)
- Property (`{ Prop: pattern }`)
- Discard (`_`) / var (`var x`)
- Wildcard (`{}`)

### Out of scope

- List patterns / slice patterns
- String value matching (strings are reference types: `null + not-null` only)
- `when` guard semantic analysis -- guarded arms are recognized and excluded from the pattern matrix (the arm's pattern does not contribute to exhaustiveness). This is conservative: the checker may report missing cases that a guard would logically cover, but never suppresses a real gap. Future work may promote guarded arms to "soft" coverage.
- `is` pattern expressions (`if (x is Pattern)`) -- single-arm, not applicable to exhaustiveness

## Architecture

### Project structure

```
src/Spire.PatternAnalysis/
  Spire.PatternAnalysis.csproj              # netstandard2.0, Microsoft.CodeAnalysis.CSharp

  ExhaustivenessChecker.cs                  # Entry: Compilation + switch operation -> result
  ExhaustivenessResult.cs                   # ImmutableArray<MissingCase>
  SlotIdentifier.cs                         # PropertySlot, TupleSlot, DeconstructSlot

  Domains/
    IValueDomain.cs                         # Core domain interface (see IValueDomain contract below)
    BoolDomain.cs
    EnumDomain.cs
    NumericDomain.cs
    NullableDomain.cs                       # {null, DomainOf(T)}
    StructuralDomain.cs                     # Base: {}, var, not null, or property combos
    TupleDomain.cs                          # : StructuralDomain -- positional elements
    PropertyPatternDomain.cs                # : StructuralDomain -- named properties
    EnforceExhaustiveDomain.cs              # Derived type set, each with own domain
    DiscriminatedUnion/
      DUTupleDomain.cs                      # : TupleDomain -- Kind enum + deconstruct
      DUPropertyPatternDomain.cs            # : PropertyPatternDomain -- kind property
    Numeric/
      Interval.cs                           # Single (lo, hi) with inclusive/exclusive bounds
      IntervalSet.cs                        # Union of intervals + set operations

  Algorithm/
    PatternMatrix.cs                        # Rows (arms) x Columns (slots), cells are constraints
    DecisionTreeBuilder.cs                  # Maranget: column selection, specialization, recursion

  Resolution/
    TypeHierarchyResolver.cs                # Assembly walk, visibility scoping, cached
```

### Domain hierarchy

```
IValueDomain
  BoolDomain                                # {true, false}
  EnumDomain                                # Named member set
  NumericDomain                             # Interval set with type-specific bounds
  NullableDomain                            # {null, DomainOf(T)}
  StructuralDomain (abstract)               # Cross-product of sub-domains
    TupleDomain                             # Positional: DeconstructionSubpatterns
      DUTupleDomain                         # Kind is element[0], variant field layouts
    PropertyPatternDomain                   # Named: PropertySubpatterns
      DUPropertyPatternDomain               # kind property, variant field layouts
  EnforceExhaustiveDomain                   # Set of concrete derived types
```

### IValueDomain contract

```csharp
interface IValueDomain
  bool IsEmpty { get; }                           // no values remain
  bool IsUniverse { get; }                        // all values of the type are present
  IValueDomain Subtract(IValueDomain other)       // remove values covered by other
  IValueDomain Intersect(IValueDomain other)      // values in both
  IValueDomain Complement()                       // all values NOT in this domain (relative to Universe)
  ITypeSymbol Type { get; }                       // the C# type this domain represents
  ImmutableArray<IValueDomain> Split()            // decompose into disjoint partitions for specialization
```

`Split()` is the key method for the Maranget algorithm. It returns the set of distinct value partitions that patterns in the current column distinguish between. Examples:
- `BoolDomain` -> `[{true}, {false}]`
- `EnumDomain({A, B, C})` -> `[{A}, {B}, {C}]`
- `NumericDomain` with patterns `>30` and `<=50` -> `[(-inf, 30], (30, 50], (50, +inf)]`
- `EnforceExhaustiveDomain({Circle, Rectangle})` -> `[{Circle}, {Rectangle}]`

All operations return the same concrete type (e.g., `BoolDomain.Subtract` returns `BoolDomain`). Cross-type operations are not needed -- each column has exactly one domain type.

### Type-to-domain resolution (`DomainOf(T)`)

Maps a C# type to its appropriate `IValueDomain`:

| Type | Domain |
|------|--------|
| `bool` | `BoolDomain` |
| Enum type | `EnumDomain` |
| Integer types (byte..long, sbyte..ulong) | `NumericDomain` with type-specific bounds |
| Floating-point (float, double) | `NumericDomain` -- NaN excluded from universe (see Numeric edge cases) |
| `decimal` | `NumericDomain` |
| `T?` (value type) | `NullableDomain(DomainOf(T))` |
| Reference type with `NullableAnnotation.Annotated` | `NullableDomain(DomainOf(T))` |
| Reference type with `NullableAnnotation.None` (oblivious) | `NullableDomain(DomainOf(T))` |
| Reference type with `NullableAnnotation.NotAnnotated` | `DomainOf(T)` directly |
| `[EnforceExhaustiveness]` type | `EnforceExhaustiveDomain` |
| Spire discriminated union | `DUTupleDomain` or `DUPropertyPatternDomain` |
| Any other type | `StructuralDomain` |

When a type doesn't fit any specific domain (the `StructuralDomain` fallback), exhaustiveness requires a total pattern (`{}`, `var`, `_`, `not null`) or exhaustive property pattern combinations across all properties mentioned in the switch arms.

### Numeric edge cases

- Integer types: universe is `[MinValue, MaxValue]` for the specific type
- `float`/`double`: universe is `[-infinity, +infinity]`. `NaN` is excluded from the universe because `NaN` does not satisfy any relational pattern (`NaN > x` and `NaN <= x` are both false). A switch over floats that uses only relational patterns cannot be made exhaustive without a wildcard/discard arm. This matches C# compiler behavior.
- `decimal`: universe is `[decimal.MinValue, decimal.MaxValue]`

### Nullable resolution

- `T?` (value type) -> NullableDomain wrapping DomainOf(T)
- `T?` (reference type, `NullableAnnotation.Annotated`) -> NullableDomain wrapping DomainOf(T)
- Reference type in nullable-oblivious context (`NullableAnnotation.None`) -> NullableDomain wrapping DomainOf(T)
- Reference type, `NullableAnnotation.NotAnnotated` -> DomainOf(T) directly (no null)
- Value type without `?` -> DomainOf(T) directly (no null)

### Entry point

```csharp
static class ExhaustivenessChecker
  ExhaustivenessResult Check(Compilation compilation, ISwitchExpressionOperation switchExpr)
  ExhaustivenessResult Check(Compilation compilation, ISwitchOperation switchStmt)
```

Both overloads share the same internal pipeline: extract arms/cases, build pattern matrix, run decision tree, collect missing cases.

### Result types

```csharp
ExhaustivenessResult
  ImmutableArray<MissingCase> MissingCases    // empty = exhaustive

MissingCase
  ImmutableArray<SlotConstraint> Constraints

SlotConstraint
  SlotIdentifier Slot
  IValueDomain Remaining                      // uncovered portion of slot's domain
```

```csharp
SlotIdentifier (abstract)
  PropertySlot      -> IPropertySymbol
  TupleSlot         -> int index, ITypeSymbol elementType
  DeconstructSlot   -> int index, IMethodSymbol deconstructor
```

### Algorithm

Maranget's pattern matrix specialization. Operates directly on `IPatternOperation` subtypes from Roslyn's public API. No custom intermediate pattern AST.

**Matrix cell representation:**

Each cell is a constraint on one slot, extracted from the arm's `IPatternOperation` tree during matrix construction:

| Cell type | Source pattern | Meaning |
|-----------|---------------|---------|
| Wildcard | `_`, `var x`, `{}` | Matches any value in the slot's domain |
| Constant | `42`, `true`, `null` | Matches exactly one value |
| Relational | `> 30`, `<= 50` | Matches a sub-range of the slot's numeric domain |
| Negated | `not null`, `not 42` | Matches complement of inner constraint |
| Or | `1 or 2 or 3` | Matches union of sub-constraints |
| And | `> 0 and < 100` | Matches intersection of sub-constraints |
| Type | `string s` | Matches non-null values of the specified type |
| Nested | `(pat1, pat2)`, `{ P: pat }` | Expands into new columns for sub-slots |

Nested constraints are flattened during matrix construction -- the sub-patterns become cells in new columns, and the original column is either removed (tuple) or constrained (property pattern).

**Pattern matrix construction:**

1. Each switch arm becomes a row. Arms with `when` guards are excluded.
2. Walk the arm's `IPatternOperation` tree, distribute constraints into columns (one per slot).
3. When a recursive/property pattern is encountered, create new columns for each sub-slot.
4. `DomainOf(T)` is called for each new column to determine its value domain.

**Core loop (`CheckExhaustive`):**

1. Zero columns -> exhaustive (all constraints satisfied)
2. Zero rows -> NOT exhaustive. Construct `MissingCase` by collecting each column's remaining `IValueDomain` (after all prior specializations). Each column contributes a `SlotConstraint` to the `MissingCase`.
3. Pick a column (heuristic: most constrained or leftmost non-wildcard)
4. Call `column.Domain.Split()` to get partitions
5. For each partition:
   a. Specialize: keep rows whose cell in this column intersects the partition, remove the column
   b. Wildcard cells are kept in all partitions
   c. Recurse on sub-matrix
6. Exhaustive only if ALL partitions are exhaustive

**Specialization** filters rows and refines remaining columns. Example -- specializing for `Kind.Circle`:
- Rows with `Kind.Circle` -> keep, strip Kind column
- Rows with `_` or `var` -> keep (wildcard)
- Rows with `Kind.Rectangle` -> drop

### Type hierarchy resolution

Resolves concrete types implementing/extending an `[EnforceExhaustiveness]`-marked type.

**Visibility-based search scope** (evaluated top-to-bottom, first match wins):

| Condition | Search scope |
|-----------|-------------|
| T is sealed | Empty (no derived types possible) |
| T is nested + private/protected | Parent type's nested types |
| T is nested + parent is internal/protected/private | Declaring assembly |
| T is internal/protected/private | Declaring assembly |
| Most exposed constructor is private | T's nested types |
| Most exposed constructor is internal | Declaring assembly |
| Public type + public ctor (or interface) | Declaring assembly + all assemblies that depend on it |

**Assembly dependency detection:** For each assembly in `Compilation.References`, check if its `ReferencedAssemblySymbols` includes the declaring assembly.

**Caching:** `ConcurrentDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>` per compilation, keyed by `SymbolEqualityComparer.Default`.

**Edge cases:**
- Generic base types: compare via `OriginalDefinition`
- Nested types: included in `GetTypeMembers()` recursion
- Abstract intermediate types: skipped (not concrete)
- Partial classes: Roslyn unifies them, no special handling

### Attribute resolution

`Spire.PatternAnalysis` resolves Spire attributes via `Compilation.GetTypeByMetadataName()`. No dependency on `Spire.Core`. Attribute metadata names are string constants within the library.

## Test strategy

Two layers: unit tests for components in isolation, file-based integration tests for end-to-end validation.

### Unit tests

Standard xUnit tests covering domain operations (construction, subtraction, intersection, complement, isEmpty), interval arithmetic, pattern matrix construction/specialization, decision tree builder logic, and type hierarchy resolution.

### Integration tests

File-based discovery mirroring `AnalyzerTestBase` conventions. Adding a test = adding a `.cs` file.

**Case file format:**

```csharp
//@ exhaustive
// Description of what this tests

// ... C# switch expression/statement
```

```csharp
//@ not_exhaustive: Circle(false), Rectangle
// Description

// ... C# switch expression/statement
```

**Structure:**

```
tests/Spire.PatternAnalysis.Tests/
  ExhaustivenessTestBase.cs                 # File discovery, compile, extract switch, assert
  Domains/                                  # Unit tests
  Algorithm/                                # Unit tests
  Resolution/                               # Unit tests
  Integration/                              # File-based integration tests
    Bool/
      _shared.cs
      cases/
    Enum/
      _shared.cs
      cases/
    NumericRange/
      _shared.cs
      cases/
    Null/
      _shared.cs
      cases/
    Tuple/
      _shared.cs
      cases/
    PropertyPattern/
      _shared.cs
      cases/
    NestedRecursive/
      _shared.cs
      cases/
    Union/
      _shared.cs
      cases/
    TypeHierarchy/
      _shared.cs
      cases/
    MixedDomain/
      _shared.cs
      cases/
```

`ExhaustivenessTestBase` compiles `_shared.cs` + case file together, extracts the switch operation from the compilation, runs `ExhaustivenessChecker`, and asserts the result matches the header directive.

## Integration with Spire.Analyzers

After the library is validated with its own test suite:

1. `Spire.Analyzers` adds a project reference to `Spire.PatternAnalysis`
2. `ExhaustivenessAnalyzer` (SPIRE009) delegates to `ExhaustivenessChecker` instead of `PatternAnalyzer`
3. `CS8509Suppressor` uses `ExhaustivenessChecker` for suppression decisions
4. `FieldAccessSafetyAnalyzer` may leverage `ExhaustivenessResult` for guard detection
5. Existing `PatternAnalyzer` is removed after migration

## Dependencies

- `Microsoft.CodeAnalysis.CSharp` (public API: `IPatternOperation`, `ITypeSymbol`, `Compilation`)
- `PolySharp` (modern C# on netstandard2.0)
- No dependency on `Spire.Core` or `Spire.Analyzers`
- Minimum Roslyn version: 4.0 (`IRelationalPatternOperation` introduced in 3.7, project uses 5.0.0)

## Estimated size

| Component | Lines |
|-----------|-------|
| Domains (all) | ~800-1200 |
| Interval arithmetic | ~500-700 |
| Algorithm (matrix + builder) | ~600-800 |
| Type hierarchy resolver | ~200-300 |
| ExhaustivenessChecker + result types | ~150-200 |
| Total library | ~2300-3400 |
| Unit tests | ~1000-1500 |
| Integration test infrastructure | ~200-300 |
| Integration test cases | ~50-80 files |
