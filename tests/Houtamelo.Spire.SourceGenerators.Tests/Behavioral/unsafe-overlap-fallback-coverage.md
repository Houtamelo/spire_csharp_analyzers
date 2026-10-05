# UnsafeOverlap fallback regressions

Tests live in `UnsafeOverlapFallbackTests.cs`. They run the real discriminated
union generator against .NET 6 or .NET Core 3.1 references, with C# 10 options
on both input parsing and the generator driver. Before generation, the harness
verifies that `InlineArrayAttribute` is absent and `Unsafe` is available.
It checks diagnostics, emits the generated compilation, loads the assembly in
the .NET 10 test host, and exercises the public API. It never substitutes the
generator's capability flag or adds the host's BCL references to the old target.

| Test | Coverage |
|------|----------|
| `FallbackCompilation_UsesOldReferences` | Fallback source compiles and emits without an InlineArray declaration. |
| `SmallestBuffer_ByteRoundTripAndDefault` | One-byte payload round trip; fieldless construction zeros the unused payload. |
| `GetOnlyFallback_NetCoreApp31_CompilesAndRuns` | An inaccessible referenced IsExternalInit shim does not enable init setters; factories, getter-only properties, and deconstruct compile and execute. |
| `InitFallback_NetCoreApp31_LocalShim_CompilesAndRuns` | A real local internal shim enables init setters and C# 10 with expressions; numeric and managed copy updates execute correctly. |
| `OffsetZeroAndNonzeroFields_RoundTrip` | Distinct int, long, and double values; buffer larger than eight bytes; unaligned fields crossing eight-byte boundaries; object deconstruct reads at zero and nonzero offsets. |
| `FieldlessFactory_HasKindAndNullObjectPayload` | Fieldless variant inside a buffered union returns its kind and null object payload. |
| `Deconstruct_UsesTypedAndObjectOverloads` | Typed multi-field output, boxed numeric/reference output, fieldless and mismatched-arity null outputs. |
| `ManagedOnlyFields_PreserveValuesAndNull` | Managed-only union needs no buffer; reference identity, nulls, and object deconstruct remain correct. |
| `MixedFields_KeepManagedAndUnmanagedStorageSeparate` | Numeric buffer operations preserve separate managed slots. |
| `ValueCopy_InitPropertiesDoNotAliasFallbackStorage` | C# 10 struct `with` expressions update generated init properties at zero/nonzero offsets and managed slots while preserving the original value. |
| `EntirelyFieldlessUnion_GeneratesWithoutBufferStorage` | No buffer or managed slots required. |
| `UnsafeDisabled_ReportsSPIRE_DU009` | Unsafe-disabled compilation reports the existing diagnostic and emits no union implementation. |
| `ModernCompilation_StillUsesInlineArray` | Modern target still generates InlineArray code that compiles, emits, and loads. |

Existing UnsafeOverlap snapshots and behavioral tests additionally protect the
modern path. Readonly, generic/ref-struct, JSON, and Unity runtime/IL2CPP behavior
are outside this fallback regression's scope. Private storage field names are
not test contracts.

Reproduce with:

```bash
NUGET_HTTP_CACHE_PATH="$PWD/tmp/nuget-http-cache" dotnet test \
  tests/Houtamelo.Spire.SourceGenerators.Tests/ --no-restore \
  --filter 'FullyQualifiedName~UnsafeOverlapFallbackTests'
```

Before the fix, the original ten-test suite had six failures and four passing
controls. Failing target compilations reported missing `_data` references
(`CS1061`, `CS0103`) and constructor initialization failures (`CS0188`).
The subsequently added one-byte case reproduced the same errors.

The .NET Core 3.1 case additionally exposed `CS0518`: metadata lookup saw
Spire's internal PolySharp `IsExternalInit` shim and enabled inaccessible init
setters. Both generator entry points now check accessibility to the consumer.
