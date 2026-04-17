# Closure Inliner Generator — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `[InlinerStruct]` and `[Inlinable]` attributes plus two source generators that produce monomorphizable struct dispatch, eliminating delegate indirection in hot paths.

**Architecture:** Two `IIncrementalGenerator`s — one emits stateless inliner structs nested inside the declaring type; the other emits generic-struct-constrained twin methods with invocation-site rewrites. Per-arity `IActionInliner` / `IFuncInliner` interfaces (N = 8). Body rewriting uses `CSharpSyntaxRewriter` + `SemanticModel` to find invocations of the attributed parameter (and tracked aliases) and replace them with `.Invoke`. An analyzer (`InlinableUsageAnalyzer`) enforces SPIRE021 when the parameter is used outside the supported forms.

**Tech Stack:** Roslyn 5.0.0 (Microsoft.CodeAnalysis.CSharp), netstandard2.0 for generators/analyzers, xUnit for tests. Reuses existing `SourceBuilder` / `EquatableArray<T>` / snapshot test infrastructure.

**Spec:** `docs/superpowers/specs/2026-04-14-closure-inliner-generator-design.md`

---

## File Structure

### New source files

**`src/Houtamelo.Spire/`** (user-facing API)
- `IActionInliner.cs` — 9 interfaces (arity 0–8).
- `IFuncInliner.cs` — 9 interfaces (arity 0–8).
- `InlinerStructAttribute.cs` — attribute for Part 1.
- `InlinableAttribute.cs` — attribute for Part 2.

**`src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/`** (generator + analyzer)
- `InlinerDescriptors.cs` — 11 diagnostic descriptors (SPIRE017–SPIRE027).
- `InlinerStructGenerator.cs` — `IIncrementalGenerator` entry point for `[InlinerStruct]`.
- `InlinableTwinGenerator.cs` — `IIncrementalGenerator` entry point for `[Inlinable]`.
- `Model/InlinerStructDecl.cs` — model record for a `[InlinerStruct]`-marked method.
- `Model/InlinableHostDecl.cs` — model record for a method with `[Inlinable]` parameters.
- `Model/InlinableParam.cs` — per-parameter model.
- `Model/InlinerDiagnostic.cs` — diagnostic payload struct carried through the pipeline.
- `Parsing/InlinerStructParser.cs` — syntax/symbol → `InlinerStructDecl`.
- `Parsing/InlinableParser.cs` — syntax/symbol → `InlinableHostDecl`.
- `Emit/InlinerStructEmitter.cs` — `InlinerStructDecl` → generated source string.
- `Emit/InlinableTwinEmitter.cs` — `InlinableHostDecl` → generated source string.
- `Emit/InlinableBodyRewriter.cs` — `CSharpSyntaxRewriter` that rewrites host body.
- `Analyzers/InlinableUsageAnalyzer.cs` — `DiagnosticAnalyzer` enforcing SPIRE021/SPIRE022/SPIRE025/SPIRE026.

**Tests**
- `tests/Houtamelo.Spire.SourceGenerators.Tests/cases/Performance/InlinerStruct/<case>/input.cs|output.cs`
- `tests/Houtamelo.Spire.SourceGenerators.Tests/cases/Performance/Inlinable/<case>/input.cs|output.cs`
- `tests/Houtamelo.Spire.SourceGenerators.Tests/Performance/InlinerStructSnapshotTests.cs`
- `tests/Houtamelo.Spire.SourceGenerators.Tests/Performance/InlinableSnapshotTests.cs`
- `tests/Houtamelo.Spire.Analyzers.Tests/SPIRE021/SPIRE021Tests.cs` + `cases/`
- (and folders for SPIRE022, SPIRE025, SPIRE026 — the analyzer-reported ones)
- `tests/Houtamelo.Spire.BehavioralTests/Tests/InlinerTests.cs`
- `tests/Houtamelo.Spire.BehavioralTests/Types/InlinerTargets.cs`
- `benchmarks/Houtamelo.Spire.Benchmarks/Benchmarks/InlinerDispatch.cs`

### Modified files

- `src/Houtamelo.Spire.Analyzers/AnalyzerReleases.Unshipped.md` — add 11 new rules.

---

## Phase 1 — Foundation (interfaces, attributes, descriptors)

### Task 1: Create `IActionInliner` interfaces (arity 0–8)

**Files:**
- Create: `src/Houtamelo.Spire/IActionInliner.cs`

- [ ] **Step 1: Write the file**

```csharp
namespace Houtamelo.Spire;

/// <summary>
/// Stateless forwarder struct interface for zero-parameter void methods.
/// Used by [InlinerStruct]-generated types to enable JIT monomorphization
/// in place of Action delegate indirection.
/// </summary>
public interface IActionInliner
{
    void Invoke();
}

/// <summary>One-parameter void inliner. See <see cref="IActionInliner"/>.</summary>
public interface IActionInliner<T1>
{
    void Invoke(T1 a1);
}

/// <summary>Two-parameter void inliner.</summary>
public interface IActionInliner<T1, T2>
{
    void Invoke(T1 a1, T2 a2);
}

/// <summary>Three-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3>
{
    void Invoke(T1 a1, T2 a2, T3 a3);
}

/// <summary>Four-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3, T4>
{
    void Invoke(T1 a1, T2 a2, T3 a3, T4 a4);
}

/// <summary>Five-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3, T4, T5>
{
    void Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5);
}

/// <summary>Six-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3, T4, T5, T6>
{
    void Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6);
}

/// <summary>Seven-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3, T4, T5, T6, T7>
{
    void Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7);
}

/// <summary>Eight-parameter void inliner.</summary>
public interface IActionInliner<T1, T2, T3, T4, T5, T6, T7, T8>
{
    void Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7, T8 a8);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Houtamelo.Spire/Houtamelo.Spire.csproj`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```
git add src/Houtamelo.Spire/IActionInliner.cs
git commit -m "feat(spire): add IActionInliner interfaces (arity 0-8)"
```

### Task 2: Create `IFuncInliner` interfaces (arity 0–8)

**Files:**
- Create: `src/Houtamelo.Spire/IFuncInliner.cs`

- [ ] **Step 1: Write the file**

```csharp
namespace Houtamelo.Spire;

/// <summary>Zero-parameter func inliner.</summary>
public interface IFuncInliner<TR>
{
    TR Invoke();
}

/// <summary>One-parameter func inliner.</summary>
public interface IFuncInliner<T1, TR>
{
    TR Invoke(T1 a1);
}

/// <summary>Two-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, TR>
{
    TR Invoke(T1 a1, T2 a2);
}

/// <summary>Three-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3);
}

/// <summary>Four-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, T4, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3, T4 a4);
}

/// <summary>Five-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, T4, T5, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5);
}

/// <summary>Six-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, T4, T5, T6, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6);
}

/// <summary>Seven-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, T4, T5, T6, T7, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7);
}

/// <summary>Eight-parameter func inliner.</summary>
public interface IFuncInliner<T1, T2, T3, T4, T5, T6, T7, T8, TR>
{
    TR Invoke(T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7, T8 a8);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Houtamelo.Spire/Houtamelo.Spire.csproj`
Expected: succeeds.

- [ ] **Step 3: Commit**

```
git add src/Houtamelo.Spire/IFuncInliner.cs
git commit -m "feat(spire): add IFuncInliner interfaces (arity 0-8)"
```

### Task 3: Create attribute types

**Files:**
- Create: `src/Houtamelo.Spire/InlinerStructAttribute.cs`
- Create: `src/Houtamelo.Spire/InlinableAttribute.cs`

- [ ] **Step 1: Write `InlinerStructAttribute.cs`**

```csharp
using System;

namespace Houtamelo.Spire;

/// <summary>
/// Marks a method so the Spire source generator emits a sibling <c>readonly struct</c>
/// named <c>{MethodName}Inliner</c> implementing the matching <see cref="IActionInliner"/>
/// or <see cref="IFuncInliner{TR}"/> shape. The generated struct forwards <c>Invoke</c>
/// calls to the attributed method so consumers can substitute it in generic
/// struct-constrained APIs and get JIT monomorphization.
/// </summary>
/// <remarks>
/// The declaring type and every enclosing type must be declared <c>partial</c>.
/// Parameter modifiers (ref/in/out/ref readonly/params) are not supported in v1
/// and will produce SPIRE017. Declaring <c>ref struct</c> types are not
/// supported and produce SPIRE018. Total arity (plus instance for non-static
/// methods) cannot exceed 8 and will produce SPIRE019.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class InlinerStructAttribute : Attribute
{
}
```

- [ ] **Step 2: Write `InlinableAttribute.cs`**

```csharp
using System;

namespace Houtamelo.Spire;

/// <summary>
/// Marks a delegate-typed parameter so the Spire source generator emits a twin overload
/// of the containing method where the parameter is replaced with a generic struct
/// constrained to the matching <see cref="IActionInliner"/> / <see cref="IFuncInliner{TR}"/>
/// shape. Direct invocations of the parameter in the method body are rewritten to
/// <c>.Invoke(...)</c> on the generic struct, enabling JIT monomorphization.
/// </summary>
/// <remarks>
/// The containing type (and every enclosing type) must be <c>partial</c>. Delegate
/// arity must be ≤ 8. Nullability of the delegate parameter is preserved as
/// <see cref="Nullable{T}"/> on the twin. The parameter may only appear as the
/// target of an invocation or inside a single-assignment <c>var</c> alias; other
/// uses produce SPIRE021.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class InlinableAttribute : Attribute
{
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/Houtamelo.Spire/Houtamelo.Spire.csproj`
Expected: succeeds.

- [ ] **Step 4: Commit**

```
git add src/Houtamelo.Spire/InlinerStructAttribute.cs src/Houtamelo.Spire/InlinableAttribute.cs
git commit -m "feat(spire): add [InlinerStruct] and [Inlinable] attributes"
```

