# Closure Inliner Generator — Design

Status: draft, design approved through Q&A. Spec pending user review.
Date: 2026-04-14

## Motivation

The JIT devirtualizes and inlines through generic struct constraints (`where T : struct, IFoo`) but cannot inline through `Action`/`Func` delegates or `delegate*` function pointers. In hot paths (ECS system updates, per-frame loops, numeric kernels), this difference is measurable: zero-indirection monomorphization beats indirect calls on every iteration.

C# does not provide an ergonomic way to get this monomorphization. Users must hand-write a `readonly struct : IAction<T>` per callsite and a generic method that accepts it. This generator automates both halves.

## Non-goals

- **No capture lifting.** Inliner structs are stateless. State (including instance receiver) is passed positionally per call. Users who need captures keep using delegates.
- **No interceptors.** Explicitly rejected — users instantiate and pass the inliner struct manually.
- **No runtime codegen.** All structs and twin methods are emitted at compile time.
- **No AOT compatibility guarantees for mod-defined inliner structs** consuming a generic host in an AOT'd main assembly. JIT-hosted runtimes (desktop MonoGame, Godot, custom CoreCLR) work naturally.

## Terminology

- **Inliner struct** — a `readonly struct` that forwards calls to a concrete method through a per-arity interface.
- **Inliner interface** — `IActionInliner<...>` or `IFuncInliner<..., TReturn>`, parameterized by arity.
- **Twin method** — the generic struct-based overload emitted alongside a user method that has `[Inlinable]` parameters.
- **Host method** — any method that declares an `[Inlinable]` delegate parameter.

## Interface set

Lives in `Houtamelo.Spire` namespace. Arity cap: **N = 8**.

```csharp
public interface IActionInliner                                           { void Invoke(); }
public interface IActionInliner<T1>                                       { void Invoke(T1 a1); }
public interface IActionInliner<T1, T2>                                   { void Invoke(T1 a1, T2 a2); }
// ... through
public interface IActionInliner<T1, T2, T3, T4, T5, T6, T7, T8>           { void Invoke(T1 a1, ..., T8 a8); }

public interface IFuncInliner<TR>                                         { TR Invoke(); }
public interface IFuncInliner<T1, TR>                                     { TR Invoke(T1 a1); }
// ... through
public interface IFuncInliner<T1, T2, T3, T4, T5, T6, T7, T8, TR>         { TR Invoke(T1 a1, ..., T8 a8); }
```

Total: 9 + 9 = 18 interfaces. Emitted once in `Houtamelo.Spire`.

## Part 1 — `[InlinerStruct]`

Attribute lives in `Houtamelo.Spire`. Applied to a static or instance method.

### Emit rule

For method `M` in type `C`:

- Generated struct: `{M}Inliner` emitted as a **sibling of the method** — i.e., nested inside `C`, at the same lexical level as `M`. The generator emits a partial extension of `C` containing the struct.
- **Every type in the enclosing chain must be declared `partial`** (the declaring type `C` and any enclosing types). Violation → `SPIRE_IL011` error.
- Accessibility mirrors `M`.
- Generic type parameters: all of `C`'s open generics (if any) followed by all of `M`'s method generics, with constraints copied verbatim.
- Declared `readonly struct {M}Inliner[<...>] : I{Action|Func}Inliner<...>`.
- Invoke body:
  - **Static `M(A1, ..., AN)`** → `IActionInliner<A1, ..., AN>` / `IFuncInliner<A1, ..., AN, R>`; `Invoke(A1 a1, ..., AN aN) => M(a1, ..., aN);` (with `return` for Func).
  - **Instance `M(A1, ..., AN)` on `C`** → `IActionInliner<C, A1, ..., AN>` / `IFuncInliner<C, A1, ..., AN, R>`; `Invoke(C instance, A1 a1, ..., AN aN) => instance.M(a1, ..., aN);`.
- Struct has a single `public` parameterless default constructor (state-free).

### Arity shapes

