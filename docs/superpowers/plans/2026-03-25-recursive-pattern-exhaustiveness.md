# Recursive Pattern Exhaustiveness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `Houtamelo.Spire.PatternAnalysis`, a standalone library that determines whether C# switch expressions/statements exhaustively cover all possible input patterns, including nested/recursive patterns.

**Architecture:** Maranget's decision-tree algorithm operating on Roslyn's public `IPatternOperation` API. Value domains model the set of possible values per type. Pattern matrix rows = switch arms, columns = slots. Column specialization + recursion determines exhaustiveness.

**Tech Stack:** C# / netstandard2.0 / Microsoft.CodeAnalysis.CSharp 5.0.0 / xUnit / PolySharp

**Spec:** `docs/superpowers/specs/2026-03-25-recursive-pattern-exhaustiveness-design.md`

---

## File Map

### Library (`src/Houtamelo.Spire.PatternAnalysis/`)

| File | Responsibility |
|------|---------------|
| `Houtamelo.Spire.PatternAnalysis.csproj` | Project file (netstandard2.0, Roslyn 5.0.0, PolySharp) |
| `ExhaustivenessChecker.cs` | Public entry point: `Check(Compilation, ISwitchExpressionOperation)` and `Check(Compilation, ISwitchOperation)` |
| `ExhaustivenessResult.cs` | `ExhaustivenessResult`, `MissingCase`, `SlotConstraint` |
| `SlotIdentifier.cs` | `SlotIdentifier` hierarchy: `PropertySlot`, `TupleSlot`, `DeconstructSlot` |
| `DomainResolver.cs` | `DomainOf(ITypeSymbol, Compilation)` — maps types to domains |
| `Domains/IValueDomain.cs` | Interface: `IsEmpty`, `IsUniverse`, `Subtract`, `Intersect`, `Complement`, `Split`, `Type` |
| `Domains/BoolDomain.cs` | `{true, false}` finite set |
| `Domains/EnumDomain.cs` | Named enum member set |
| `Domains/NumericDomain.cs` | Wraps `IntervalSet` with type-specific bounds |
| `Domains/NullableDomain.cs` | `{null} ∪ DomainOf(T)` |
| `Domains/StructuralDomain.cs` | Abstract base for cross-product domains |
| `Domains/TupleDomain.cs` | Positional element cross-product |
| `Domains/PropertyPatternDomain.cs` | Named property cross-product |
| `Domains/EnforceExhaustiveDomain.cs` | Concrete derived type set |
| `Domains/DiscriminatedUnion/DUTupleDomain.cs` | Kind enum + deconstruct patterns |
| `Domains/DiscriminatedUnion/DUPropertyPatternDomain.cs` | Kind property + property patterns |
| `Domains/Numeric/Interval.cs` | Single `(lo, hi)` range with inclusive/exclusive bounds |
| `Domains/Numeric/IntervalSet.cs` | Union of intervals + set operations |
| `Algorithm/PatternMatrix.cs` | Matrix data structure: rows, columns, cells (constraints) |
| `Algorithm/DecisionTreeBuilder.cs` | Maranget core loop: column selection, specialization, recursion |
| `Resolution/TypeHierarchyResolver.cs` | Assembly walk with visibility scoping, cached |

### Tests (`tests/Houtamelo.Spire.PatternAnalysis.Tests/`)

| File | Responsibility |
|------|---------------|
| `Houtamelo.Spire.PatternAnalysis.Tests.csproj` | Test project (net10.0, xUnit, references Houtamelo.Spire.PatternAnalysis + Houtamelo.Spire) |
| `ExhaustivenessTestBase.cs` | File-based test discovery for integration tests |
| `Domains/BoolDomainTests.cs` | BoolDomain unit tests |
| `Domains/EnumDomainTests.cs` | EnumDomain unit tests |
| `Domains/NullableDomainTests.cs` | NullableDomain unit tests |
| `Domains/NumericDomainTests.cs` | NumericDomain unit tests |
| `Domains/StructuralDomainTests.cs` | TupleDomain + PropertyPatternDomain unit tests |
| `Domains/EnforceExhaustiveDomainTests.cs` | EnforceExhaustiveDomain unit tests |
| `Domains/Numeric/IntervalTests.cs` | Interval unit tests |
| `Domains/Numeric/IntervalSetTests.cs` | IntervalSet unit tests |
| `Algorithm/PatternMatrixTests.cs` | Matrix construction + specialization unit tests |
| `Algorithm/DecisionTreeBuilderTests.cs` | Algorithm unit tests |
| `Resolution/TypeHierarchyResolverTests.cs` | Resolver unit tests |
| `Integration/*/` | File-based integration tests (10 categories) |

**Known limitation:** All numeric domains use `double` internally for interval arithmetic. This loses precision for `long` values near `long.MaxValue` and for `decimal` beyond 53 bits of significand. In practice, nobody writes switch arms distinguishing `long.MaxValue` from `long.MaxValue - 1`, so this is acceptable. Document in code.

**Note on DiscriminatedUnionAttribute:** This attribute is source-generated (not in `Houtamelo.Spire`). Test compilations for DU domains must include the attribute declaration as a raw C# string in their `_shared.cs` files. `DomainResolver` resolves it via `GetTypeByMetadataName("Houtamelo.Spire.DiscriminatedUnionAttribute")` which works in production (generator emits it) and in tests (included as source text).

**Note on StructuralDomain set operations:** The Maranget algorithm uses `Split()` and `Intersect()` during column specialization, but does NOT call `Subtract()` or `Complement()` on structural/composite domains. Cross-product subtraction is inherently complex and unnecessary — the matrix specialization handles decomposition implicitly. `StructuralDomain` implements `Subtract`/`Complement` as `throw new NotSupportedException()`.

**Visibility:** All library types are `internal`. Add `[InternalsVisibleTo("Houtamelo.Spire.Analyzers")]` and `[InternalsVisibleTo("Houtamelo.Spire.PatternAnalysis.Tests")]` to the library project.

---

## Task 0: Prerequisite — Expand EnforceExhaustivenessAttribute

**Files:**
- Modify: `src/Houtamelo.Spire/EnforceExhaustivenessAttribute.cs`

Must be done first — needed by EnforceExhaustiveDomain tests and TypeHierarchy integration tests.

Note: `EnforceExhaustivenessAttribute` inherits from `EnforceInitializationAttribute`. Expanding its `AttributeTargets` to include `Class | Interface` is safe — child attributes can have broader targets than parents in C#. Existing analyzers (SPIRE001-008) check for `EnforceInitializationAttribute` specifically, and their `IsDefaultValueInvalid` checks only operate on structs/enums, so interfaces/classes with the attribute are silently ignored by those rules. No breakage.