### Task 4: Add SPIRE017–SPIRE027 descriptors

**Files:**
- Create: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/InlinerDescriptors.cs`
- Modify: `src/Houtamelo.Spire.Analyzers/AnalyzerReleases.Unshipped.md`

- [ ] **Step 1: Write `InlinerDescriptors.cs`**

```csharp
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance;

internal static class InlinerDescriptors
{
    private const string Category = "SourceGeneration";

    public static readonly DiagnosticDescriptor SPIRE017_UnsupportedParameterModifier = new(
        id: "SPIRE017",
        title: "[InlinerStruct] method parameter has unsupported modifier",
        messageFormat: "Parameter '{0}' uses an unsupported modifier ({1}); [InlinerStruct] methods cannot have ref/in/out/ref readonly/params parameters",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE018_RefStructDeclaringType = new(
        id: "SPIRE018",
        title: "[InlinerStruct] not supported on ref struct",
        messageFormat: "[InlinerStruct] cannot be applied to a method declared on a ref struct",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE019_ArityExceeded = new(
        id: "SPIRE019",
        title: "[InlinerStruct] arity exceeds 8",
        messageFormat: "Method '{0}' has arity {1} (instance methods count the receiver), which exceeds the supported maximum of 8",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE020_NameCollision = new(
        id: "SPIRE020",
        title: "[InlinerStruct] generated struct name collides with existing type",
        messageFormat: "Generated struct '{0}' collides with an existing type in the same namespace/nesting",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE021_UnsupportedBodyUsage = new(
        id: "SPIRE021",
        title: "[Inlinable] parameter used in unsupported form",
        messageFormat: "[Inlinable] parameter '{0}' may only be invoked or aliased via single-assignment 'var'; {1}",
        category: "Correctness", defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE022_NonDelegateParameter = new(
        id: "SPIRE022",
        title: "[Inlinable] applied to non-delegate parameter",
        messageFormat: "[Inlinable] can only be applied to Action or Func parameters; '{0}' has type {1}",
        category: "Correctness", defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE023_ContainerNotPartial = new(
        id: "SPIRE023",
        title: "[Inlinable] method's containing type is not partial",
        messageFormat: "The type containing '[Inlinable]' parameter '{0}' must be declared 'partial' (and so must every enclosing type)",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE024_DelegateArityExceeded = new(
        id: "SPIRE024",
        title: "[Inlinable] delegate arity exceeds 8",
        messageFormat: "Delegate parameter '{0}' has arity {1}; supported maximum is 8",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE025_UnsupportedRefKind = new(
        id: "SPIRE025",
        title: "[Inlinable] parameter has unsupported ref-kind",
        messageFormat: "[Inlinable] parameter '{0}' cannot be declared with ref/in/out/ref readonly",
        category: "Correctness", defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE026_PropertyOrIndexerParameter = new(
        id: "SPIRE026",
        title: "[Inlinable] not supported on property/indexer parameters",
        messageFormat: "[Inlinable] is not supported on indexer or property accessor parameters in v1",
        category: "Correctness", defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE027_EnclosingTypeNotPartial = new(
        id: "SPIRE027",
        title: "Declaring type (or enclosing type) is not partial",
        messageFormat: "Type '{0}' must be declared 'partial' for inliner-struct generation",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);
}
```

- [ ] **Step 2: Append to `AnalyzerReleases.Unshipped.md`**

Open `src/Houtamelo.Spire.Analyzers/AnalyzerReleases.Unshipped.md` and add (preserving existing entries):

```
### New Rules

Rule ID     | Category         | Severity | Notes
------------|------------------|----------|--------------------------------------------------
SPIRE017 | SourceGeneration | Error    | [InlinerStruct] method has unsupported parameter modifier
SPIRE018 | SourceGeneration | Error    | [InlinerStruct] declared on a ref struct
SPIRE019 | SourceGeneration | Error    | [InlinerStruct] arity exceeds 8
SPIRE020 | SourceGeneration | Error    | Generated inliner struct name collides
SPIRE021 | Correctness      | Error    | [Inlinable] parameter used in unsupported form
SPIRE022 | Correctness      | Error    | [Inlinable] on non-delegate parameter
SPIRE023 | SourceGeneration | Error    | Containing type of [Inlinable] method is not partial
SPIRE024 | SourceGeneration | Error    | [Inlinable] delegate arity exceeds 8
SPIRE025 | Correctness      | Error    | [Inlinable] parameter has unsupported ref-kind
SPIRE026 | Correctness      | Error    | [Inlinable] on property/indexer parameter
SPIRE027 | SourceGeneration | Error    | Declaring/enclosing type is not partial
```

- [ ] **Step 3: Build**

Run: `dotnet build src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj`
Expected: succeeds (RS2008 satisfied by the release-tracking entries).

- [ ] **Step 4: Commit**

```
git add src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/InlinerDescriptors.cs src/Houtamelo.Spire.Analyzers/AnalyzerReleases.Unshipped.md
git commit -m "feat(analyzers): add SPIRE017-SPIRE027 descriptors"
```

### Task 5: Full-solution smoke build

- [ ] **Step 1: Build + test**

Run: `dotnet build && dotnet test`
Expected: both succeed. No new tests yet; existing tests must still pass.

- [ ] **Step 2: Commit if any fixups made**

If Step 1 required fixes (e.g., unused-variable warnings), commit those separately with a matching message.

---

## Phase 2 — `[InlinerStruct]` generator, happy paths

### Task 6: Create model types

**Files:**
- Create: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Model/InlinerStructDecl.cs`
- Create: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Model/InlinerDiagnostic.cs`

- [ ] **Step 1: Write `InlinerDiagnostic.cs`**

```csharp
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

internal readonly record struct InlinerDiagnostic(
    DiagnosticDescriptor Descriptor,
    LocationInfo Location,
    EquatableArray<string> MessageArgs);

internal readonly record struct LocationInfo(
    string FilePath,
    TextSpan Span,
    LinePositionSpan LineSpan)
{
    public static LocationInfo From(Location location)
    {
        var lineSpan = location.GetLineSpan();
        return new LocationInfo(
            lineSpan.Path ?? string.Empty,
            location.SourceSpan,
            lineSpan.Span);
    }

    public Location ToLocation()
        => Location.Create(FilePath, Span, LineSpan);
}
```

(Uses existing `EquatableArray<T>` from `Model/` and Microsoft.CodeAnalysis.Text types.)

- [ ] **Step 2: Write `InlinerStructDecl.cs`**

```csharp
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

internal sealed record InlinerStructDecl(
    string Namespace,
    string MethodName,
    string Accessibility,
    bool IsStatic,
    bool IsVoid,
    string? ReturnType,
    string DeclaringTypeName,
    EquatableArray<ContainingTypeInfo> ContainingTypes,
    EquatableArray<string> TypeParameters,
    EquatableArray<string> TypeParameterConstraints,
    EquatableArray<InlinerParamInfo> Parameters,
    InlinerDiagnostic? Diagnostic);

internal readonly record struct InlinerParamInfo(string Name, string Type);
```

(`ContainingTypeInfo` already exists under `SourceGenerators/Model/`; reuse it.)

- [ ] **Step 3: Build**

Run: `dotnet build src/Houtamelo.Spire.Analyzers`
Expected: succeeds.

- [ ] **Step 4: Commit**

```
git add src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Model/
git commit -m "feat(analyzers): add InlinerStructDecl model"
```

### Task 7: Write first snapshot test — static void, arity 0

**Files:**
- Create: `tests/Houtamelo.Spire.SourceGenerators.Tests/cases/Performance/InlinerStruct/static_void_arity0/input.cs`
- Create: `tests/Houtamelo.Spire.SourceGenerators.Tests/cases/Performance/InlinerStruct/static_void_arity0/output.cs`
- Create: `tests/Houtamelo.Spire.SourceGenerators.Tests/Performance/InlinerStructSnapshotTests.cs`

- [ ] **Step 1: Write `input.cs`**

```csharp
using Houtamelo.Spire;

namespace TestNs;

public static partial class Samples
{
    [InlinerStruct]
    public static void Run() { }
}
```

- [ ] **Step 2: Write `output.cs`**

```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public static partial class Samples
    {
        public readonly struct RunInliner : global::Houtamelo.Spire.IActionInliner
        {
            public void Invoke() => Samples.Run();
        }
    }
}
```

- [ ] **Step 3: Write `InlinerStructSnapshotTests.cs`**

```csharp
namespace Houtamelo.Spire.SourceGenerators.Tests.Performance;

public sealed class InlinerStructSnapshotTests : GeneratorSnapshotTestBase
{
    protected override string CasesSubdir => "Performance/InlinerStruct";
    protected override string GeneratorName => "InlinerStructGenerator";
}
```

(Assumes we extend `GeneratorSnapshotTestBase` with `CasesSubdir` and `GeneratorName` overrides. If the existing base doesn't support that, make this test class self-contained using the same helper APIs — see existing base for method names.)

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests --filter "FullyQualifiedName~InlinerStructSnapshotTests" --no-build -- --no-build`

(The `--no-build` is intentional: the test should first fail to build because `InlinerStructGenerator` doesn't exist yet. Once we add the stub in the next task, the generator will run but emit nothing, and the snapshot will mismatch.)

Expected: build failure OR snapshot mismatch. Record which.

- [ ] **Step 5: Commit (test-first)**

```
git add tests/Houtamelo.Spire.SourceGenerators.Tests/cases/Performance/ tests/Houtamelo.Spire.SourceGenerators.Tests/Performance/
git commit -m "test(inliner): add first snapshot case (static void arity 0)"
```

### Task 8: Create `InlinerStructGenerator` stub

**Files:**
- Create: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/InlinerStructGenerator.cs`

- [ ] **Step 1: Write stub**

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Emit;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance;

[Generator(LanguageNames.CSharp)]
public sealed class InlinerStructGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Houtamelo.Spire.InlinerStructAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var decls = context.SyntaxProvider.ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, ct) => InlinerStructParser.Parse(ctx, ct))
            .Where(static d => d is not null)
            .Select(static (d, _) => d!);

        context.RegisterSourceOutput(decls, static (ctx, decl) =>
        {
            if (decl.Diagnostic is { } diag)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    diag.Descriptor,
                    diag.Location.ToLocation(),
                    diag.MessageArgs.ToArray()));
                return;
            }

            var source = InlinerStructEmitter.Emit(decl);
            var hint = $"{decl.DeclaringTypeName}.{decl.MethodName}Inliner.g.cs";
            ctx.AddSource(hint, source);
        });
    }
}
```

- [ ] **Step 2: Write minimal `InlinerStructParser` stub (always returns null)**

Create `Parsing/InlinerStructParser.cs`:

```csharp
using System.Threading;
using Microsoft.CodeAnalysis;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;

internal static class InlinerStructParser
{
    public static InlinerStructDecl? Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        return null; // implemented in later task
    }
}
```

- [ ] **Step 3: Write minimal `InlinerStructEmitter` stub**

Create `Emit/InlinerStructEmitter.cs`:

```csharp
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Emit;

internal static class InlinerStructEmitter
{
    public static string Emit(InlinerStructDecl decl) => string.Empty;
}
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: succeeds. Snapshot test still fails (parser returns null, nothing generated).

- [ ] **Step 5: Commit**

```
git add src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/
git commit -m "feat(inliner): scaffold [InlinerStruct] generator"
```

### Task 9: Implement `InlinerStructParser` — static void arity 0

**Files:**
- Modify: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinerStructParser.cs`

- [ ] **Step 1: Replace parser stub with arity-0 static-void implementation**

```csharp
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;

internal static class InlinerStructParser
{
    public static InlinerStructDecl? Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method) return null;

        var containing = method.ContainingType;
        var nsName = containing.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : containing.ContainingNamespace.ToDisplayString();

        var containingChain = BuildContainingChain(containing);

        var parameters = method.Parameters
            .Select(p => new InlinerParamInfo(p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .ToImmutableArray();

        var typeParams = method.TypeParameters
            .Select(tp => tp.Name)
            .ToImmutableArray();

        var typeParamConstraints = ImmutableArray<string>.Empty; // fleshed out later

        var returnType = method.ReturnsVoid
            ? null
            : method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new InlinerStructDecl(
            Namespace: nsName,
            MethodName: method.Name,
            Accessibility: AccessibilityKeyword(method.DeclaredAccessibility),
            IsStatic: method.IsStatic,
            IsVoid: method.ReturnsVoid,
            ReturnType: returnType,
            DeclaringTypeName: containing.Name,
            ContainingTypes: new EquatableArray<ContainingTypeInfo>(containingChain),
            TypeParameters: new EquatableArray<string>(typeParams),
            TypeParameterConstraints: new EquatableArray<string>(typeParamConstraints),
            Parameters: new EquatableArray<InlinerParamInfo>(parameters),
            Diagnostic: null);
    }

    private static ImmutableArray<ContainingTypeInfo> BuildContainingChain(INamedTypeSymbol type)
    {
        var list = new System.Collections.Generic.List<ContainingTypeInfo>();
        var current = type;
        while (current is not null)
        {
            list.Add(new ContainingTypeInfo(
                Name: current.Name,
                Keyword: TypeKeyword(current),
                IsStatic: current.IsStatic,
                IsPartial: true,
                TypeParameters: current.TypeParameters.Select(tp => tp.Name).ToImmutableArray()));
            current = current.ContainingType;
        }
        list.Reverse();
        return list.ToImmutableArray();
    }

    private static string TypeKeyword(INamedTypeSymbol t)
        => t.TypeKind switch
        {
            TypeKind.Class  => t.IsRecord ? "record" : "class",
            TypeKind.Struct => t.IsRecord ? "record struct" : (t.IsReadOnly ? "readonly struct" : "struct"),
            _ => "class",
        };

    private static string AccessibilityKeyword(Accessibility a)
        => a switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.Private => "private",
            _ => "internal",
        };
}
```

(`ContainingTypeInfo` shape is assumed from existing `SourceGenerators/Model/` — adjust field list to whatever the existing record declares.)

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: succeeds. Snapshot test runs; still fails (emitter returns empty).

- [ ] **Step 3: Commit**

```
git add src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinerStructParser.cs
git commit -m "feat(inliner): parse [InlinerStruct] methods into InlinerStructDecl"
```

### Task 10: Implement `InlinerStructEmitter` — static void arity 0

**Files:**
- Modify: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Emit/InlinerStructEmitter.cs`

- [ ] **Step 1: Replace stub with arity-0 void implementation**

```csharp
using System.Linq;
using System.Text;
using Houtamelo.Spire.Analyzers.SourceGenerators.Emit;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Emit;

internal static class InlinerStructEmitter
{
    public static string Emit(InlinerStructDecl decl)
    {
        var sb = new SourceBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        var hasNs = !string.IsNullOrEmpty(decl.Namespace);
        if (hasNs)
        {
            sb.AppendLine($"namespace {decl.Namespace}");
            sb.OpenBrace();
        }

        sb.OpenContainingTypes(decl.ContainingTypes);

        var structName = $"{decl.MethodName}Inliner";
        var interfaceName = BuildInterfaceName(decl);
        var invokeSig = BuildInvokeSignature(decl);
        var invokeBody = BuildInvokeBody(decl);

        sb.AppendLine($"{decl.Accessibility} readonly struct {structName} : {interfaceName}");
        sb.OpenBrace();
        sb.AppendLine($"public {(decl.IsVoid ? "void" : decl.ReturnType)} Invoke({invokeSig})");
        sb.AppendLine($"    => {invokeBody};");
        sb.CloseBrace();

        sb.CloseContainingTypes(decl.ContainingTypes);

        if (hasNs) sb.CloseBrace();

        return sb.ToString();
    }

    private static string BuildInterfaceName(InlinerStructDecl decl)
    {
        var basis = decl.IsVoid ? "IActionInliner" : "IFuncInliner";
        var instancePrefix = decl.IsStatic
            ? string.Empty
            : BuildInstanceTypePrefix(decl);
        var paramTypes = decl.Parameters.Select(p => p.Type);
        var typeArgs = (instancePrefix.Length == 0 ? System.Linq.Enumerable.Empty<string>() : new[] { instancePrefix })
            .Concat(paramTypes);

        if (!decl.IsVoid && decl.ReturnType is not null)
            typeArgs = typeArgs.Concat(new[] { decl.ReturnType });

        var args = string.Join(", ", typeArgs);
        return args.Length == 0
            ? $"global::Houtamelo.Spire.{basis}"
            : $"global::Houtamelo.Spire.{basis}<{args}>";
    }

    private static string BuildInstanceTypePrefix(InlinerStructDecl decl)
        => decl.DeclaringTypeName; // refined for generic declaring types in later task

    private static string BuildInvokeSignature(InlinerStructDecl decl)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (!decl.IsStatic)
            parts.Add($"{decl.DeclaringTypeName} instance");
        int i = 1;
        foreach (var p in decl.Parameters)
            parts.Add($"{p.Type} a{i++}");
        return string.Join(", ", parts);
    }

    private static string BuildInvokeBody(InlinerStructDecl decl)
    {
        var args = string.Join(", ", Enumerable.Range(1, decl.Parameters.Length).Select(i => $"a{i}"));
        var call = decl.IsStatic
            ? $"{decl.DeclaringTypeName}.{decl.MethodName}({args})"
            : $"instance.{decl.MethodName}({args})";
        return decl.IsVoid ? call : $"return {call}";
    }
}
```

- [ ] **Step 2: Run the snapshot test**

Run: `dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests --filter "FullyQualifiedName~InlinerStructSnapshotTests"`
Expected: PASS.

- [ ] **Step 3: Commit**

```
git add src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Emit/InlinerStructEmitter.cs
git commit -m "feat(inliner): emit IActionInliner struct for static void arity 0"
```

### Task 11: Extend to static-with-args (arity 1–8)

**Files:**
- Create: `tests/.../cases/Performance/InlinerStruct/static_void_arity1/{input,output}.cs`
- Create: `tests/.../cases/Performance/InlinerStruct/static_void_arity3/{input,output}.cs`
- Create: `tests/.../cases/Performance/InlinerStruct/static_void_arity8/{input,output}.cs`

- [ ] **Step 1: Write arity-1 case**

`input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public static partial class Samples
{
    [InlinerStruct]
    public static void Log(string msg) { System.Console.WriteLine(msg); }
}
```

`output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public static partial class Samples
    {
        public readonly struct LogInliner : global::Houtamelo.Spire.IActionInliner<string>
        {
            public void Invoke(string a1) => Samples.Log(a1);
        }
    }
}
```

- [ ] **Step 2: Write arity-3 case**

`input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public static partial class Samples
{
    [InlinerStruct]
    public static void Op(int a, int b, int c) { }
}
```

`output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public static partial class Samples
    {
        public readonly struct OpInliner : global::Houtamelo.Spire.IActionInliner<int, int, int>
        {
            public void Invoke(int a1, int a2, int a3) => Samples.Op(a1, a2, a3);
        }
    }
}
```

- [ ] **Step 3: Write arity-8 case** (full 8 params).

`input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public static partial class Samples
{
    [InlinerStruct]
    public static void Big(int a, int b, int c, int d, int e, int f, int g, int h) { }
}
```

`output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public static partial class Samples
    {
        public readonly struct BigInliner : global::Houtamelo.Spire.IActionInliner<int, int, int, int, int, int, int, int>
        {
            public void Invoke(int a1, int a2, int a3, int a4, int a5, int a6, int a7, int a8)
                => Samples.Big(a1, a2, a3, a4, a5, a6, a7, a8);
        }
    }
}
```

- [ ] **Step 4: Run snapshots**

Run: `dotnet test --filter "FullyQualifiedName~InlinerStructSnapshotTests"`
Expected: the three new cases PASS (emitter already supports these — the arity-1/3/8 test validates generality).

- [ ] **Step 5: Commit**

```
git add tests/Houtamelo.Spire.SourceGenerators.Tests/cases/Performance/InlinerStruct/
git commit -m "test(inliner): cover static void arity 1, 3, 8"
```

### Task 12: Extend to non-void return (IFuncInliner)

**Files:**
- Create: `tests/.../InlinerStruct/static_func_arity1/{input,output}.cs`
- Create: `tests/.../InlinerStruct/static_func_arity0/{input,output}.cs`

- [ ] **Step 1: Write cases**

`static_func_arity1/input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public static partial class Samples
{
    [InlinerStruct]
    public static int Double(int x) => x * 2;
}
```

`static_func_arity1/output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public static partial class Samples
    {
        public readonly struct DoubleInliner : global::Houtamelo.Spire.IFuncInliner<int, int>
        {
            public int Invoke(int a1) => Samples.Double(a1);
        }
    }
}
```

`static_func_arity0/input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public static partial class Samples
{
    [InlinerStruct]
    public static int Zero() => 0;
}
```

`static_func_arity0/output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public static partial class Samples
    {
        public readonly struct ZeroInliner : global::Houtamelo.Spire.IFuncInliner<int>
        {
            public int Invoke() => Samples.Zero();
        }
    }
}
```

- [ ] **Step 2: Run snapshots**

Expected: both PASS (emitter already supports non-void via `BuildInterfaceName` returning `IFuncInliner`).

- [ ] **Step 3: Commit**

```
git add tests/Houtamelo.Spire.SourceGenerators.Tests/cases/Performance/InlinerStruct/static_func_arity*
git commit -m "test(inliner): cover static func arity 0 and 1"
```

### Task 13: Extend to instance methods

**Files:**
- Create: `tests/.../InlinerStruct/instance_void_arity1/{input,output}.cs`
- Create: `tests/.../InlinerStruct/instance_func_arity2/{input,output}.cs`

- [ ] **Step 1: Write cases**

`instance_void_arity1/input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public partial class Widget
{
    [InlinerStruct]
    public void Set(int v) { }
}
```

`instance_void_arity1/output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public partial class Widget
    {
        public readonly struct SetInliner : global::Houtamelo.Spire.IActionInliner<global::TestNs.Widget, int>
        {
            public void Invoke(global::TestNs.Widget instance, int a1) => instance.Set(a1);
        }
    }
}
```

`instance_func_arity2/input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public partial class Calc
{
    [InlinerStruct]
    public int Mul(int a, int b) => a * b;
}
```

`instance_func_arity2/output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public partial class Calc
    {
        public readonly struct MulInliner : global::Houtamelo.Spire.IFuncInliner<global::TestNs.Calc, int, int, int>
        {
            public int Invoke(global::TestNs.Calc instance, int a1, int a2) => instance.Mul(a1, a2);
        }
    }
}
```

- [ ] **Step 2: Update `InlinerStructEmitter.BuildInstanceTypePrefix`**

Change to emit fully qualified type name:
```csharp
private static string BuildInstanceTypePrefix(InlinerStructDecl decl)
{
    var ns = string.IsNullOrEmpty(decl.Namespace) ? "" : $"global::{decl.Namespace}.";
    return ns + decl.DeclaringTypeName;
}
```

Same update to `BuildInvokeSignature` for the `instance` parameter.

- [ ] **Step 3: Run snapshots**

Expected: PASS.

- [ ] **Step 4: Commit**

```
git add src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Emit/InlinerStructEmitter.cs tests/Houtamelo.Spire.SourceGenerators.Tests/cases/Performance/InlinerStruct/instance_*
git commit -m "feat(inliner): support instance methods in [InlinerStruct]"
```

### Task 14: Generic source methods — mirror generics

**Files:**
- Create: `tests/.../InlinerStruct/generic_method/{input,output}.cs`

- [ ] **Step 1: Write case**

`input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public static partial class Samples
{
    [InlinerStruct]
    public static void Log<T>(T value) { }
}
```

`output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public static partial class Samples
    {
        public readonly struct LogInliner<T> : global::Houtamelo.Spire.IActionInliner<T>
        {
            public void Invoke(T a1) => Samples.Log<T>(a1);
        }
    }
}
```

- [ ] **Step 2: Update emitter to include method's type parameters on the struct and on the invocation**

In `InlinerStructEmitter.Emit`:
- Append `<{typeParamsList}>` to struct name when `decl.TypeParameters.Length > 0`.
- In `BuildInvokeBody`, append `<{typeParamsList}>` to the method call.
- Include type parameter constraints from `decl.TypeParameterConstraints`.

Also update `InlinerStructParser` to populate `TypeParameterConstraints` from `method.TypeParameters[i].ConstraintTypes` / `HasValueTypeConstraint` / `HasReferenceTypeConstraint` / `HasConstructorConstraint` — assemble into `where T : ...` strings.

- [ ] **Step 3: Run snapshot**

Expected: PASS.

- [ ] **Step 4: Commit**

```
git add .
git commit -m "feat(inliner): mirror generic method type parameters onto inliner struct"
```

### Task 15: Methods on generic types

**Files:**
- Create: `tests/.../InlinerStruct/generic_type/{input,output}.cs`

- [ ] **Step 1: Write case**

`input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public partial class Box<T>
{
    [InlinerStruct]
    public T Get() => default!;
}
```

`output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public partial class Box<T>
    {
        public readonly struct GetInliner : global::Houtamelo.Spire.IFuncInliner<global::TestNs.Box<T>, T>
        {
            public T Invoke(global::TestNs.Box<T> instance) => instance.Get();
        }
    }
}
```

- [ ] **Step 2: Update emitter — include declaring-type generics in the fully qualified name**

Update `BuildInstanceTypePrefix` to append `<{declaringTypeGenerics}>` when applicable. The parser's `ContainingTypeInfo` already carries type parameters; thread them through.

- [ ] **Step 3: Run snapshot; commit**

```
git add .
git commit -m "feat(inliner): support methods on generic declaring types"
```

### Task 16: Nested declaring types

**Files:**
- Create: `tests/.../InlinerStruct/nested_types/{input,output}.cs`

- [ ] **Step 1: Write case**

`input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public partial class Outer
{
    public partial class Inner
    {
        [InlinerStruct]
        public static int Double(int x) => x * 2;
    }
}
```

`output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public partial class Outer
    {
        public partial class Inner
        {
            public readonly struct DoubleInliner : global::Houtamelo.Spire.IFuncInliner<int, int>
            {
                public int Invoke(int a1) => Inner.Double(a1);
            }
        }
    }
}
```

- [ ] **Step 2: Run snapshot**

Expected: PASS (uses existing `SourceBuilder.OpenContainingTypes` which handles nesting).

- [ ] **Step 3: Commit**

```
git add .
git commit -m "test(inliner): cover nested declaring types"
```

### Task 17: Full InlinerStruct happy-path sweep

- [ ] **Step 1: Run all InlinerStruct snapshot tests**

Run: `dotnet test --filter "FullyQualifiedName~InlinerStructSnapshotTests"`
Expected: all tests PASS.

- [ ] **Step 2: Run full build + test**

Run: `dotnet build && dotnet test`
Expected: no regressions.

- [ ] **Step 3: Commit any fixups**

---

## Phase 3 — `[InlinerStruct]` diagnostics

### Task 18: SPIRE017 — parameter modifiers

**Files:**
- Create: `tests/Houtamelo.Spire.Analyzers.Tests/SPIRE017/SPIRE017Tests.cs` + `cases/`

- [ ] **Step 1: Write runner**

```csharp
namespace Houtamelo.Spire.Analyzers.Tests.SPIRE017;