| Source method | Interface |
|---|---|
| `static void M()` | `IActionInliner` |
| `static void M(A)` | `IActionInliner<A>` |
| `static void M(A, B, ...)` | `IActionInliner<A, B, ...>` |
| `static R M(A, B, ...)` | `IFuncInliner<A, B, ..., R>` |
| `void M()` on `C` | `IActionInliner<C>` |
| `void M(A, B, ...)` on `C` | `IActionInliner<C, A, B, ...>` |
| `R M(A, B, ...)` on `C` | `IFuncInliner<C, A, B, ..., R>` |

### Constraints

- **Parameter modifiers** (`ref`, `in`, `out`, `ref readonly`, `params`, `this` on non-extension methods) → `SPIRE_IL001` error.
- **Declaring type is `ref struct`** → `SPIRE_IL002` error.
- **Total arity > 8** (including prepended instance for non-static) → `SPIRE_IL003` error.
- **Generated struct name collides with an existing type** in the same namespace/nesting → `SPIRE_IL004` error.

Value-type declaring types are allowed; the instance is copied into the positional arg slot on each call. Documented as a v1 limitation.

## Part 2 — `[Inlinable]`

Attribute lives in `Houtamelo.Spire`. Applied to a parameter whose type is `Action<...>` or `Func<...>` (optionally nullable).

### Emit rule

For host method `H` declaring one or more `[Inlinable]` parameters, the generator emits a **twin method** in the same `partial` type as `H`:

- Same name as `H`.
- Original generic type parameters retained, with one additional `TInliner` per `[Inlinable]` parameter appended. Naming: `T{ParameterName}Inliner` (e.g., `closure` → `TClosureInliner`). If that collides with an existing generic, a numeric suffix is appended.
- Each appended generic has:
  - A `struct` constraint.
  - An interface constraint matching the delegate's arity:
    - `Action<T1, ..., Tn>` → `IActionInliner<T1, ..., Tn>`.
    - `Func<T1, ..., Tn, TR>` → `IFuncInliner<T1, ..., Tn, TR>`.
- Each `[Inlinable]` parameter's type is replaced:
  - **Non-nullable** `Action<...>` / `Func<...>` → `TInliner`.
  - **Nullable** `Action<...>?` / `Func<...>?` → `TInliner?` (i.e., `Nullable<TInliner>`).
- All of `H`'s generic constraints are preserved verbatim.
- Access modifier, return type, non-`[Inlinable]` parameters, ref-kind on non-`[Inlinable]` parameters, and XML docs are mirrored.
- Body of `H` is copied, then rewritten per the rules below.

### Body rewrite rules

For each `[Inlinable]` parameter `p` in the host method body:

1. **Direct invocation**: `p(args)` → `p.Invoke(args)`.
2. **Conditional invocation on non-nullable parameter**: `p?(args)`, `p?.Invoke(args)` → `p.Invoke(args)` (null check stripped; struct cannot be null).
3. **Conditional invocation on nullable parameter**: `p?(args)` → `p?.Invoke(args)`; `p?.Invoke(args)` → unchanged.
4. **Null comparison on nullable parameter**: `p == null`, `p != null`, `p is null`, `p is {}` → unchanged (works on `Nullable<T>`).
5. **Null comparison on non-nullable parameter**: left as-is; C# compile error surfaces.
6. **Aliasing via `var`**: `var a = p;` declares `a` with type `TInliner` (or `TInliner?`); any direct invocation or conditional invocation of `a` is rewritten per the same rules. Aliases propagate transitively.
7. **Passing to another method**: `OtherMethod(p)` / `OtherMethod(a)` — left as-is. If `OtherMethod` has a compatible generic twin, overload resolution selects it; otherwise a C# compile error surfaces to the user.

### Supported alias shapes

Aliases are tracked only in SSA-safe form:

- Local is declared with `var` (not explicit delegate type).
- Single assignment from `p` or another tracked alias at declaration.
- No reassignment after declaration.
- Not captured by a nested lambda or local function.
- Not stored into a field, property, array element, or `ref`/`ref readonly`/`in`/`out` destination.
- Not declared as a `ref` local.
- Ternary `cond ? p : q` counts as a tracked alias if **both** branches are tracked aliases of the same `[Inlinable]` parameter.