- [ ] **Step 1: Update attribute targets**

Change `[AttributeUsage(AttributeTargets.Enum)]` to:
```csharp
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Class | AttributeTargets.Interface)]
```

- [ ] **Step 2: Verify existing tests still pass**

Run: `dotnet test`
Expected: ALL PASS (expanding attribute targets is backwards-compatible).

- [ ] **Step 3: Commit**

```bash
git add src/Houtamelo.Spire/EnforceExhaustivenessAttribute.cs
git commit -m "feat(core): expand EnforceExhaustivenessAttribute to support class and interface targets"
```

---

## Task 1: Project Scaffolding

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Houtamelo.Spire.PatternAnalysis.csproj`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Houtamelo.Spire.PatternAnalysis.Tests.csproj`
- Modify: `Spire.Analyzers.slnx`

- [ ] **Step 1: Create library project**

```xml
<!-- src/Houtamelo.Spire.PatternAnalysis/Houtamelo.Spire.PatternAnalysis.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" PrivateAssets="all" />
    <PackageReference Include="PolySharp" Version="1.15.0" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>Houtamelo.Spire.Analyzers</_Parameter1>
    </AssemblyAttribute>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>Houtamelo.Spire.PatternAnalysis.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create test project**

```xml
<!-- tests/Houtamelo.Spire.PatternAnalysis.Tests/Houtamelo.Spire.PatternAnalysis.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);NU1701;AD0001;xUnit1003</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <Compile Remove="**/cases/**" />
    <None Include="**/cases/**" Exclude="bin/**;obj/**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Houtamelo.Spire.PatternAnalysis\Houtamelo.Spire.PatternAnalysis.csproj" />
    <ProjectReference Include="..\..\src\Houtamelo.Spire\Houtamelo.Spire.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add projects to solution**

Add to `Spire.Analyzers.slnx`:
```xml
<Folder Name="/src/">
  <!-- existing entries -->
  <Project Path="src/Houtamelo.Spire.PatternAnalysis/Houtamelo.Spire.PatternAnalysis.csproj" />
</Folder>
<Folder Name="/tests/">
  <!-- existing entries -->
  <Project Path="tests/Houtamelo.Spire.PatternAnalysis.Tests/Houtamelo.Spire.PatternAnalysis.Tests.csproj" />
</Folder>
```

- [ ] **Step 4: Verify build**

Run: `dotnet build`
Expected: Success with no errors.

- [ ] **Step 5: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/ tests/Houtamelo.Spire.PatternAnalysis.Tests/ Spire.Analyzers.slnx
git commit -m "scaffold: add Houtamelo.Spire.PatternAnalysis project and test project"
```

---

## Task 2: Core Types

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/IValueDomain.cs`
- Create: `src/Houtamelo.Spire.PatternAnalysis/SlotIdentifier.cs`
- Create: `src/Houtamelo.Spire.PatternAnalysis/ExhaustivenessResult.cs`

- [ ] **Step 1: Create IValueDomain interface**

```csharp
// src/Houtamelo.Spire.PatternAnalysis/Domains/IValueDomain.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Interface for a set of possible values of a given type.
/// Each domain represents the "universe" of values that a pattern slot can hold,
/// and supports set operations needed by the Maranget algorithm.
internal interface IValueDomain
{
    /// True when no values remain in this domain.
    bool IsEmpty { get; }

    /// True when this domain contains every possible value of its type.
    bool IsUniverse { get; }

    /// The C# type this domain represents.
    ITypeSymbol Type { get; }

    /// Remove values covered by other from this domain.
    IValueDomain Subtract(IValueDomain other);

    /// Values present in both this and other.
    IValueDomain Intersect(IValueDomain other);

    /// All values NOT in this domain (relative to universe of this type).
    IValueDomain Complement();

    /// Decompose into disjoint partitions for Maranget column specialization.
    /// Each partition represents a distinct "case" the algorithm must check.
    ImmutableArray<IValueDomain> Split();
}
```

- [ ] **Step 2: Create SlotIdentifier hierarchy**

```csharp
// src/Houtamelo.Spire.PatternAnalysis/SlotIdentifier.cs
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis;

/// Identifies a position within a pattern that a constraint applies to.
internal abstract class SlotIdentifier
{
    private SlotIdentifier() { }

    internal sealed class PropertySlot(IPropertySymbol property) : SlotIdentifier
    {
        public IPropertySymbol Property { get; } = property;
    }

    internal sealed class TupleSlot(int index, ITypeSymbol elementType) : SlotIdentifier
    {
        public int Index { get; } = index;
        public ITypeSymbol ElementType { get; } = elementType;
    }

    internal sealed class DeconstructSlot(int index, IMethodSymbol deconstructor) : SlotIdentifier
    {
        public int Index { get; } = index;
        public IMethodSymbol Deconstructor { get; } = deconstructor;
    }
}
```

- [ ] **Step 3: Create result types**

```csharp
// src/Houtamelo.Spire.PatternAnalysis/ExhaustivenessResult.cs
using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Domains;

namespace Houtamelo.Spire.PatternAnalysis;

/// Result of exhaustiveness analysis on a switch expression/statement.
internal readonly struct ExhaustivenessResult(ImmutableArray<MissingCase> missingCases)
{
    public ImmutableArray<MissingCase> MissingCases { get; } = missingCases;
    public bool IsExhaustive => MissingCases.IsEmpty;
}

/// A single combination of uncovered values across one or more slots.
internal readonly struct MissingCase(ImmutableArray<SlotConstraint> constraints)
{
    public ImmutableArray<SlotConstraint> Constraints { get; } = constraints;
}

/// An uncovered portion of a single slot's domain.
internal readonly struct SlotConstraint(SlotIdentifier slot, IValueDomain remaining)
{
    public SlotIdentifier Slot { get; } = slot;
    public IValueDomain Remaining { get; } = remaining;
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build`
Expected: Success.