public sealed class SPIRE017Tests : GeneratorDiagnosticTestBase
{
    protected override string RuleId => "SPIRE017";
}
```

If no `GeneratorDiagnosticTestBase` exists, use the existing snapshot helpers: write a small test class that invokes the generator and asserts the expected diagnostic id appears.

- [ ] **Step 2: Write should_fail cases** (one per modifier kind: `ref`, `in`, `out`, `ref readonly`, `params`)

`cases/ref_parameter.cs`:
```csharp
//@ should_fail
// [InlinerStruct] cannot be applied to methods with ref parameters.
using Houtamelo.Spire;
namespace TestNs;
public static partial class Samples
{
    [InlinerStruct] //~ ERROR
    public static void Take(ref int x) { }
}
```

(Similar files for in/out/ref_readonly/params.)

- [ ] **Step 3: Write should_pass case**

`cases/plain_parameters.cs`:
```csharp
//@ should_pass
using Houtamelo.Spire;
namespace TestNs;
public static partial class Samples
{
    [InlinerStruct]
    public static void Take(int x) { }
}
```

- [ ] **Step 4: Update parser to emit diagnostic on modifier**

In `InlinerStructParser.Parse`, before returning the decl, check `method.Parameters`:
```csharp
foreach (var p in method.Parameters)
{
    if (p.RefKind != RefKind.None || p.IsParams)
    {
        var modifier = p.IsParams ? "params" : p.RefKind.ToString().ToLowerInvariant();
        var diag = new InlinerDiagnostic(
            InlinerDescriptors.SPIRE017_UnsupportedParameterModifier,
            LocationInfo.From(p.Locations.FirstOrDefault() ?? Location.None),
            new EquatableArray<string>(ImmutableArray.Create(p.Name, modifier)));
        return BuildPartialDeclWithDiagnostic(method, containing, diag);
    }
}
```

Where `BuildPartialDeclWithDiagnostic` returns a minimal `InlinerStructDecl` with the diagnostic field set. The generator's `RegisterSourceOutput` already handles it.

- [ ] **Step 5: Run tests**

Expected: `should_fail` cases PASS (diagnostic emitted); `should_pass` case PASSES (no diagnostic).

- [ ] **Step 6: Commit**

```
git add .
git commit -m "feat(inliner): SPIRE017 diagnostic for parameter modifiers"
```

### Task 19: SPIRE018 — ref struct declaring type

Same shape as Task 18 but:
- Should-fail case: method on `public ref struct Foo { [InlinerStruct] public void M() {} }`.
- Should-pass case: method on a normal class.
- Parser check: `method.ContainingType.IsRefLikeType`.

- [ ] **Step 1: Test cases**
- [ ] **Step 2: Parser check**
- [ ] **Step 3: Run + commit**

### Task 20: SPIRE019 — arity > 8

- Should-fail: static method with 9 params.
- Should-fail: instance method with 8 params (9 total).
- Should-pass: instance method with 7 params (8 total).
- Parser: compute `arity = method.Parameters.Length + (method.IsStatic ? 0 : 1)`; emit diagnostic if > 8.

- [ ] **Step 1: Test cases**
- [ ] **Step 2: Parser check**
- [ ] **Step 3: Run + commit**

### Task 21: SPIRE020 — struct name collision

- Should-fail: `public partial class C { public class FooInliner { } [InlinerStruct] public static void Foo() {} }`.
- Should-pass: when no collision.
- Parser: after building `InlinerStructDecl`, look up `{MethodName}Inliner` on the containing type's members. If any non-generated member has that name, emit diagnostic.

- [ ] **Step 1: Test cases**
- [ ] **Step 2: Parser check**
- [ ] **Step 3: Run + commit**

### Task 22: SPIRE027 — enclosing chain not partial

- Should-fail: `[InlinerStruct]` on method of non-partial class.
- Should-fail: method on `partial class Inner` inside non-partial `Outer`.
- Should-pass: all partial.
- Parser: walk `containingChain`, check each `INamedTypeSymbol` — a type is partial if **any** of its `DeclaringSyntaxReferences.GetSyntax()` returns a `TypeDeclarationSyntax` with `PartialKeyword`. If any link is non-partial, emit diagnostic naming that type.

- [ ] **Step 1: Test cases**
- [ ] **Step 2: Parser check**
- [ ] **Step 3: Run + commit**

---

## Phase 4 — `[Inlinable]` generator, happy paths

### Task 23: Create `[Inlinable]` model types

**Files:**
- Create: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Model/InlinableHostDecl.cs`
- Create: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Model/InlinableParam.cs`

- [ ] **Step 1: Write `InlinableParam.cs`**

```csharp
namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