Any other shape → `SPIRE_IL005` error with the specific offending expression location.

### Constraints

- **`[Inlinable]` on a non-delegate parameter type** → `SPIRE_IL006` error.
- **Containing type (or any enclosing type) is not `partial`** → `SPIRE_IL007` error (functionally duplicates `SPIRE_IL011`; kept as a Part-2-specific alias for clearer reporting).
- **Delegate arity > 8** → `SPIRE_IL008` error.
- **Unsupported use of the parameter in the body** (see list above) → `SPIRE_IL005` error.
- **`[Inlinable]` on the same parameter as a disallowed ref-kind** (`ref`/`in`/`out`/`ref readonly`) → `SPIRE_IL009` error.
- **`[Inlinable]` on an expression-bodied property or indexer parameter** → v1 unsupported; `SPIRE_IL010` error.

## Diagnostics summary

| ID | Severity | Description |
|---|---|---|
| SPIRE_IL001 | Error | `[InlinerStruct]` method parameter has ref-kind (`ref`/`in`/`out`/`ref readonly`/`params`). |
| SPIRE_IL002 | Error | `[InlinerStruct]` method is declared on a `ref struct`. |
| SPIRE_IL003 | Error | `[InlinerStruct]` method arity exceeds 8 (instance methods include the declaring type). |
| SPIRE_IL004 | Error | Generated `{M}Inliner` struct name collides with an existing type. |
| SPIRE_IL005 | Error | `[Inlinable]` parameter used outside supported forms (aliasing/capture/storage/etc.). |
| SPIRE_IL006 | Error | `[Inlinable]` attribute applied to a non-delegate parameter. |
| SPIRE_IL007 | Error | Type containing an `[Inlinable]` parameter is not declared `partial`. |
| SPIRE_IL008 | Error | `[Inlinable]` delegate parameter arity exceeds 8. |
| SPIRE_IL009 | Error | `[Inlinable]` parameter has an unsupported ref-kind. |
| SPIRE_IL010 | Error | `[Inlinable]` applied on a property / indexer accessor parameter (v1 unsupported). |
| SPIRE_IL011 | Error | Declaring type (or any enclosing type) of an `[InlinerStruct]` or `[Inlinable]` method is not declared `partial`. |

## Package and project layout

- **Interfaces & attributes** → `src/Houtamelo.Spire/` (namespace `Houtamelo.Spire`).
  - New files: `IActionInliner.cs`, `IFuncInliner.cs`, `InlinerStructAttribute.cs`, `InlinableAttribute.cs`.
- **Generator + analyzer** → `src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/`.
  - New subfolders under it, following existing layout: `Emit/`, `Model/`, `Parsing/`, `Analyzers/`.
  - Generator classes: `InlinerStructGenerator`, `InlinableTwinGenerator` (or combined into one).
  - Body-rewrite logic (alias tracking, invocation rewriting) lives in `Emit/InlinableBodyRewriter.cs`.
- **Tests**:
  - Snapshot tests for generated output: `tests/Houtamelo.Spire.SourceGenerators.Tests/cases/Performance/...`.
  - Behavioral tests: `tests/Houtamelo.Spire.BehavioralTests/` — verify runtime equivalence between delegate call and twin call, and measure elimination of per-call allocations.
  - Benchmarks: `benchmarks/Houtamelo.Spire.Benchmarks/Benchmarks/InlinerDispatch.cs` — show delegate vs `delegate*<...>` vs inliner-struct vs direct call.

## Emit examples

### `[InlinerStruct]` — static, void

User:
```csharp
namespace Game;

public static partial class Systems
{
    [InlinerStruct]
    public static void Log<T>(T value) => Console.WriteLine(value);
}
```

