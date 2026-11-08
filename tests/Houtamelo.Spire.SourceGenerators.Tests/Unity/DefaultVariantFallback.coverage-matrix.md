# `__SpireCreateDefault(int)` default-variant fallback coverage

This is a focused bridge regression matrix. A valid discriminator selects the
corresponding declared factory. Every integer that is not an exact declared
discriminator returns the **first declared variant**, passing `default(T)!` for
each first-variant payload field. The fallback never returns a default union
with an invalid `Kind` value.

Implement the cases in one focused
`tests/Houtamelo.Spire.SourceGenerators.Tests/Unity/UnityDefaultVariantFallbackTests.cs`
using `UnityGeneratorTestHarness.Run` and `EmitAndLoad`, as the existing Unity
state/concrete-wrapper tests do. Invoke `__SpireCreateDefault(int)` through
reflection, inspect `kind` and the generated payload accessor, and assert both
generator compilation and invocation success before checking values. Do not use
source-text matching as the behavior test.

## Category A: valid factory dispatch and first payload defaults

### should_pass

| File Name | Description |
|---|---|
| `CreateDefault_ValidFirstSecondAndFieldlessVariantsUseFactories` | A three-variant union declares `First(int value, string text)`, `Second(float amount)`, and `Empty()`. Calls with `0`, `1`, and `2` return the matching kinds; the first and second payloads contain CLR defaults (`0`/`null`/`0f`), proving the helper invokes factories rather than returning uninitialized storage. |

## Category B: invalid integers cannot exploit byte wrapping

### should_pass

| File Name | Description |
|---|---|
| `CreateDefault_InvalidNegativeAndLargeInputsFallBackToFirst` | On the same union, calls with `-1`, `-255`, `256`, `257`, `int.MinValue`, and `int.MaxValue` all return `Kind.First` with `value == 0` and `text == null`. `257` and `-255` are mandatory: an unchecked cast to the byte-backed `Kind` would select the second variant, so this proves validation occurs before enum conversion. |

## Category C: fieldless first variant is the fallback target

### should_pass

| File Name | Description |
|---|---|
| `CreateDefault_FieldlessFirstVariant_InvalidInputsReturnEmpty` | With `Empty()` declared first and a fielded variant declared second, valid `0` and invalid `-255`/`257` all return `Kind.Empty`, with no fabricated later payload and no invalid enum value. This is distinct from `__SpireFromSerialized(null)`: it exercises integer dispatch directly. |

## Category D: generic and nested generated declarations

### should_pass

| File Name | Description |
|---|---|
| `CreateDefault_ClosedGenericAndNestedUnionsPreserveFallback` | In one emitted assembly, close `GenericCommand<T>` as `int` and define nested `Outer.Command`; each has a fielded first variant before a second variant. `__SpireCreateDefault(257)` and `__SpireCreateDefault(-255)` resolve on the correct constructed/nested type, return its first kind, and initialize `int`/`float` payload fields to defaults. Valid discriminator `1` still selects each second variant. |

## Category E: existing serialized recovery remains aligned

### should_pass

| File Name | Description |
|---|---|
| `CreateDefault_DoesNotChangePayloadAuthoritativeFallback` | Keep the existing `__SpireFromSerialized(null)`, missing-wrapper, and unknown-wrapper checks. They still return the first variant/default payload and preserve the established warning/canonicalization path; changing `__SpireCreateDefault` must not alter payload-authoritative deserialization. |

### should_fail

| File Name | Description |
|---|---|
| `CreateDefault_ZeroVariantUnion_RetainsExistingDiagnostic` | A no-variant declaration keeps its existing generator diagnostic and does not require inventing a first-variant runtime fallback. |

## Native and snapshot boundaries

The Roslyn compile/emit/load cases above are the primary gate and should execute
in the normal generator test project and Unity Mono test host. No new Unity asset
fixture is needed: existing native checks in
`tests/UnityVariantPayloadCore/Assets/Runtime/SerializedUnionPlayerProbe.cs`
(`CheckDirectInvalidPayloadFallback`) and
`tests/UnityVariantPayloadCore/Assets/Editor/SerializedUnionEditorTests.cs`
(`AssertFallbackAsset`) already prove first-variant/default-field recovery for
`__SpireFromSerialized` and malformed assets. If the changed helper is not
exercised by the native drawer path, add only direct calls with `257` and `-255`
to those existing Player/Editor checks, asserting the first kind and defaults;
do not create a parallel project or duplicate the YAML recovery matrix.

The method is present in Unity Q09 compatibility output. After implementation,
update only affected Unity player/editor files under
`tests/Houtamelo.Spire.SourceGenerators.Tests/Compatibility/fixturedata/outputs/`
and their `manifest.json` hashes. Snapshots lock generated shape; the runtime
test above is authoritative for the wrapping regression.
