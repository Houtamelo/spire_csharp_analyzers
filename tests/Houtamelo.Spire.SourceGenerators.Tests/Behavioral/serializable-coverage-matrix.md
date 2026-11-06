# Generated Payload `[Serializable]` Coverage Matrix

This matrix covers the pending emitter issue in `docs/PENDING.md`: generated
variant payload structs must carry `System.SerializableAttribute` only when the
user's union declaration carries the real `System.SerializableAttribute`.
`EmitUnitySerialization` controls engine-dependent callbacks, wrappers, and
drawer output; it is not the source of the semantic opt-in. A `[Serializable]`
union must therefore retain serializable payload metadata even when Unity
references are unavailable, while an unannotated union must not gain it merely
because the generator emits payload structs.

Use the real generator-driver harness and inspect output symbols, generated
source, and compilation diagnostics. Existing positive fixtures include
`Unity/fixtures/Serializable_BasicStruct.cs.txt` and
`Unity/fixtures/Serializable_AliasAttribute.cs.txt`; the unannotated control is
`Unity/fixtures/NormalAdditive.cs.txt`. Update expected generated snapshots only
where the input union is unannotated; positive serializable fixtures must retain
their attributes.

---

## Category A: Attribute presence follows the user union marker

### should_fail

None; these are symbol/source contract checks rather than diagnostics.

### should_pass

| Test Name | Description |
|-----------|-------------|
| `UnannotatedUnion_FieldedAndFieldlessPayloadsAreNotSerializable` | Generate an ordinary mutable struct union containing both `Value(int value)` and `Empty()`, with Unity references present. Neither `Types.Variants.Value` nor `Types.Variants.Empty` has `System.SerializableAttribute`; no Unity serialization wrapper/callback surface is emitted. |
| `SerializableUnion_FieldedAndFieldlessPayloadsAreSerializable` | Generate `[Serializable, DiscriminatedUnion]` with the same fielded and fieldless variants. Both canonical payload structs have `System.SerializableAttribute`, and the Unity wrapper/callback surface remains available. |
| `SerializableUnion_WithoutUnityReferencesKeepsPayloadMarker` | Generate `[Serializable, DiscriminatedUnion]` without Unity references. `SPIRE_DU016` is reported and engine callbacks are unavailable, but canonical payload structs still have `System.SerializableAttribute` because the source marker is present. |

## Category B: Generic, nested, and former-name payloads

### should_fail

None; generic and alias cases must preserve compilation while checking symbol
metadata.

### should_pass

| Test Name | Description |
|-----------|-------------|
| `SerializableGenericAndNestedUnions_PreservePayloadMarker` | A serializable namespace-level generic union and a serializable union nested in a non-generic containing type both propagate `System.SerializableAttribute` to every canonical payload, including fieldless payloads, while preserving type constraints and nested metadata names. |
| `SerializableFormerlySerializedAs_CanonicalAndAliasPayloadsAreSerializable` | A serializable `Current` variant with `FormerlySerializedAs = "Old"` emits both canonical and former-name payload structs with the serializable marker; the former name is not treated as an additional runtime variant or public conversion. |
| `UnannotatedFormerlySerializedAs_DoesNotOptInSerialization` | An otherwise unannotated union with a former-name alias emits neither canonical nor alias payload `[Serializable]` metadata and does not create Unity callbacks merely because alias migration metadata is present. |
| `SerializableAliasAttribute_IsSemantic` | A source alias to `System.SerializableAttribute` (for example `SerializableAlias`) is recognized exactly like `[Serializable]`; payload markers and Unity output match the direct-marker fixture. |
| `ForeignSerializableAlias_DoesNotFalsePositive` | A source alias named `Serializable` or `SerializableAlias` that resolves to a user-defined attribute in another namespace does not opt the union into payload `[Serializable]` metadata or Unity serialization. |

## Regression and snapshot checks

### should_fail

None.

### should_pass

| Test Name | Description |
|-----------|-------------|
| `GeneratedPayloadSerializableAttribute_IsNotUnconditionallyEmitted` | Across unannotated fielded, fieldless, generic, nested, and alias controls, inspect every generated payload symbol/source and assert no unconditional `[Serializable]` line remains. The test must distinguish payload attributes from an unused generated alias declaration. |
| `SerializablePayloadSnapshots_ChangeOnlyByOptIn` | Re-run affected generator/snapshot fixtures. Unannotated inputs such as `NormalAdditive` lose payload `[Serializable]` lines; `Serializable_BasicStruct`, `Serializable_AliasAttribute`, generic, nested, and `FormerlySerializedAs` positive inputs retain them. No unrelated generated member, callback, or field policy changes. |

The implementation condition should use the model's semantic
`IsUnitySerializable` marker (the actual user `System.SerializableAttribute`),
not `EmitUnitySerialization` (the optional Unity-reference capability). The
tests must keep those two dimensions separate.