Generated (nested inside `Systems`):
```csharp
namespace Game;

public static partial class Systems
{
    public readonly struct LogInliner<T> : IActionInliner<T>
    {
        public void Invoke(T a1) => Log(a1);
    }
}
```

### `[InlinerStruct]` — instance, non-void

User:
```csharp
namespace Game;

public partial class Transform
{
    [InlinerStruct]
    public Vector3 Scale(Vector3 v, float factor) => v * factor;
}
```

Generated (nested inside `Transform`):
```csharp
namespace Game;

public partial class Transform
{
    public readonly struct ScaleInliner : IFuncInliner<Transform, Vector3, float, Vector3>
    {
        public Vector3 Invoke(Transform instance, Vector3 a1, float a2) => instance.Scale(a1, a2);
    }
}
```

### `[InlinerStruct]` on a method of a nested type

User:
```csharp
namespace Game;

public partial class Outer
{
    public partial class Inner
    {
        [InlinerStruct]
        public static int Double(int x) => x * 2;
    }
}
```

Generated (nested inside `Inner`; `Outer` and `Inner` must both be `partial`):
```csharp
namespace Game;

public partial class Outer
{
    public partial class Inner
    {
        public readonly struct DoubleInliner : IFuncInliner<int, int>
        {
            public int Invoke(int a1) => Double(a1);
        }
    }
}
```

### `[Inlinable]` — single parameter, non-nullable

User:
```csharp
namespace Game;

public static partial class ListExt
{
    public static void ForEach<T>(this List<T> list, [Inlinable] Action<T> action)
    {
        foreach (var item in list)
            action(item);
    }
}
```

Generated (twin, added as a sibling method):
```csharp
namespace Game;

public static partial class ListExt
{
    public static void ForEach<T, TActionInliner>(this List<T> list, TActionInliner action)
        where TActionInliner : struct, IActionInliner<T>
    {
        foreach (var item in list)
            action.Invoke(item);
    }
}
```

### `[Inlinable]` — nullable + alias

User:
```csharp
public static partial class ListExt
{
    public static void ForEachMaybe<T>(this List<T> list, [Inlinable] Action<T>? action)
    {
        var a = action;
        foreach (var item in list)
            a?.Invoke(item);
    }
}
```

Generated:
```csharp
public static partial class ListExt
{
    public static void ForEachMaybe<T, TActionInliner>(this List<T> list, TActionInliner? action)
        where TActionInliner : struct, IActionInliner<T>
    {
        var a = action;
        foreach (var item in list)
            a?.Invoke(item);
    }
}
```

### Multiple `[Inlinable]` parameters

User:
```csharp
public static partial class Pipeline
{
    public static R Run<T, R>(T seed, [Inlinable] Func<T, T> pre, [Inlinable] Func<T, R> post)
        => post(pre(seed));
}
```

Generated:
```csharp
public static partial class Pipeline
{
    public static R Run<T, R, TPreInliner, TPostInliner>(T seed, TPreInliner pre, TPostInliner post)
        where TPreInliner  : struct, IFuncInliner<T, T>
        where TPostInliner : struct, IFuncInliner<T, R>
        => post.Invoke(pre.Invoke(seed));
}
```

### Calling site

```csharp
// After user adds [InlinerStruct] to `Log` and [Inlinable] to ForEach's action:
Systems.LogInliner<int> printer = default;
list.ForEach(printer);  // overload resolution picks the generic twin; monomorphized, inlined
```

## Future work (out of scope for v1)

- Ref/in/out parameter modifiers on `[InlinerStruct]` methods via dedicated ref-shape interface families.
- `ref struct` declaring types via `allows ref struct` generic constraints.
- Capture-lifting (i.e., non-stateless inliner structs).
- Source-level `[Inlinable]` lambda literals (generator-side lambda→struct without a named source method).
- Suggestion analyzer flagging hot delegate calls where `[Inlinable]` would apply.
- Automatic fallback when the generator cannot rewrite a body: emit the original method only, plus a `SPIRE_IL100` info diagnostic.

## Open items before implementation

None — all design decisions pinned through Q&A.
