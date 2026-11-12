# Results and options

The Unity bundle includes `Houtamelo.Spire.Functional.dll` and its XML API
documentation in `Runtime`. Keep it beside `Houtamelo.Spire.dll`. Both runtime
DLLs are enabled for the Editor and players.

```csharp
using Houtamelo.Spire.Functional;

Result<int> result = Result<int>.Ok(21);
Result<int> doubled = result.map(value => value * 2);

int? optional = true.then(() => 42);
string label = optional.map_or(value => value.ToString(), "missing");
```

`Result<T>` holds a value or an `Exception`. `Result` represents completion
without a success payload. Use the `Ok` and `Err` factories, then map, chain, or
inspect the selected variant. Success callbacks are skipped for errors.
`unwrap()` throws the stored exception and preserves its stack trace;
`expect(message)` adds context through a new exception whose `InnerException`
is the original error. Callback exceptions propagate normally.

`OptionImpl.Some(value)` and `OptionImpl.None<T>()` use `Nullable<T>` for value
types. Mapping, filtering, and fallback extensions preserve absence. Reference
outputs use `null` for absence. The `then` Boolean extension computes its value
only when the condition is true; `then_some` accepts an already evaluated value.

Collection operations include `try_collect`, `try_fold`, `try_map`,
`try_for_each`, `iter_oks`, `iter_errs`, `unzip`, and `flatten`. The `try_`
operations stop on the first failed result. Exceptions thrown during enumeration
or by callbacks are not converted into failed results.

Assemblies with **Override References** enabled must explicitly include both
runtime DLLs when using Functional APIs, even if their result types are defined
in another assembly. The compiler extensions remain separate analyzer plugins.