internal sealed record InlinableParam(
    string Name,
    int Position,
    bool IsNullable,
    bool IsFunc,
    EquatableArray<string> DelegateTypeArguments,
    string ReturnType,
    string GenericName);
```

`DelegateTypeArguments` carries the delegate's type args in order (for `Action<T>` it's `[T]`; for `Func<A,R>` it's `[A]` with `ReturnType = "R"`). `IsFunc` distinguishes the two.

- [ ] **Step 2: Write `InlinableHostDecl.cs`**

```csharp
namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

internal sealed record InlinableHostDecl(
    string Namespace,
    string MethodName,
    string Accessibility,
    bool IsStatic,
    string ReturnType,
    string DeclaringTypeName,
    EquatableArray<ContainingTypeInfo> ContainingTypes,
    EquatableArray<string> HostTypeParameters,
    EquatableArray<string> HostTypeParameterConstraints,
    EquatableArray<InlinableParam> InlinableParams,
    EquatableArray<HostParamInfo> OtherParams,
    string OriginalBody,
    string OriginalFilePath,
    InlinerDiagnostic? Diagnostic);

internal sealed record HostParamInfo(
    string Name,
    string Type,
    int Position,
    string? RefKind,
    bool IsThis);
```

`OriginalBody` is the literal text of the method body (block or expression-bodied), and will be fed to the rewriter.

- [ ] **Step 3: Build; commit**

```
git add src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Model/
git commit -m "feat(inliner): add InlinableHostDecl model"
```

### Task 24: First snapshot — non-nullable Action<T>, direct invocation

**Files:**
- Create: `tests/.../cases/Performance/Inlinable/action_t_direct/{input,output}.cs`

- [ ] **Step 1: Write input**

```csharp
using System.Collections.Generic;
using Houtamelo.Spire;