- [ ] **Step 5: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/
git commit -m "feat(pattern-analysis): add core types — IValueDomain, SlotIdentifier, ExhaustivenessResult"
```

---

## Task 3: Interval + IntervalSet (TDD)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/Numeric/Interval.cs`
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/Numeric/IntervalSet.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/Numeric/IntervalTests.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/Numeric/IntervalSetTests.cs`

Interval arithmetic is the foundation for NumericDomain. Build and test this independently first.

- [ ] **Step 1: Write Interval tests**

Test cases for `Interval`:
- Construction with inclusive/exclusive bounds
- `Contains(double value)` — boundary cases, interior, exterior
- `Overlaps(Interval other)` — overlapping, adjacent, disjoint
- `IsEmpty` — empty intervals (lo > hi, or lo == hi with exclusive bounds)
- `Equals` / `GetHashCode`

All numeric values are represented as `double` internally. Integer bounds are represented as whole-number doubles with inclusive bounds. This avoids generic type proliferation — precision loss is not an issue because we only need to know "is the range fully covered?" not exact arithmetic.

- [ ] **Step 2: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~IntervalTests"`
Expected: FAIL (types don't exist yet).

- [ ] **Step 3: Implement Interval**

`Interval` is an immutable readonly struct with:
- `double Lo, double Hi` — bounds
- `bool LoInclusive, bool HiInclusive` — bound types
- `bool IsEmpty` — computed
- `bool Contains(double value)`
- `bool Overlaps(Interval other)`
- `static Interval Empty`, `static Interval Universe(double min, double max)`
- Override `Equals`, `GetHashCode`

- [ ] **Step 4: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~IntervalTests"`
Expected: PASS.

- [ ] **Step 5: Write IntervalSet tests**

Test cases for `IntervalSet`:
- `Union(IntervalSet other)` — disjoint, overlapping, adjacent intervals merge
- `Subtract(IntervalSet other)` — splits intervals, creates gaps
- `Intersect(IntervalSet other)` — common ranges
- `Complement(double universeMin, double universeMax)` — inverts within bounds
- `IsEmpty` — empty after full subtraction
- `CoversAll(double min, double max)` — full universe coverage
- Key scenario: `[min, 30] ∪ (30, max]` = full universe for integers
- Key scenario: `(-inf, 30] ∪ (30, +inf)` = full universe (minus NaN) for doubles
- Adjacent intervals with matching inclusive/exclusive bounds merge correctly

- [ ] **Step 6: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~IntervalSetTests"`
Expected: FAIL.

- [ ] **Step 7: Implement IntervalSet**

`IntervalSet` is an immutable type wrapping `ImmutableArray<Interval>` (sorted, non-overlapping, normalized). Methods:
- `static IntervalSet Empty`, `static IntervalSet Universe(double min, double max)`
- `IntervalSet Union(IntervalSet other)`
- `IntervalSet Subtract(IntervalSet other)`
- `IntervalSet Intersect(IntervalSet other)`
- `IntervalSet Complement(double universeMin, double universeMax)`
- `bool IsEmpty`, `bool CoversAll(double min, double max)`
- `ImmutableArray<IntervalSet> SplitByBoundaries(ImmutableArray<double> boundaries)` — splits into partitions at given boundary points (used by `NumericDomain.Split()`)

Normalization on construction: sort intervals by lo, merge overlapping/adjacent intervals.

- [ ] **Step 8: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~IntervalSetTests"`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/Domains/Numeric/ tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/Numeric/
git commit -m "feat(pattern-analysis): add Interval and IntervalSet with tests"
```

---

## Task 4: BoolDomain + EnumDomain (TDD)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/BoolDomain.cs`
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/EnumDomain.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/BoolDomainTests.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/EnumDomainTests.cs`

- [ ] **Step 1: Write BoolDomain tests**

Test cases:
- Universe contains both values, `IsUniverse` is true
- `Subtract({true})` leaves `{false}`, and vice versa
- `Subtract({true, false})` leaves empty
- `Complement()` of `{true}` is `{false}`
- `Split()` returns `[{true}, {false}]`
- `IsEmpty` after subtracting all values
- `Intersect({true}, {false})` is empty

- [ ] **Step 2: Write EnumDomain tests**

Test cases (use a test enum with members A, B, C):
- Universe contains all members
- `Subtract({A})` leaves `{B, C}`
- `Subtract({A, B, C})` leaves empty
- `Complement()` of `{A}` is `{B, C}`
- `Split()` returns one singleton per member: `[{A}, {B}, {C}]`
- `IsEmpty` when all members subtracted
- `Intersect({A, B}, {B, C})` is `{B}`

Note: EnumDomain tests need Roslyn `ITypeSymbol` instances. Create a minimal Roslyn compilation with a test enum to get the `INamedTypeSymbol`. Helper method in tests:

```csharp
private static INamedTypeSymbol GetEnumSymbol(string enumDeclaration)
{
    var tree = CSharpSyntaxTree.ParseText(enumDeclaration);
    var compilation = CSharpCompilation.Create("Test", [tree],
        [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    var model = compilation.GetSemanticModel(tree);
    var enumDecl = tree.GetRoot().DescendantNodes()
        .OfType<EnumDeclarationSyntax>().First();
    return model.GetDeclaredSymbol(enumDecl)!;
}
```

- [ ] **Step 3: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~BoolDomain or FullyQualifiedName~EnumDomain"`
Expected: FAIL.

- [ ] **Step 4: Implement BoolDomain**

Internally tracks a `bool hasTrue, bool hasFalse`. Implements `IValueDomain`. `Split()` returns one `BoolDomain` per present value.

- [ ] **Step 5: Implement EnumDomain**

Internally tracks `ImmutableHashSet<IFieldSymbol>` (named enum members present). Constructed from `INamedTypeSymbol` enum type. `Split()` returns one singleton `EnumDomain` per member.

- [ ] **Step 6: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~BoolDomain or FullyQualifiedName~EnumDomain"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/Domains/BoolDomain.cs src/Houtamelo.Spire.PatternAnalysis/Domains/EnumDomain.cs tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/
git commit -m "feat(pattern-analysis): add BoolDomain and EnumDomain with tests"
```

---

## Task 5: NumericDomain (TDD)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/NumericDomain.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/NumericDomainTests.cs`

- [ ] **Step 1: Write NumericDomain tests**

Test cases:
- Universe for `int` is `[int.MinValue, int.MaxValue]` — `IsUniverse` true
- `Subtract(> 30)` from int universe leaves `[int.MinValue, 30]`
- `Subtract(<= 30)` from int universe leaves `(30, int.MaxValue]`
- `(> 30)` union `(<= 30)` = universe for int — `IsUniverse` true after reconstitution
- `Complement()` of `(> 50)` is `[int.MinValue, 50]`
- `Split()` with boundary at 30: returns `[(-inf, 30], (30, +inf)]`
- Double universe excludes NaN: `[-inf, +inf]`
- `IsEmpty` after subtracting full range

NumericDomain wraps `IntervalSet` and adds type-specific universe bounds. Constructor takes `ITypeSymbol` to determine bounds from `SpecialType`.

- [ ] **Step 2: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~NumericDomainTests"`
Expected: FAIL.

- [ ] **Step 3: Implement NumericDomain**

Fields: `IntervalSet _intervals`, `ITypeSymbol _type`, `double _universeMin`, `double _universeMax`.

Factory: `static NumericDomain Universe(ITypeSymbol type)` — resolves `SpecialType` to min/max bounds.

`Split()` delegates to `IntervalSet.SplitByBoundaries()` — boundaries are the interval endpoints present in the current set.

`Subtract`, `Intersect`, `Complement` delegate to `IntervalSet` operations.

- [ ] **Step 4: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~NumericDomainTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/Domains/NumericDomain.cs tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/NumericDomainTests.cs
git commit -m "feat(pattern-analysis): add NumericDomain with tests"
```

---

## Task 6: NullableDomain (TDD)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/NullableDomain.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/NullableDomainTests.cs`

- [ ] **Step 1: Write NullableDomain tests**

Test cases:
- Universe of `bool?` = `{null, true, false}` — `IsUniverse` true
- Subtract null from `bool?` leaves `{true, false}` (inner BoolDomain)
- Subtract `{true}` from `bool?` leaves `{null, false}`
- Subtract `{null, true, false}` leaves empty
- `Split()` of `bool?` returns `[{null}, {true}, {false}]` — null partition + inner domain's splits
- `Complement()` of `{null}` is `{true, false}` (inner universe)
- `Intersect({null, true}, {null, false})` is `{null}`
- Works with inner EnumDomain: `MyEnum?` needs null + all members

NullableDomain wraps an `IValueDomain` inner domain and adds a `bool _hasNull` flag.

- [ ] **Step 2: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~NullableDomainTests"`
Expected: FAIL.

- [ ] **Step 3: Implement NullableDomain**

Fields: `IValueDomain _inner`, `bool _hasNull`, `ITypeSymbol _type`.

`Split()`: returns `[NullableDomain(null-only)]` + inner domain's `Split()` results (each wrapped to exclude null).

`Subtract(other)`:
- If other is NullableDomain: subtract null flag, subtract inner domains
- If other represents null: clear null flag
- If other represents non-null values: subtract from inner

- [ ] **Step 4: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~NullableDomainTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/Domains/NullableDomain.cs tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/NullableDomainTests.cs
git commit -m "feat(pattern-analysis): add NullableDomain with tests"
```

---

## Task 7: StructuralDomain + TupleDomain + PropertyPatternDomain (TDD)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/StructuralDomain.cs`
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/TupleDomain.cs`
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/PropertyPatternDomain.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/StructuralDomainTests.cs`

StructuralDomain is the abstract base. TupleDomain and PropertyPatternDomain inherit it and add positional vs named access semantics.

- [ ] **Step 1: Write tests for TupleDomain**

Test cases:
- `(bool, bool)` universe: 4 cells (true/true, true/false, false/true, false/false)
- `Split()` on `(bool, bool)` → splits first non-exhausted element
- `Subtract({true}, {true})` from `(bool, bool)` leaves 3 combinations
- `Subtract({true}, _) + Subtract({false}, _)` → empty (exhaustive)
- `IsEmpty` after full subtraction
- `IsUniverse` for fresh construction

- [ ] **Step 2: Write tests for PropertyPatternDomain**

Test cases:
- Struct with `bool Prop` — universe requires `Prop` fully covered
- `Subtract({Prop: true})` leaves `{Prop: false}`
- `Subtract({Prop: true}) + Subtract({Prop: false})` → empty
- Multi-property: struct with `bool A, bool B` — cross-product of 4

- [ ] **Step 3: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~StructuralDomainTests"`
Expected: FAIL.

- [ ] **Step 4: Implement StructuralDomain (abstract)**

Abstract base for cross-product domains:
- `ImmutableArray<(SlotIdentifier slot, IValueDomain domain)> Slots` — one per sub-dimension
- `bool HasWildcard` — true if a total pattern (`{}`, `var`, `_`) covers this structural domain
- Shared `Subtract`, `Intersect`, `Complement` logic via slot-wise operations
- Shared `IsEmpty` = any slot is empty, `IsUniverse` = all slots are universe + has wildcard

- [ ] **Step 5: Implement TupleDomain**

Extends `StructuralDomain`. Slots are positional (`TupleSlot`). Constructed from tuple element types.

- [ ] **Step 6: Implement PropertyPatternDomain**

Extends `StructuralDomain`. Slots are named (`PropertySlot`). Constructed from property symbols and their domains. Only tracks properties mentioned in switch arms — unmentioned properties are implicitly wildcarded.

- [ ] **Step 7: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~StructuralDomainTests"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/Domains/StructuralDomain.cs src/Houtamelo.Spire.PatternAnalysis/Domains/TupleDomain.cs src/Houtamelo.Spire.PatternAnalysis/Domains/PropertyPatternDomain.cs tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/StructuralDomainTests.cs
git commit -m "feat(pattern-analysis): add StructuralDomain, TupleDomain, PropertyPatternDomain with tests"
```

---

## Task 8: TypeHierarchyResolver (TDD)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Resolution/TypeHierarchyResolver.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Resolution/TypeHierarchyResolverTests.cs`

- [ ] **Step 1: Write TypeHierarchyResolver tests**

Tests require Roslyn compilations with type hierarchies. Create helper that compiles C# source and returns `Compilation`.

Test cases:
- Sealed class → empty result
- Abstract class with 2 concrete subclasses in same assembly → returns both
- Interface with 3 implementors → returns all 3
- Private nested class → only searches parent type's nested types
- Internal class → only searches declaring assembly
- Class with only private constructors → only searches nested types
- Class with internal constructor → only searches declaring assembly
- Public class with public constructor → searches all assemblies
- Abstract intermediate types → skipped (not concrete)
- Generic base: `class Foo<T>`, `class Bar : Foo<int>` → found via `OriginalDefinition`

- [ ] **Step 2: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~TypeHierarchyResolverTests"`
Expected: FAIL.

- [ ] **Step 3: Implement TypeHierarchyResolver**

```csharp
internal sealed class TypeHierarchyResolver
{
    private readonly ConcurrentDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> _cache
        = new(SymbolEqualityComparer.Default);

    public ImmutableArray<INamedTypeSymbol> Resolve(INamedTypeSymbol baseType, Compilation compilation)
    {
        return _cache.GetOrAdd(baseType, bt => ResolveCore(bt, compilation));
    }
}
```

`ResolveCore` implements the visibility-based search scope from the spec:
1. Sealed → empty
2. Nested + private/protected → search parent's nested types
3. Nested + parent is internal/protected/private → search declaring assembly
4. Internal/protected/private → search declaring assembly
5. Most exposed ctor is private → search T's nested types
6. Most exposed ctor is internal → search declaring assembly
7. Otherwise → search declaring assembly + dependent assemblies

Walk `INamespaceSymbol` tree recursively. For each `INamedTypeSymbol`, check `BaseType` chain (classes) or `AllInterfaces` (interfaces). Filter to concrete types only.

- [ ] **Step 4: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~TypeHierarchyResolverTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/Resolution/ tests/Houtamelo.Spire.PatternAnalysis.Tests/Resolution/
git commit -m "feat(pattern-analysis): add TypeHierarchyResolver with visibility scoping and tests"
```

---

## Task 9: EnforceExhaustiveDomain (TDD)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/EnforceExhaustiveDomain.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/EnforceExhaustiveDomainTests.cs`

- [ ] **Step 1: Write EnforceExhaustiveDomain tests**

Test cases (using compilations with sealed class hierarchies):
- 2 concrete subtypes, both covered → `IsEmpty` after subtraction
- 2 concrete subtypes, one missing → remaining contains that subtype
- `Split()` returns one partition per concrete subtype
- `Subtract` by type pattern → removes that subtype
- `Complement()` of `{Circle}` when all types are `{Circle, Rectangle}` → `{Rectangle}`

- [ ] **Step 2: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~EnforceExhaustiveDomainTests"`
Expected: FAIL.

- [ ] **Step 3: Implement EnforceExhaustiveDomain**

Internally tracks `ImmutableHashSet<INamedTypeSymbol>` of remaining concrete types. Each type maps to its own sub-domain (for property-level exhaustiveness within each type branch).

Uses `TypeHierarchyResolver` to discover concrete types from the base type + compilation.

`Split()` returns one `EnforceExhaustiveDomain` per concrete type (singleton set). Each partition's sub-domain is the type's `StructuralDomain`.

- [ ] **Step 4: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~EnforceExhaustiveDomainTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/Domains/EnforceExhaustiveDomain.cs tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/EnforceExhaustiveDomainTests.cs
git commit -m "feat(pattern-analysis): add EnforceExhaustiveDomain with tests"
```

---

## Task 10: DU Domains (TDD)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/DiscriminatedUnion/DUTupleDomain.cs`
- Create: `src/Houtamelo.Spire.PatternAnalysis/Domains/DiscriminatedUnion/DUPropertyPatternDomain.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/DiscriminatedUnion/DUDomainTests.cs`

These domains extend TupleDomain and PropertyPatternDomain with Spire-specific union semantics.

- [ ] **Step 1: Write DUTupleDomain tests**

Test cases (using compilations with Spire-generated union types):
- Kind enum as element[0], variant fields as remaining elements
- All variants covered via Kind constants → `IsEmpty` after subtraction
- Missing one variant → remaining
- `Split()` returns partitions per Kind member
- Per-variant field coverage: Circle covered in 2 arms with different radius ranges

Note: Tests need `Houtamelo.Spire` reference to define `[DiscriminatedUnion]` types. The test project already references Houtamelo.Spire.

- [ ] **Step 2: Write DUPropertyPatternDomain tests**

Test cases:
- Property pattern with `kind` property → variant identification
- All variants covered via `{ kind: Kind.Circle }` + `{ kind: Kind.Rectangle }` → exhaustive
- Per-variant field coverage via nested property patterns

- [ ] **Step 3: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~DUDomainTests"`
Expected: FAIL.

- [ ] **Step 4: Implement DUTupleDomain**

Extends `TupleDomain`. Knows:
- Kind enum type (`INamedTypeSymbol`)
- Variant names and field layouts (from Kind enum members)
- Element[0] is always the Kind enum — uses `EnumDomain` for that slot
- Remaining elements vary per variant — resolves via Deconstruct method signature

- [ ] **Step 5: Implement DUPropertyPatternDomain**

Extends `PropertyPatternDomain`. Knows:
- Kind property (`IPropertySymbol`)
- Variant names and field maps
- Kind property uses `EnumDomain`
- Other properties are variant-specific fields

- [ ] **Step 6: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~DUDomainTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/Domains/DiscriminatedUnion/ tests/Houtamelo.Spire.PatternAnalysis.Tests/Domains/DiscriminatedUnion/
git commit -m "feat(pattern-analysis): add DUTupleDomain and DUPropertyPatternDomain with tests"
```

---

## Task 11: DomainResolver (TDD)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/DomainResolver.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/DomainResolverTests.cs`

- [ ] **Step 1: Write DomainResolver tests**

Test cases (compilations with various types):
- `bool` → `BoolDomain`
- `enum Color { R, G, B }` → `EnumDomain`
- `int` → `NumericDomain`
- `double` → `NumericDomain`
- `int?` → `NullableDomain(NumericDomain)`
- `bool?` → `NullableDomain(BoolDomain)`
- `string` (nullable-oblivious) → `NullableDomain(StructuralDomain)`
- `string?` (annotated) → `NullableDomain(StructuralDomain)`
- `string` (not annotated, `#nullable enable`) → `StructuralDomain`
- `[EnforceExhaustiveness]` class → `EnforceExhaustiveDomain`
- `[DiscriminatedUnion]` struct → `DUTupleDomain` or `DUPropertyPatternDomain`
- Unknown class → `StructuralDomain`

- [ ] **Step 2: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~DomainResolverTests"`
Expected: FAIL.

- [ ] **Step 3: Implement DomainResolver**

```csharp
internal sealed class DomainResolver
{
    private readonly Compilation _compilation;
    private readonly INamedTypeSymbol? _enforceExhaustivenessAttr;
    private readonly INamedTypeSymbol? _discriminatedUnionAttr;
    private readonly TypeHierarchyResolver _hierarchyResolver;

    public DomainResolver(Compilation compilation, TypeHierarchyResolver hierarchyResolver)
    {
        _compilation = compilation;
        _hierarchyResolver = hierarchyResolver;
        _enforceExhaustivenessAttr = compilation.GetTypeByMetadataName("Houtamelo.Spire.EnforceExhaustivenessAttribute");
        _discriminatedUnionAttr = compilation.GetTypeByMetadataName("Houtamelo.Spire.DiscriminatedUnionAttribute");
    }

    public IValueDomain Resolve(ITypeSymbol type) { ... }
}
```

Resolution order from spec:
1. Check nullable (value type `T?`, annotated ref, oblivious ref) → wrap in `NullableDomain`
2. Unwrap to inner type
3. `bool` → `BoolDomain`
4. Enum → `EnumDomain`
5. Numeric (`SpecialType` check) → `NumericDomain`
6. Has `[DiscriminatedUnion]` → `DUTupleDomain` or `DUPropertyPatternDomain`
7. Has `[EnforceExhaustiveness]` → `EnforceExhaustiveDomain`
8. Fallback → `StructuralDomain`

- [ ] **Step 4: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~DomainResolverTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/DomainResolver.cs tests/Houtamelo.Spire.PatternAnalysis.Tests/DomainResolverTests.cs
git commit -m "feat(pattern-analysis): add DomainResolver — maps ITypeSymbol to IValueDomain"
```

---

## Task 12: PatternMatrix (TDD)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Algorithm/PatternMatrix.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Algorithm/PatternMatrixTests.cs`

The matrix is the core data structure the algorithm operates on.

- [ ] **Step 1: Write PatternMatrix tests**

Unit tests operate on **pre-built** `Cell`/`Column` arrays — no Roslyn compilations needed. The `Build()` method (which takes `ISwitchExpressionOperation`) is tested via integration tests in Tasks 16-18.

Test cases:
- Construction from pre-built rows/columns: verify row/column counts
- Column indexing by `SlotIdentifier`
- `Specialize(columnIndex, IValueDomain partition)`:
  - Keeps rows whose cell intersects the partition
  - Removes the specialized column
  - Wildcard cells are kept in all partitions
- Mixed rows: some wildcard cells, some constraints — correct filtering

Matrix cells are one of: `Wildcard`, `Constraint(IValueDomain)`. The constraint represents what values this cell matches.

- [ ] **Step 2: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~PatternMatrixTests"`
Expected: FAIL.

- [ ] **Step 3: Implement PatternMatrix**

Key types:

```csharp
internal abstract class Cell
{
    internal sealed class Wildcard : Cell;
    internal sealed class Constraint(IValueDomain matchedValues) : Cell;
}

internal sealed class PatternMatrix
{
    private readonly ImmutableArray<ImmutableArray<Cell>> _rows;
    private readonly ImmutableArray<Column> _columns;

    public static PatternMatrix Build(
        ISwitchExpressionOperation switchExpr,
        DomainResolver resolver) { ... }

    public static PatternMatrix Build(
        ISwitchOperation switchStmt,
        DomainResolver resolver) { ... }

    public PatternMatrix Specialize(int columnIndex, IValueDomain partition) { ... }
}

internal readonly struct Column(SlotIdentifier slot, IValueDomain domain) { ... }
```

`Build()` walks each arm's `IPatternOperation` tree:
- `IDiscardPatternOperation` → `Cell.Wildcard`
- `IDeclarationPatternOperation` with `var` → `Cell.Wildcard`
- `IConstantPatternOperation` → `Cell.Constraint` with singleton domain
- `IRelationalPatternOperation` → `Cell.Constraint` with range domain
- `IBinaryPatternOperation(Or)` → `Cell.Constraint` with union of sub-constraints
- `IBinaryPatternOperation(And)` → `Cell.Constraint` with intersection
- `INegatedPatternOperation` → `Cell.Constraint` with complement
- `IRecursivePatternOperation` → expand into sub-columns
- `ITypePatternOperation` → `Cell.Constraint` for type domain
- Arms with `when` guards → skip entirely

- [ ] **Step 4: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~PatternMatrixTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/Algorithm/PatternMatrix.cs tests/Houtamelo.Spire.PatternAnalysis.Tests/Algorithm/PatternMatrixTests.cs
git commit -m "feat(pattern-analysis): add PatternMatrix — rows, columns, cells, specialization"
```

---

## Task 13: DecisionTreeBuilder (TDD)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/Algorithm/DecisionTreeBuilder.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Algorithm/DecisionTreeBuilderTests.cs`

The core Maranget algorithm.

- [ ] **Step 1: Write DecisionTreeBuilder tests**

Test cases using pre-built `PatternMatrix` instances:

- Empty matrix (0 rows) → not exhaustive, returns MissingCase with full column domains
- Single wildcard row → exhaustive
- Bool column: 2 rows (true, false) → exhaustive
- Bool column: 1 row (true) → not exhaustive, missing `{false}`
- Enum column: all members covered → exhaustive
- Enum column: missing one → reports correct missing member
- 2-column (bool, bool): 4 rows → exhaustive
- 2-column (bool, bool): 3 rows → not exhaustive, reports correct missing combo
- 2-column (enum, bool): partial coverage → correct missing combos
- Numeric: `> 0` + `<= 0` → exhaustive for int
- Numeric: `> 0` + `< 0` → not exhaustive, missing `0`
- Nested: 3 columns from flattened tuple → correct specialization propagation

- [ ] **Step 2: Run tests — verify they fail**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~DecisionTreeBuilderTests"`
Expected: FAIL.

- [ ] **Step 3: Implement DecisionTreeBuilder**

```csharp
internal static class DecisionTreeBuilder
{
    public static ExhaustivenessResult Check(PatternMatrix matrix)
    {
        var missingCases = new List<MissingCase>();
        CheckCore(matrix, ImmutableArray<SlotConstraint>.Empty, missingCases);
        return new ExhaustivenessResult(missingCases.ToImmutableArray());
    }

    private static void CheckCore(
        PatternMatrix matrix,
        ImmutableArray<SlotConstraint> accumulated,
        List<MissingCase> missingCases)
    {
        // Base cases
        if (matrix.ColumnCount == 0)
            return; // exhaustive — all constraints satisfied

        if (matrix.RowCount == 0)
        {
            // Gap found — collect remaining domains
            var constraints = accumulated;
            foreach (var col in matrix.Columns)
                constraints = constraints.Add(new SlotConstraint(col.Slot, col.Domain));
            missingCases.Add(new MissingCase(constraints));
            return;
        }

        // Pick column (heuristic: smallest domain first, or leftmost non-wildcard)
        int colIdx = SelectColumn(matrix);
        var column = matrix.Columns[colIdx];

        // Split column domain into partitions
        var partitions = column.Domain.Split();

        foreach (var partition in partitions)
        {
            var specialized = matrix.Specialize(colIdx, partition);
            var newAccumulated = accumulated.Add(
                new SlotConstraint(column.Slot, partition));
            CheckCore(specialized, newAccumulated, missingCases);
        }
    }

    private static int SelectColumn(PatternMatrix matrix) { ... }
}
```

Column selection heuristic: prefer columns with the smallest `Split()` count (fewest partitions). Ties broken by leftmost. This minimizes branching.

- [ ] **Step 4: Run tests — verify they pass**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~DecisionTreeBuilderTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/Algorithm/DecisionTreeBuilder.cs tests/Houtamelo.Spire.PatternAnalysis.Tests/Algorithm/DecisionTreeBuilderTests.cs
git commit -m "feat(pattern-analysis): add DecisionTreeBuilder — Maranget algorithm core"
```

---

## Task 14: ExhaustivenessChecker (entry point)

**Files:**
- Create: `src/Houtamelo.Spire.PatternAnalysis/ExhaustivenessChecker.cs`

Wires everything together: DomainResolver → PatternMatrix.Build → DecisionTreeBuilder.Check.

- [ ] **Step 1: Implement ExhaustivenessChecker**

```csharp
// src/Houtamelo.Spire.PatternAnalysis/ExhaustivenessChecker.cs
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Houtamelo.Spire.PatternAnalysis.Algorithm;
using Houtamelo.Spire.PatternAnalysis.Resolution;

namespace Houtamelo.Spire.PatternAnalysis;

internal static class ExhaustivenessChecker
{
    public static ExhaustivenessResult Check(
        Compilation compilation,
        ISwitchExpressionOperation switchExpr)
    {
        var resolver = CreateResolver(compilation);
        var matrix = PatternMatrix.Build(switchExpr, resolver);
        return DecisionTreeBuilder.Check(matrix);
    }

    public static ExhaustivenessResult Check(
        Compilation compilation,
        ISwitchOperation switchStmt)
    {
        var resolver = CreateResolver(compilation);
        var matrix = PatternMatrix.Build(switchStmt, resolver);
        return DecisionTreeBuilder.Check(matrix);
    }

    private static DomainResolver CreateResolver(Compilation compilation)
    {
        var hierarchyResolver = new TypeHierarchyResolver();
        return new DomainResolver(compilation, hierarchyResolver);
    }
}
```

Note: In production use (from Houtamelo.Spire.Analyzers), the `TypeHierarchyResolver` should be created once per compilation in `CompilationStartAction` and reused. The entry point may need an overload accepting a pre-created resolver. Defer this optimization to the integration task (Task 17).

- [ ] **Step 2: Write a minimal end-to-end smoke test**

Create `tests/Houtamelo.Spire.PatternAnalysis.Tests/ExhaustivenessCheckerSmokeTest.cs`. Compile a trivial bool switch (`true => 1, false => 2`), extract the `ISwitchExpressionOperation`, call `ExhaustivenessChecker.Check()`, assert `IsExhaustive == true`. This validates the full pipeline wiring.

- [ ] **Step 3: Run smoke test**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~SmokeTest"`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/Houtamelo.Spire.PatternAnalysis/ExhaustivenessChecker.cs tests/Houtamelo.Spire.PatternAnalysis.Tests/ExhaustivenessCheckerSmokeTest.cs
git commit -m "feat(pattern-analysis): add ExhaustivenessChecker entry point"
```

---

## Task 15: Integration Test Infrastructure

**Files:**
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/ExhaustivenessTestBase.cs`

Adapts the `AnalyzerTestBase` pattern for exhaustiveness testing. File-based discovery, but instead of checking diagnostics, it calls `ExhaustivenessChecker.Check()` and asserts the result.

- [ ] **Step 1: Implement ExhaustivenessTestBase**

Key differences from `AnalyzerTestBase`:
- Header: `//@ exhaustive` or `//@ not_exhaustive` (instead of `should_fail`/`should_pass`)
- No error markers (`//~ ERROR`) — we check the `ExhaustivenessResult` instead
- Must locate the switch expression/statement in the compiled case file
- Asserts `result.IsExhaustive` matches the header directive

```csharp
public abstract class ExhaustivenessTestBase
{
    protected abstract string Category { get; } // e.g., "Bool", "Enum", "Tuple"

    [Theory]
    [ExhaustivenessTestDiscovery("exhaustive")]
    public async Task Exhaustive(string caseName) { ... }

    [Theory]
    [ExhaustivenessTestDiscovery("not_exhaustive")]
    public async Task NotExhaustive(string caseName) { ... }
}
```

Test discovery attribute scans `Integration/{Category}/cases/` for `.cs` files with matching header.

Compilation setup:
- Parse `_shared.cs` + case file as separate syntax trees
- Create `CSharpCompilation` with `Net80` references + `Houtamelo.Spire` assembly reference
- Extract `ISwitchExpressionOperation` or `ISwitchOperation` from the case file's semantic model
- Run `ExhaustivenessChecker.Check(compilation, switchOp)`
- Assert result matches header

To locate the switch operation: walk the case file's syntax tree for `SwitchExpressionSyntax` or `SwitchStatementSyntax`, then use `semanticModel.GetOperation()`.

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: Success. (No test cases exist yet, so no tests run.)

- [ ] **Step 3: Commit**

```bash
git add tests/Houtamelo.Spire.PatternAnalysis.Tests/ExhaustivenessTestBase.cs
git commit -m "feat(pattern-analysis): add ExhaustivenessTestBase — file-based integration test infrastructure"
```

---

## Task 16: Integration Tests — Bool + Enum + NumericRange

**Files:**
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Bool/BoolTests.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Bool/cases/_shared.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Bool/cases/*.cs` (4-6 cases)
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Enum/EnumTests.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Enum/cases/_shared.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Enum/cases/*.cs` (4-6 cases)
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/NumericRange/NumericRangeTests.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/NumericRange/cases/_shared.cs`
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/NumericRange/cases/*.cs` (4-6 cases)

- [ ] **Step 1: Create Bool test runner and shared preamble**

```csharp
// BoolTests.cs
public class BoolTests : ExhaustivenessTestBase
{
    protected override string Category => "Bool";
}
```

```csharp
// _shared.cs — empty or minimal, bool is built-in
global using System;
```

- [ ] **Step 2: Create Bool test cases**

Example cases:

`TrueAndFalse.cs`:
```csharp
//@ exhaustive
// Both bool values covered
public class TrueAndFalse
{
    public int Test(bool b) => b switch
    {
        true => 1,
        false => 2,
    };
}
```

`TrueOnly.cs`:
```csharp
//@ not_exhaustive
// Missing false
public class TrueOnly
{
    public int Test(bool b) => b switch
    {
        true => 1,
    };
}
```

`NullableBoolFull.cs`:
```csharp
//@ exhaustive
// bool? with null + true + false
public class NullableBoolFull
{
    public int Test(bool? b) => b switch
    {
        null => 0,
        true => 1,
        false => 2,
    };
}
```

`NullableBoolMissingNull.cs`:
```csharp
//@ not_exhaustive
// bool? missing null
public class NullableBoolMissingNull
{
    public int Test(bool? b) => b switch
    {
        true => 1,
        false => 2,
    };
}
```

- [ ] **Step 3: Create Enum test cases**

Shared preamble:
```csharp
// _shared.cs
global using System;

public enum Color { Red, Green, Blue }
```

Cases: `AllMembers.cs` (exhaustive), `MissingOne.cs` (not), `NullableEnumFull.cs`, `NullableEnumMissingNull.cs`.

- [ ] **Step 4: Create NumericRange test cases**

Cases: `IntFullPartition.cs` (`> 0` + `<= 0`, exhaustive), `IntMissingZero.cs` (`> 0` + `< 0`, not exhaustive), `DoubleRelational.cs` (`> 50.0` + `<= 50.0`, exhaustive), `OverlappingRanges.cs`.

- [ ] **Step 5: Run all integration tests**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~Integration"`
Expected: ALL PASS.

- [ ] **Step 6: Commit**

```bash
git add tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Bool/ tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Enum/ tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/NumericRange/
git commit -m "test(pattern-analysis): add Bool, Enum, NumericRange integration tests"
```

---

## Task 17: Integration Tests — Null + Tuple + PropertyPattern + NestedRecursive

**Files:**
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Null/` (runner, shared, cases)
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Tuple/` (runner, shared, cases)
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/PropertyPattern/` (runner, shared, cases)
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/NestedRecursive/` (runner, shared, cases)

- [ ] **Step 1: Create Null category**

Shared: define a simple class. Cases: `StringNullableOblivious.cs` (null + not null, exhaustive), `StringAnnotatedNotNull.cs` (non-nullable ref, no null needed), `NullableValueType.cs`.

- [ ] **Step 2: Create Tuple category**

Shared: define enums/types. Cases: `BoolBoolFull.cs` (4 combos, exhaustive), `BoolBoolMissing.cs` (3 combos, not exhaustive), `EnumBoolCrossProduct.cs`, `NestedTuple.cs` (`((bool, bool), bool)` — 8 combos).

- [ ] **Step 3: Create PropertyPattern category**

Shared: define struct with properties. Cases: `SinglePropertyBool.cs` (`{ Flag: true }` + `{ Flag: false }`, exhaustive), `MultiProperty.cs`, `NestedPropertyPattern.cs` (`{ Outer: { Inner: pattern } }`).

- [ ] **Step 4: Create NestedRecursive category — the motivating example**

Shared: define a Spire discriminated union `Shape` with `Circle(double radius)` and `Rectangle(double width, double height)`.

Cases:

`ShapeConditionFull.cs`:
```csharp
//@ exhaustive
// Original motivating example — all variants covered with bool condition
public class ShapeConditionFull
{
    public int Test(Shape shape, bool condition) => (shape, condition) switch
    {
        ((Shape.Kind.Circle, double r), true) => 1,
        ((Shape.Kind.Circle, double r), false) => 2,
        ((Shape.Kind.Rectangle, double w, double h), _) => 3,
    };
}
```

`ShapeConditionMissing.cs`:
```csharp
//@ not_exhaustive
// Circle only covered for true, not false
public class ShapeConditionMissing
{
    public int Test(Shape shape, bool condition) => (shape, condition) switch
    {
        ((Shape.Kind.Circle, double r), true) => 1,
        ((Shape.Kind.Rectangle, double w, double h), _) => 3,
    };
}
```

`DeepNesting.cs` — 3+ levels of tuple nesting.

- [ ] **Step 5: Run all integration tests**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~Integration"`
Expected: ALL PASS.

- [ ] **Step 6: Commit**

```bash
git add tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Null/ tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Tuple/ tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/PropertyPattern/ tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/NestedRecursive/
git commit -m "test(pattern-analysis): add Null, Tuple, PropertyPattern, NestedRecursive integration tests"
```

---

## Task 18: Integration Tests — Union + TypeHierarchy + MixedDomain

**Files:**
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Union/` (runner, shared, cases)
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/TypeHierarchy/` (runner, shared, cases)
- Create: `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/MixedDomain/` (runner, shared, cases)

- [ ] **Step 1: Create Union category**

Shared: define both struct and record Spire discriminated unions.

Cases: `StructDeconstruct.cs` (deconstruct patterns, exhaustive), `StructProperty.cs` (property patterns, exhaustive), `RecordTypePattern.cs` (record union, type patterns, exhaustive), `MixedPatternStyles.cs` (some arms deconstruct, some property, exhaustive), `MissingVariant.cs` (not exhaustive).

- [ ] **Step 2: Create TypeHierarchy category**

Shared: define `[EnforceExhaustiveness]` marked class/interface with concrete subtypes.

Note: Requires `EnforceExhaustivenessAttribute` to support `AttributeTargets.Class | AttributeTargets.Interface` (prerequisite from spec). If not yet expanded, update the attribute before writing these tests.

Cases: `SealedHierarchyFull.cs` (all subtypes covered, exhaustive), `SealedHierarchyMissing.cs` (missing one, not exhaustive), `InterfaceHierarchy.cs` (interface with implementors).

- [ ] **Step 3: Create MixedDomain category**

Shared: define unions, enums, nullable types.

Cases: `EnumBoolNullable.cs` (cross-product of enum + bool? + relational), `UnionFieldIsUnion.cs` (nested union — variant field is another union type), `EnumInsideVariant.cs` (enum field within a union variant + relational on another field).

- [ ] **Step 4: Run all integration tests**

Run: `dotnet test --project tests/Houtamelo.Spire.PatternAnalysis.Tests/ --filter "FullyQualifiedName~Integration"`
Expected: ALL PASS.

- [ ] **Step 5: Run full test suite**

Run: `dotnet test`
Expected: ALL PASS (both PatternAnalysis tests and existing Houtamelo.Spire.Analyzers tests).

- [ ] **Step 6: Commit**

```bash
git add tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Union/ tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/TypeHierarchy/ tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/MixedDomain/
git commit -m "test(pattern-analysis): add Union, TypeHierarchy, MixedDomain integration tests"
```

---

## Execution Order

Tasks have the following dependencies:

```
Task 0 (expand attribute) — first, prerequisite for all
  └─ Task 1 (scaffold)
       └─ Task 2 (core types)
            ├─ Task 3 (Interval/IntervalSet)
            │    └─ Task 5 (NumericDomain)
            ├─ Task 4 (BoolDomain + EnumDomain)
            ├─ Task 6 (NullableDomain) ← depends on Task 4, Task 5
            ├─ Task 7 (Structural domains) ← depends on Task 4
            ├─ Task 8 (TypeHierarchyResolver)
            │    └─ Task 9 (EnforceExhaustiveDomain)
            ├─ Task 10 (DU domains) ← depends on Task 4, Task 7
            └─ Task 11 (DomainResolver) ← depends on Tasks 4-10
                 └─ Task 12 (PatternMatrix) ← depends on Task 11
                      └─ Task 13 (DecisionTreeBuilder)
                           └─ Task 14 (ExhaustivenessChecker)
                                └─ Task 15 (test infrastructure)
                                     ├─ Task 16 (Bool/Enum/Numeric integration)
                                     ├─ Task 17 (Null/Tuple/Property/Nested integration)
                                     └─ Task 18 (Union/TypeHierarchy/Mixed integration)
```

**Parallelizable groups** (tasks within a group have no mutual dependencies):
- Group A: Tasks 3, 4, 8 (can run in parallel after Task 2)
- Group B: Tasks 5, 6, 7, 9, 10 (after their respective dependencies)
- Group C: Tasks 16, 17, 18 (after Task 15)
