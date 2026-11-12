# Houtamelo.Spire.Functional

Functional operations for Spire results and nullable value types. Targets .NET
Standard 2.0 and depends on `Houtamelo.Spire`, which NuGet installs automatically.

```sh
dotnet add package Houtamelo.Spire.Functional
```

```csharp
using Houtamelo.Spire.Functional;

Result<int> result = Result<int>.Ok(21);
Result<int> doubled = result.map(value => value * 2);

int? optional = true.then(() => 42);
string label = optional.map_or(value => value.ToString(), "missing");
```

`Result<T>` contains either a success value or an `Exception`; `Result` represents
success without a payload or an exception. Mapping and chaining preserve errors
without invoking success callbacks. `unwrap()` throws the stored exception while
preserving its stack trace; `expect(message)` wraps it as `InnerException` and
adds context. Exceptions thrown by callbacks propagate to the caller.

`OptionImpl` provides `Some`, `None`, mapping, filtering, lazy fallbacks, and
conversion to results for `Nullable<T>`. The `then` extension computes a value
only when its Boolean receiver is true. Reference-output overloads use `null`
to represent absence.

Collection helpers include `try_collect`, `try_fold`, `try_map`, `try_for_each`,
`iter_oks`, `iter_errs`, `unzip`, and `flatten`. The `try_` sequence operations stop
at the first failed result; callbacks and enumeration exceptions still propagate.

The Spire Unity distribution includes this library and its XML API documentation
alongside the core runtime and compiler extensions.