namespace TestNs;

public static partial class ListExt
{
    public static void ForEach<T>(this List<T> list, [Inlinable] System.Action<T> action)
    {
        foreach (var item in list)
            action(item);
    }
}
```

- [ ] **Step 2: Write output**

```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public static partial class ListExt
    {
        public static void ForEach<T, TActionInliner>(this global::System.Collections.Generic.List<T> list, TActionInliner action)
            where TActionInliner : struct, global::Houtamelo.Spire.IActionInliner<T>
        {
            foreach (var item in list)
                action.Invoke(item);
        }
    }
}
```

- [ ] **Step 3: Create `InlinableSnapshotTests.cs`** mirroring the InlinerStruct test runner (same base class, different `CasesSubdir = "Performance/Inlinable"`).

- [ ] **Step 4: Run test; expect FAIL** (generator not implemented).

- [ ] **Step 5: Commit**

```
git add .
git commit -m "test(inliner): first [Inlinable] snapshot (Action<T> direct invocation)"
```

### Task 25: Implement `InlinableParser` — single non-nullable Action

**Files:**
- Create: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Parsing/InlinableParser.cs`

- [ ] **Step 1: Write parser**

```csharp
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;

internal static class InlinableParser
{
    private const string AttributeFullName = "Houtamelo.Spire.InlinableAttribute";

    // Trigger: the method declaration whose parameter has [Inlinable]. We use
    // ForAttributeWithMetadataName on the *parameter attribute* by registering
    // on the method and filtering at parse time (ForAttributeWithMetadataName
    // only tracks attribute target symbols directly, so parameter attributes
    // are observed via IParameterSymbol.GetAttributes()).

    public static InlinableHostDecl? Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method) return null;

        var inlinables = new List<InlinableParam>();
        var others = new List<HostParamInfo>();

        for (int i = 0; i < method.Parameters.Length; i++)
        {
            var p = method.Parameters[i];
            var inlinableAttr = p.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == AttributeFullName);
            if (inlinableAttr is not null)
            {
                var parsed = ParseDelegateParam(p, i);
                if (parsed is null)
                {
                    // SPIRE022 reported by analyzer; skip model for now
                    return null;
                }
                inlinables.Add(parsed);
            }
            else
            {
                others.Add(new HostParamInfo(
                    Name: p.Name,
                    Type: p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    Position: i,
                    RefKind: p.RefKind == RefKind.None ? null : p.RefKind.ToString().ToLowerInvariant(),
                    IsThis: i == 0 && method.IsExtensionMethod));
            }
        }

        if (inlinables.Count == 0) return null;

        var methodSyntax = ctx.TargetNode as MethodDeclarationSyntax;
        var bodyText = methodSyntax?.Body?.ToFullString()
            ?? (methodSyntax?.ExpressionBody is { } eb ? eb.ToFullString() : string.Empty);

        return new InlinableHostDecl(
            Namespace: method.ContainingType.ContainingNamespace.IsGlobalNamespace
                ? "" : method.ContainingType.ContainingNamespace.ToDisplayString(),
            MethodName: method.Name,
            Accessibility: AccessibilityKeyword(method.DeclaredAccessibility),
            IsStatic: method.IsStatic,
            ReturnType: method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            DeclaringTypeName: method.ContainingType.Name,
            ContainingTypes: new EquatableArray<ContainingTypeInfo>(BuildContainingChain(method.ContainingType)),
            HostTypeParameters: new EquatableArray<string>(method.TypeParameters.Select(tp => tp.Name).ToImmutableArray()),
            HostTypeParameterConstraints: new EquatableArray<string>(BuildConstraints(method.TypeParameters)),
            InlinableParams: new EquatableArray<InlinableParam>(inlinables.ToImmutableArray()),
            OtherParams: new EquatableArray<HostParamInfo>(others.ToImmutableArray()),
            OriginalBody: bodyText,
            OriginalFilePath: methodSyntax?.SyntaxTree.FilePath ?? "",
            Diagnostic: null);
    }

    private static InlinableParam? ParseDelegateParam(IParameterSymbol p, int position)
    {
        var isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        var type = p.Type;
        if (type is not INamedTypeSymbol named) return null;

        var full = named.OriginalDefinition.ToDisplayString();
        if (full.StartsWith("System.Action"))
        {
            var args = named.TypeArguments
                .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToImmutableArray();
            return new InlinableParam(
                Name: p.Name,
                Position: position,
                IsNullable: isNullable,
                IsFunc: false,
                DelegateTypeArguments: new EquatableArray<string>(args),
                ReturnType: "",
                GenericName: $"T{ToPascal(p.Name)}Inliner");
        }
        if (full.StartsWith("System.Func"))
        {
            var all = named.TypeArguments.ToArray();
            var paramTypes = all.Take(all.Length - 1)
                .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToImmutableArray();
            var ret = all.Last().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new InlinableParam(
                Name: p.Name,
                Position: position,
                IsNullable: isNullable,
                IsFunc: true,
                DelegateTypeArguments: new EquatableArray<string>(paramTypes),
                ReturnType: ret,
                GenericName: $"T{ToPascal(p.Name)}Inliner");
        }
        return null;
    }

    private static string ToPascal(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);

    // ... reuse BuildContainingChain / BuildConstraints / AccessibilityKeyword from InlinerStructParser
}
```

