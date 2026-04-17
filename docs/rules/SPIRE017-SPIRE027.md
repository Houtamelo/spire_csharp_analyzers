# SPIRE017–SPIRE027: Closure Inliner Family

Source-generator and analyzer rules for `[InlinerStruct]` and `[Inlinable]` — the closure inliner feature that replaces delegate indirection with JIT-monomorphizable struct dispatch.

## Feature in one paragraph

`[InlinerStruct]` on a method emits a sibling `readonly struct` implementing `IActionInliner<...>` or `IFuncInliner<..., TR>` that forwards calls to the attributed method. `[Inlinable]` on a delegate-typed parameter emits a twin overload of the containing method where the parameter is replaced by a generic struct constrained to the matching inliner interface; direct invocations are rewritten to `.Invoke(...)`. Consumers pass the generated struct (or any hand-written inliner) and get monomorphization instead of delegate indirection.

See `docs/superpowers/specs/2026-04-14-closure-inliner-generator-design.md` for the full design.

## Rule summary

| ID | Category | Severity | Trigger |
|---|---|---|---|
| SPIRE017 | SourceGeneration | Error | `[InlinerStruct]` method parameter uses `ref`/`in`/`out`/`ref readonly`/`params`. |
| SPIRE018 | SourceGeneration | Error | `[InlinerStruct]` method declared on a `ref struct`. |
| SPIRE019 | SourceGeneration | Error | `[InlinerStruct]` total arity (including the instance for non-static methods) exceeds 8. |
| SPIRE020 | SourceGeneration | Error | Generated `{MethodName}Inliner` struct name collides with an existing member of the declaring type. |
| SPIRE021 | Correctness | Warning | `[Inlinable]` parameter used outside direct invocation, null-check, single-assignment `var` alias, or method pass-through. Silently defeats the monomorphization benefit. |
| SPIRE022 | Correctness | Error | `[Inlinable]` applied to a parameter whose type is not `System.Action`/`Action<...>`/`Func<...>`. |
| SPIRE023 | SourceGeneration | Error | Containing type of an `[Inlinable]` method is not declared `partial`. |
| SPIRE024 | SourceGeneration | Error | `[Inlinable]` delegate arity exceeds 8. |
| SPIRE025 | Correctness | Error | `[Inlinable]` parameter has `ref`/`in`/`out`/`ref readonly`. |
| SPIRE026 | Correctness | Error | `[Inlinable]` applied to an indexer or property accessor parameter. |
| SPIRE027 | SourceGeneration | Error | Declaring type (or any enclosing type) of an `[InlinerStruct]` method is not `partial`. |

## When to suppress

- **SPIRE021** (only rule that is Warning severity): suppress locally when you intentionally want a delegate-shaped API that captures an `[Inlinable]` parameter into a lambda or stores it in a field. The cleaner alternative is to drop `[Inlinable]` entirely and accept the delegate cost — the method will still compile and run, just without monomorphization.

  ```csharp
  #pragma warning disable SPIRE021
  return x => action(x); // intentional delegate wrap
  #pragma warning restore SPIRE021
  ```

- **All other IDs are Error**: they either block compilation of the generated twin (SPIRE022–026) or describe an invalid `[InlinerStruct]` setup that cannot produce a working sibling struct (SPIRE017–020, 023, 027). Fix the offending source rather than suppress.

## Minimal examples

### `[InlinerStruct]`

```csharp
public static partial class Math2
{
    [InlinerStruct]
    public static int Double(int x) => x * 2;
}
// Generated:
//   public readonly struct DoubleInliner : global::Houtamelo.Spire.IFuncInliner<int, int>
//   {
//       public int Invoke(int a1) => Math2.Double(a1);
//   }
```

### `[Inlinable]`

```csharp
public static partial class ListExt
{
    public static void ForEach<T>(this List<T> list, [Inlinable] Action<T> action)
    {
        foreach (var item in list) action(item);
    }
}
// Generated twin:
//   public static void ForEach<T, TActionInliner>(this List<T> list, TActionInliner action)
//       where TActionInliner : struct, global::Houtamelo.Spire.IActionInliner<T>
//   {
//       foreach (var item in list) action.Invoke(item);
//   }
```