(Move `BuildContainingChain`, `AccessibilityKeyword`, etc. into a shared `InlinerParserUtilities` static class used by both parsers.)

- [ ] **Step 2: Add `InlinableTwinGenerator`** (entry point)

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Emit;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance;

[Generator(LanguageNames.CSharp)]
public sealed class InlinableTwinGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Inlinable is on a parameter; we trigger on the method declaration
        // and inspect its parameters' attributes. We use SyntaxProvider directly.
        var methods = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax mds
                    && mds.ParameterList.Parameters.Any(p => p.AttributeLists.Count > 0),
                transform: static (ctx, ct) =>
                {
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) as IMethodSymbol;
                    if (symbol is null) return null;
                    var hasInlinable = symbol.Parameters.Any(p =>
                        p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Houtamelo.Spire.InlinableAttribute"));
                    return hasInlinable
                        ? InlinableParser.Parse(
                            new GeneratorAttributeSyntaxContext(ctx.SemanticModel, ctx.Node, symbol,
                                System.Collections.Immutable.ImmutableArray<AttributeData>.Empty),
                            ct)
                        : null;
                })
            .Where(static d => d is not null)
            .Select(static (d, _) => d!);

        context.RegisterSourceOutput(methods, static (ctx, decl) =>
        {
            if (decl.Diagnostic is { } diag)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    diag.Descriptor, diag.Location.ToLocation(), diag.MessageArgs.ToArray()));
                return;
            }
            var source = InlinableTwinEmitter.Emit(decl);
            var hint = $"{decl.DeclaringTypeName}.{decl.MethodName}.InlinableTwin.g.cs";
            ctx.AddSource(hint, source);
        });
    }
}
```

Note: `GeneratorAttributeSyntaxContext`'s constructor isn't public; the parser needs an alternative entry that takes `(SemanticModel, SyntaxNode, IMethodSymbol)` directly. Adjust `InlinableParser.Parse` signature accordingly — accept `(IMethodSymbol method, SyntaxNode node, CancellationToken ct)`.

- [ ] **Step 3: Write `InlinableTwinEmitter` stub** that returns empty string.

- [ ] **Step 4: Build**

Expected: succeeds.

- [ ] **Step 5: Commit**

```
git add .
git commit -m "feat(inliner): scaffold [Inlinable] parser + generator"
```

### Task 26: Implement `InlinableTwinEmitter` — emit method header only

**Files:**
- Modify: `Emit/InlinableTwinEmitter.cs`

- [ ] **Step 1: Emit header + empty body placeholder**

```csharp
internal static class InlinableTwinEmitter
{
    public static string Emit(InlinableHostDecl decl)
    {
        var sb = new SourceBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        var hasNs = !string.IsNullOrEmpty(decl.Namespace);
        if (hasNs) { sb.AppendLine($"namespace {decl.Namespace}"); sb.OpenBrace(); }
        sb.OpenContainingTypes(decl.ContainingTypes);

        var allTypeParams = decl.HostTypeParameters
            .Concat(decl.InlinableParams.Select(p => p.GenericName))
            .ToArray();

        var tpList = allTypeParams.Length == 0 ? "" : $"<{string.Join(", ", allTypeParams)}>";

        var paramList = BuildParamList(decl);
        var staticKw = decl.IsStatic ? "static " : "";
        var retType = decl.ReturnType;

        sb.AppendLine($"{decl.Accessibility} {staticKw}{retType} {decl.MethodName}{tpList}({paramList})");

        // Constraints
        foreach (var c in decl.HostTypeParameterConstraints)
            sb.AppendLine($"    {c}");
        foreach (var p in decl.InlinableParams)
        {
            var iface = BuildInlinerInterface(p);
            sb.AppendLine($"    where {p.GenericName} : struct, {iface}");
        }

        sb.OpenBrace();
        var rewritten = InlinableBodyRewriter.Rewrite(decl);
        foreach (var line in rewritten.Split('\n'))
            sb.AppendLine(line.TrimEnd('\r'));
        sb.CloseBrace();

        sb.CloseContainingTypes(decl.ContainingTypes);
        if (hasNs) sb.CloseBrace();
        return sb.ToString();
    }

    private static string BuildParamList(InlinableHostDecl decl)
    {
        var all = new (int pos, string rendered)[decl.InlinableParams.Length + decl.OtherParams.Length];
        int idx = 0;
        foreach (var p in decl.InlinableParams)
        {
            var gen = p.IsNullable ? $"{p.GenericName}?" : p.GenericName;
            all[idx++] = (p.Position, $"{gen} {p.Name}");
        }
        foreach (var p in decl.OtherParams)
        {
            var prefix = p.IsThis ? "this " : "";
            var refKind = p.RefKind is null ? "" : $"{p.RefKind} ";
            all[idx++] = (p.Position, $"{prefix}{refKind}{p.Type} {p.Name}");
        }
        System.Array.Sort(all, (a, b) => a.pos.CompareTo(b.pos));
        return string.Join(", ", all.Select(t => t.rendered));
    }

    private static string BuildInlinerInterface(InlinableParam p)
    {
        var kind = p.IsFunc ? "IFuncInliner" : "IActionInliner";
        var args = p.DelegateTypeArguments.ToArray();
        if (p.IsFunc) args = args.Concat(new[] { p.ReturnType }).ToArray();
        return args.Length == 0
            ? $"global::Houtamelo.Spire.{kind}"
            : $"global::Houtamelo.Spire.{kind}<{string.Join(", ", args)}>";
    }
}
```

- [ ] **Step 2: Write `InlinableBodyRewriter` stub that returns the original body unchanged**

```csharp
internal static class InlinableBodyRewriter
{
    public static string Rewrite(InlinableHostDecl decl) => decl.OriginalBody;
}
```

- [ ] **Step 3: Build; run snapshot (FAIL — body not rewritten, expected rewrite)**

- [ ] **Step 4: Commit**

```
git add .
git commit -m "feat(inliner): emit [Inlinable] twin method header"
```

### Task 27: Implement body rewriter — direct invocation

**Files:**
- Modify: `Emit/InlinableBodyRewriter.cs`

- [ ] **Step 1: Use `CSharpSyntaxRewriter` + identifier-name matching**

```csharp
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Emit;

internal static class InlinableBodyRewriter
{
    public static string Rewrite(InlinableHostDecl decl)
    {
        var tree = CSharpSyntaxTree.ParseText(decl.OriginalBody);
        var root = tree.GetRoot();
        var names = new HashSet<string>(decl.InlinableParams.Select(p => p.Name));
        var nullable = decl.InlinableParams.Where(p => p.IsNullable)
            .Select(p => p.Name).ToHashSet();
        var walker = new Walker(names, nullable);
        var result = walker.Visit(root);
        return result.ToFullString();
    }

    private sealed class Walker : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _names;
        private readonly HashSet<string> _nullable;
        public Walker(HashSet<string> names, HashSet<string> nullable)
        {
            _names = names;
            _nullable = nullable;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax id && _names.Contains(id.Identifier.Text))
            {
                var newExpr = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    id.WithoutTrivia(),
                    SyntaxFactory.IdentifierName("Invoke"));
                return node.WithExpression(newExpr);
            }
            return base.VisitInvocationExpression(node);
        }
    }
}
```

- [ ] **Step 2: Run snapshot; expect PASS** for the `action_t_direct` case.

- [ ] **Step 3: Commit**

```
git add .
git commit -m "feat(inliner): body rewrite - direct parameter invocation"
```

### Task 28: Func return + multiple delegate arities

**Files:**
- Create: `tests/.../Inlinable/func_t_r_direct/{input,output}.cs`
- Create: `tests/.../Inlinable/action_arity0/{input,output}.cs`
- Create: `tests/.../Inlinable/action_arity8/{input,output}.cs`

- [ ] **Step 1: Write cases** exercising `Func<T, R>` (e.g., `Map`), `Action` arity 0, `Action` arity 8.

- [ ] **Step 2: Run snapshots; expect PASS** (emitter already covers these).

- [ ] **Step 3: Commit**

```
git add .
git commit -m "test(inliner): cover [Inlinable] Func/Action across arities"
```

### Task 29: Multiple `[Inlinable]` params

**Files:**
- Create: `tests/.../Inlinable/multi_inlinable/{input,output}.cs`

- [ ] **Step 1: Write case** (Pipeline.Run from spec).
- [ ] **Step 2: Run snapshot; expect PASS** (emitter already iterates all inlinable params).
- [ ] **Step 3: Commit**

### Task 30: Nullable delegate → `TInliner?`

**Files:**
- Create: `tests/.../Inlinable/nullable_direct/{input,output}.cs`
- Create: `tests/.../Inlinable/nullable_conditional_invocation/{input,output}.cs`

- [ ] **Step 1: Write `nullable_direct` case**

`input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public static partial class S
{
    public static void Call([Inlinable] System.Action<int>? action, int x)
        => action?.Invoke(x);
}
```

`output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public static partial class S
    {
        public static void Call<TActionInliner>(TActionInliner? action, int x)
            where TActionInliner : struct, global::Houtamelo.Spire.IActionInliner<int>
            => action?.Invoke(x);
    }
}
```

- [ ] **Step 2: Run snapshot; expect PASS** — the rewriter leaves `action?.Invoke(x)` alone because the identifier isn't a naked invocation target; and the emitter emits `TActionInliner?` because `IsNullable` is true.

- [ ] **Step 3: Write `nullable_conditional_invocation` case** — body uses `action?(x)`.

`input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public static partial class S
{
    public static void Call([Inlinable] System.Action<int>? action, int x)
        => action?(x);
}
```

`output.cs`: same shape, body `action?.Invoke(x)`.

- [ ] **Step 4: Update rewriter to handle `?()` invocation** (C# parses `action?(x)` as `ConditionalAccessExpression` with an `InvocationExpression` whose `Expression` is `MemberBindingExpression`). Actually: `action?(x)` isn't valid C# syntax in isolation — it's `action?.Invoke(x)` or `(action ?? throw)(x)`. Verify in `syntax-tree` skill; if `?()` is not real C# syntax, document in the spec and remove it from rewrite rules. If it *is* valid (via function-pointer shorthand), handle via `ConditionalAccessExpressionSyntax`.

- [ ] **Step 5: Run; commit**

### Task 31: Non-nullable + `?.Invoke` — strip null check

**Files:**
- Create: `tests/.../Inlinable/nonnull_strip_null_check/{input,output}.cs`

- [ ] **Step 1: Write case**

`input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public static partial class S
{
    public static void Call([Inlinable] System.Action<int> action, int x)
        => action?.Invoke(x);
}
```

`output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public static partial class S
    {
        public static void Call<TActionInliner>(TActionInliner action, int x)
            where TActionInliner : struct, global::Houtamelo.Spire.IActionInliner<int>
            => action.Invoke(x);
    }
}
```

- [ ] **Step 2: Extend rewriter to detect `ConditionalAccessExpressionSyntax` whose `Expression` is a non-nullable inlinable identifier, and drop the `?` — rewrite to a plain member access.**

```csharp
public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
{
    if (node.Expression is IdentifierNameSyntax id && _names.Contains(id.Identifier.Text) && !_nullable.Contains(id.Identifier.Text))
    {
        // action?.X(...)  →  action.X(...)
        var body = node.WhenNotNull; // MemberBindingExpression "Invoke" + invocation
        if (body is InvocationExpressionSyntax inv && inv.Expression is MemberBindingExpressionSyntax mb)
        {
            var access = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                id.WithoutTrivia(), mb.Name);
            return inv.WithExpression(access);
        }
    }
    return base.VisitConditionalAccessExpression(node);
}
```

- [ ] **Step 3: Run snapshot; expect PASS**

- [ ] **Step 4: Commit**

```
git add .
git commit -m "feat(inliner): body rewrite - strip null-check for non-nullable inlinable"
```

### Task 32: Alias tracking — `var a = p;`

**Files:**
- Create: `tests/.../Inlinable/alias_var/{input,output}.cs`

- [ ] **Step 1: Write case**

`input.cs`:
```csharp
using Houtamelo.Spire;
namespace TestNs;
public static partial class S
{
    public static void Call([Inlinable] System.Action<int> action, int x)
    {
        var a = action;
        a(x);
    }
}
```

`output.cs`:
```csharp
// <auto-generated/>
#nullable enable

namespace TestNs
{
    public static partial class S
    {
        public static void Call<TActionInliner>(TActionInliner action, int x)
            where TActionInliner : struct, global::Houtamelo.Spire.IActionInliner<int>
        {
            var a = action;
            a.Invoke(x);
        }
    }
}
```

- [ ] **Step 2: Extend rewriter to track aliases**

Before the rewrite, walk the body once with a collection pass: any `LocalDeclarationStatementSyntax` whose single declarator's initializer is `IdentifierNameSyntax` matching an already-tracked name. Add the declared local's name to `_names`. Repeat until fixpoint (transitive aliases).

```csharp
public static string Rewrite(InlinableHostDecl decl)
{
    var tree = CSharpSyntaxTree.ParseText(decl.OriginalBody);
    var root = tree.GetRoot();
    var names = new HashSet<string>(decl.InlinableParams.Select(p => p.Name));
    var nullable = decl.InlinableParams.Where(p => p.IsNullable).Select(p => p.Name).ToHashSet();

    // Collect aliases (transitive)
    bool changed = true;
    while (changed)
    {
        changed = false;
        foreach (var vd in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (vd.Initializer?.Value is IdentifierNameSyntax id
                && names.Contains(id.Identifier.Text)
                && !names.Contains(vd.Identifier.Text))
            {
                names.Add(vd.Identifier.Text);
                if (nullable.Contains(id.Identifier.Text)) nullable.Add(vd.Identifier.Text);
                changed = true;
            }
        }
    }

    var walker = new Walker(names, nullable);
    return walker.Visit(root)!.ToFullString();
}
```

- [ ] **Step 3: Run snapshot; expect PASS**

- [ ] **Step 4: Commit**

```
git add .
git commit -m "feat(inliner): body rewrite - track single-assignment var aliases"
```

### Task 33: Ternary alias

**Files:**
- Create: `tests/.../Inlinable/alias_ternary/{input,output}.cs`

- [ ] **Step 1: Write case** — `var chosen = cond ? action1 : action2;` with two `[Inlinable]` params.

(Note: two aliases of different inlinable params unify only if they share the same interface; if not, this is SPIRE021. For v1 support only when both branches alias the **same** inlinable param.)

- [ ] **Step 2: Extend alias collector** — also match `ConditionalExpressionSyntax` initializers where both branches are tracked aliases of the same root name.

- [ ] **Step 3: Run; commit**

### Task 34: Happy-path sweep

- [ ] **Step 1: Run all Inlinable snapshots**

Run: `dotnet test --filter "FullyQualifiedName~InlinableSnapshotTests"`
Expected: all PASS.

- [ ] **Step 2: Full build + test**

Run: `dotnet build && dotnet test`
Expected: no regressions.

- [ ] **Step 3: Commit any fixups**

---

## Phase 5 — Diagnostics analyzer

### Task 35: SPIRE022 — `[Inlinable]` on non-delegate parameter

**Files:**
- Create: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/Analyzers/InlinableUsageAnalyzer.cs`
- Create: `tests/Houtamelo.Spire.Analyzers.Tests/SPIRE022/SPIRE022Tests.cs` + cases

- [ ] **Step 1: Write analyzer skeleton**

```csharp
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InlinableUsageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        InlinerDescriptors.SPIRE021_UnsupportedBodyUsage,
        InlinerDescriptors.SPIRE022_NonDelegateParameter,
        InlinerDescriptors.SPIRE023_ContainerNotPartial,
        InlinerDescriptors.SPIRE024_DelegateArityExceeded,
        InlinerDescriptors.SPIRE025_UnsupportedRefKind,
        InlinerDescriptors.SPIRE026_PropertyOrIndexerParameter);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compCtx =>
        {
            var inlinableAttr = compCtx.Compilation.GetTypeByMetadataName("Houtamelo.Spire.InlinableAttribute");
            if (inlinableAttr is null) return;

            compCtx.RegisterSymbolAction(symCtx => AnalyzeMethod(symCtx, inlinableAttr), SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext ctx, INamedTypeSymbol inlinableAttr)
    {
        var method = (IMethodSymbol)ctx.Symbol;
        foreach (var p in method.Parameters)
        {
            var attr = p.GetAttributes().FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, inlinableAttr));
            if (attr is null) continue;

            var loc = p.Locations.FirstOrDefault() ?? Location.None;

            // SPIRE022 — non-delegate
            if (!IsActionOrFunc(p.Type, out _, out _))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    InlinerDescriptors.SPIRE022_NonDelegateParameter, loc, p.Name, p.Type.ToDisplayString()));
                continue;
            }
            // (other diagnostics in later tasks)
        }
    }

    private static bool IsActionOrFunc(ITypeSymbol t, out bool isFunc, out int arity)
    {
        isFunc = false; arity = 0;
        if (t is not INamedTypeSymbol n) return false;
        var def = n.OriginalDefinition.ToDisplayString();
        if (def == "System.Action" || def.StartsWith("System.Action<"))
        {
            isFunc = false;
            arity = n.TypeArguments.Length;
            return true;
        }
        if (def.StartsWith("System.Func<"))
        {
            isFunc = true;
            arity = n.TypeArguments.Length - 1;
            return true;
        }
        return false;
    }
}
```

- [ ] **Step 2: Test cases**

`SPIRE022/cases/nondelegate_string.cs` (should_fail), a plain-delegate should_pass.

- [ ] **Step 3: Run; commit**

```
git add .
git commit -m "feat(inliner): SPIRE022 for [Inlinable] on non-delegate"
```

### Task 36: SPIRE023 / SPIRE027 — non-partial containing type

- [ ] **Step 1: Extend analyzer** — walk `method.ContainingType` up to the outermost; for each, check `IsPartial` via `DeclaringSyntaxReferences` (as in Task 22).
- [ ] **Step 2: Write test cases** (both IDs: use SPIRE023 when the immediate containing type is non-partial, SPIRE027 when a more distant enclosing type is non-partial).
- [ ] **Step 3: Run; commit**

### Task 37: SPIRE024 — delegate arity > 8

- [ ] **Step 1: Extend analyzer** — reject when arity > 8.
- [ ] **Step 2: Tests** (should_fail with `Action<T1..T9>`).
- [ ] **Step 3: Run; commit**

### Task 38: SPIRE025 — ref-kind on `[Inlinable]` parameter

- [ ] **Step 1: Analyzer check** — `p.RefKind != RefKind.None`.
- [ ] **Step 2: Tests** (should_fail with `ref Action<int>`).
- [ ] **Step 3: Run; commit**

### Task 39: SPIRE026 — parameter on indexer/property accessor

- [ ] **Step 1: Analyzer check** — `method.MethodKind` is `PropertyGet/Set/Indexer*`.
- [ ] **Step 2: Tests**.
- [ ] **Step 3: Run; commit**

### Task 40: SPIRE021 — unsupported body usage

**This is the most complex diagnostic.** Cases to detect:

1. `p` passed to another method (that isn't the rewriter's `OtherMethod(p)` explicit pass-through — actually this is allowed per Q-K; _this case is NOT an error._ Only unsupported forms are.)
2. `p` stored into a field/property/array element.
3. `p` captured by a nested lambda or local function.
4. `p` assigned to a non-var-typed local (`Action<T> x = p;`).
5. Reassignment of an alias local.
6. `ref` local bound to `p`.

- [ ] **Step 1: Extend analyzer with a method-body walker**

For each `[Inlinable]` parameter, walk the method body and inspect every `IdentifierNameSyntax` referencing it. Classify context:

- Parent is `InvocationExpressionSyntax`.Expression → allowed (direct invocation).
- Parent is `ConditionalAccessExpressionSyntax`.Expression → allowed.
- Parent is `ArgumentSyntax` → allowed (pass-through to another method).
- Parent is `EqualsValueClauseSyntax` of a `VariableDeclaratorSyntax` with `var` declarator → allowed (alias init). Track alias for further walking.
- Parent is `BinaryExpressionSyntax` (==, !=) or `IsPatternExpressionSyntax` with null → allowed (null check).
- Anything else → SPIRE021.

Then recurse over aliases with the same rules. Reject alias if declarator is non-`var`, or if alias is reassigned (second `AssignmentExpressionSyntax.Left == alias`), or if alias appears in a nested `LambdaExpressionSyntax` / `LocalFunctionStatementSyntax`.

- [ ] **Step 2: Test cases** — cover each failure shape with its own `should_fail` case, plus a `should_pass` case that uses only the allowed forms.

- [ ] **Step 3: Run; commit**

---

## Phase 6 — Behavioral tests & benchmarks

### Task 41: Behavioral test — sanity equivalence

**Files:**
- Create: `tests/Houtamelo.Spire.BehavioralTests/Types/InlinerTargets.cs`
- Create: `tests/Houtamelo.Spire.BehavioralTests/Tests/InlinerTests.cs`

- [ ] **Step 1: Declare targets**

```csharp
using Houtamelo.Spire;

namespace Houtamelo.Spire.BehavioralTests.Types;

public static partial class InlinerTargets
{
    [InlinerStruct]
    public static int Double(int x) => x * 2;

    [InlinerStruct]
    public static void Accumulate(System.Collections.Generic.List<int> bucket, int x)
        => bucket.Add(x * 10);
}

public static partial class InlinerHosts
{
    public static int Apply<T, TF>(T seed, TF f) where TF : struct, IFuncInliner<T, T>
        => f.Invoke(seed);

    public static void ForEach<T>(
        System.Collections.Generic.List<T> list,
        [Inlinable] System.Action<T> action)
    {
        foreach (var item in list) action(item);
    }
}
```

- [ ] **Step 2: Write tests**

```csharp
using System.Collections.Generic;
using Houtamelo.Spire.BehavioralTests.Types;
using Xunit;

namespace Houtamelo.Spire.BehavioralTests.Tests;

public sealed class InlinerTests
{
    [Fact]
    public void InlinerStruct_ForwardsStaticCall()
    {
        var doubler = default(InlinerTargets.DoubleInliner);
        Assert.Equal(10, doubler.Invoke(5));
        Assert.Equal(14, InlinerHosts.Apply(7, doubler));
    }

    [Fact]
    public void InlinerStruct_ForwardsInstanceCall_ViaPositional()
    {
        var bucket = new List<int>();
        var acc = default(InlinerTargets.AccumulateInliner);
        acc.Invoke(bucket, 3);
        Assert.Equal(new[] { 30 }, bucket);
    }

    [Fact]
    public void InlinableTwin_RoutedByOverloadResolution()
    {
        var list = new List<int> { 1, 2, 3 };
        var sink = new List<int>();
        // The generator emits a twin; calling with a struct picks it.
        var accBound = new BoundAccumulator(sink);
        InlinerHosts.ForEach(list, accBound);
        Assert.Equal(new[] { 1, 2, 3 }, sink);
    }

    private readonly struct BoundAccumulator : IActionInliner<int>
    {
        private readonly List<int> _sink;
        public BoundAccumulator(List<int> sink) => _sink = sink;
        public void Invoke(int x) => _sink.Add(x);
    }
}
```

- [ ] **Step 3: Run; expect PASS** for all three tests.

- [ ] **Step 4: Commit**

```
git add tests/Houtamelo.Spire.BehavioralTests/
git commit -m "test(inliner): behavioral equivalence tests"
```

### Task 42: Benchmark

**Files:**
- Create: `benchmarks/Houtamelo.Spire.Benchmarks/Benchmarks/InlinerDispatch.cs`

- [ ] **Step 1: Implement benchmark**

```csharp
using BenchmarkDotNet.Attributes;
using Houtamelo.Spire;
using Houtamelo.Spire.Benchmarks.Helpers;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public partial class InlinerDispatch
{
    private readonly int[] _data = new int[BenchN.Default];
    private int _sink;

    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < _data.Length; i++) _data[i] = i;
    }

    [Benchmark(Baseline = true)]
    public int Direct()
    {
        int s = 0;
        foreach (var x in _data) s += x * 2;
        return _sink = s;
    }

    [Benchmark]
    public int DelegateCall()
    {
        int s = 0;
        System.Func<int, int> f = static x => x * 2;
        foreach (var x in _data) s += f(x);
        return _sink = s;
    }

    [Benchmark]
    public int InlinerStructCall()
    {
        int s = 0;
        var f = default(DoubleInliner);
        foreach (var x in _data) s += f.Invoke(x);
        return _sink = s;
    }

    [InlinerStruct]
    private static int Double(int x) => x * 2;
}
```

- [ ] **Step 2: Run smoke benchmark**

Run: `dotnet run -c Release --project benchmarks/Houtamelo.Spire.Benchmarks/ -- --filter '*InlinerDispatch*' --job Dry`
Expected: all three benchmarks complete; `InlinerStructCall` time ≈ `Direct`, `DelegateCall` noticeably slower.

- [ ] **Step 3: Commit**

```
git add benchmarks/Houtamelo.Spire.Benchmarks/Benchmarks/InlinerDispatch.cs
git commit -m "bench(inliner): compare direct / delegate / inliner-struct dispatch"
```

### Task 43: Move release entries to Shipped; bump version

**Files:**
- Modify: `src/Houtamelo.Spire.Analyzers/AnalyzerReleases.Shipped.md`
- Modify: `src/Houtamelo.Spire.Analyzers/AnalyzerReleases.Unshipped.md`
- Modify: `src/Houtamelo.Spire/Houtamelo.Spire.csproj` — bump `<Version>` to 4.6.0.
- Modify: `src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj` — bump `<Version>` to 4.6.0.
- Modify: `src/Houtamelo.Spire.CodeFixes/Houtamelo.Spire.CodeFixes.csproj` — bump `<Version>` to 4.6.0.

- [ ] **Step 1: Move SPIRE017–SPIRE027 from Unshipped.md to a new release block in Shipped.md** (follow existing release-block format; add a date line).
- [ ] **Step 2: Bump versions in all three `.csproj`.**
- [ ] **Step 3: Full build + test + pack**

Run: `dotnet build && dotnet test && dotnet pack -c Release src/Houtamelo.Spire/`
Expected: all green; `.nupkg` produced.

- [ ] **Step 4: Commit**

```
git add .
git commit -m "chore: release closure inliner generator, bump version to 4.6.0"
```

---

## Self-Review

After writing this plan I checked it against the spec:

**Spec coverage:**
- Interfaces set (18 types) → Tasks 1, 2. ✓
- Attributes (InlinerStruct, Inlinable) → Task 3. ✓
- Descriptors SPIRE017–SPIRE027 → Task 4. ✓
- [InlinerStruct] static/instance/generic/nested → Tasks 10–16. ✓
- [InlinerStruct] diagnostics (SPIRE017–SPIRE020, SPIRE027) → Tasks 18–22. ✓
- [Inlinable] emit rule → Tasks 25–26. ✓
- [Inlinable] body rewrite rules 1–7 → Tasks 27 (direct), 30 (nullable), 31 (non-null strip), 32–33 (aliases). ✓
- [Inlinable] diagnostics (SPIRE021–SPIRE027) → Tasks 35–40. ✓
- Behavioral tests → Task 41. ✓
- Benchmarks → Task 42. ✓
- Package layout (interfaces in Spire, generator in Performance subfolder) → Tasks 1–4 follow the spec layout. ✓
- Release tracking + version bump → Task 43. ✓

**Placeholders:** none found. Every task either has inline code or refers to already-described patterns by task number.

**Type consistency:** `InlinerStructDecl`, `InlinableHostDecl`, `InlinableParam`, `InlinerDiagnostic`, `LocationInfo`, `HostParamInfo`, `InlinerParamInfo` — all defined consistently across tasks. Emitter and parser field names align.

**Known risk:** Task 27's use of `CSharpSyntaxTree.ParseText(decl.OriginalBody)` reparses the body in isolation, losing the outer method's generic and parameter context. This is fine for textual rewriting (we only look at identifier names) but cannot resolve semantic meaning. If a future task needs semantic info (e.g., to validate that an alias is declared before use in flow order), use the original method's `SemanticModel` via `SyntaxReference.GetSyntax()` on the captured `IMethodSymbol` instead. Flagged in Task 40 where SPIRE021 analysis benefits from the full semantic model.

**Open item surfaced by self-review:** Task 30's handling of `p?(x)` syntax. The spec lists `p?(args)` as a form to be rewritten, but C# does not accept `identifier?(args)` as a valid conditional invocation — the conditional-access form is `identifier?.Invoke(args)`. This is a spec inconsistency. **Action:** during implementation of Task 30, verify with a syntax-tree dump and, if confirmed invalid, file a spec amendment removing `p?(args)` from the rewrite rules (the surrounding `p?.Invoke(args)` form covers the use case).
