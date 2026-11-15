# SPIRE030 Test Coverage Matrix

The tests use the real Unity-reference generator harness (`UnityGeneratorTestHarness`) and run
`RecordVariantDeclarationAnalyzer` on the generated compilation. Unity-dependent tests must carry
`[Trait("Environment", "Unity")]`; a portable/CoreCLR test must not accidentally rely on Unity
metadata. The warning is expected only for a public nested readonly `record struct` variant with
at least one positional parameter inside a mutable, non-ref `[Serializable, DiscriminatedUnion]`
struct union while Unity serialization references are present.

The direct behavioral tests should assert generator and C# compilation success before inspecting
analyzer diagnostics. For each SPIRE030 detection, assert `DiagnosticSeverity.Warning`, the variant
and union names in the message, and a source location on the authored `readonly` keyword. The
diagnostic should be reported once per variant, even if the record is split across partial parts.

## Detection and diagnostic shape

### should_fail

| Test name | Declaration / assertion |
|---|---|
| `ReadonlyPartialValue` | Mutable `[Serializable, DiscriminatedUnion]` union containing `[Variant] public readonly partial record struct Value(int amount);`; exactly one SPIRE030 on `readonly`. |
| `ReadonlyNonPartialValue` | Same union shape with non-partial `public readonly record struct Value(int amount);`; exactly one SPIRE030. This confirms the warning is about the serialized shape, not the generator's ability to add attributes. |
| `ReadonlyFullyAnnotatedValue` | Partial readonly record with `[Serializable]` and `[field: SerializeField]` on every positional member; still exactly one SPIRE030 and no SPIRE028 for `Value`. |
| `ReadonlyManagedReferenceValue` | Readonly record with a reference positional member marked `[field: SerializeReference]` (or `[UnitySerializeReference]`); SPIRE030 still reports because readonly applies to the backing field regardless of managed-reference metadata. |
| `ReadonlyMultipleValues` | One union with two readonly fielded record variants; exactly one SPIRE030 per variant, with locations on each authored modifier and no duplicate diagnostics. |
| `ReadonlyGenericOuterValue` | `[Serializable, DiscriminatedUnion] public partial struct State<T>` containing `public readonly record struct Value(T payload);`; one SPIRE030 and no generator/compiler errors. |
| `ReadonlyMixedVariants` | Union containing one readonly fielded record variant and one mutable record variant; SPIRE030 only for the readonly variant. The mutable sibling may receive SPIRE028 when its own attributes are missing, but no SPIRE030. |
| `ReadonlyWithReconstructionCustomization` | Readonly record has an authored positional property/field that triggers SPIRE029; assert both SPIRE030 and SPIRE029 are retained, with distinct locations and messages. |

### should_pass / diagnostic assertions

| Test name | Declaration / assertion |
|---|---|
| `ReadonlyFieldlessRecord` | `[Variant] public readonly record struct Empty;`; no SPIRE030. A fieldless payload has no positional data to lose. |
| `ReadonlyFieldlessRecordWithEmptyParameterList` | `[Variant] public readonly record struct Empty();`; no SPIRE030. This is distinct from the semicolon form because both syntax forms are supported. |
| `ReadonlyOuterUnion` | `[Serializable, DiscriminatedUnion] public readonly partial struct State` with a readonly fielded record variant; no SPIRE030. The union already receives SPIRE_DU013 and must not produce the nested warning. |
| `ReadonlyRefOuterUnion` | `[Serializable, DiscriminatedUnion] public ref partial struct State` with a readonly fielded record variant; no SPIRE030. The outer ref union is outside Unity serialization mode. |

## Scope and semantic exemptions

### should_pass

| Test name | Declaration / assertion |
|---|---|
| `NonSerializableUnion` | Mutable `[DiscriminatedUnion]` union without `[Serializable]` containing a readonly fielded record variant; no SPIRE030 and no SPIRE028. Runtime generation remains valid. |
| `CoreClrReadonlyVariant` | Same source compiled with normal CoreCLR references and no Unity references/preprocessor symbols; no SPIRE030. This protects against coupling the analyzer to C# `readonly` alone. |
| `MutableRecordVariant` | Serializable Unity union containing `[Variant] public record struct Value(int amount);`; no SPIRE030. Existing SPIRE028 behavior is tested independently. |
| `ReadonlyNonVariantRecord` | Serializable union containing a readonly nested record struct without `[Variant]`; no SPIRE030. Only union variants are in scope. |
| `SemanticAttributeLookalikes` | Define source-local attributes named `SerializableAttribute`, `DiscriminatedUnionAttribute`, or `VariantAttribute` in another namespace and use them in a lookalike declaration; no SPIRE030. Attribute identity must be semantic, not based on spelling. |
| `FieldlessMixedUnion` | Union containing both readonly fieldless and mutable fielded variants; no SPIRE030 for the fieldless readonly variant and no accidental diagnostic count increase. |
| `ReadonlyVariantInUnsupportedUnion` | Readonly fielded variant inside a union declaration that already fails another unsupported-shape diagnostic (for example, an unsupported record/class outer declaration); no SPIRE030 cascade. |

## Code-fix and post-fix behavior

The dedicated SPIRE030 fix is `Remove readonly from variant`. These are workspace tests over real
Roslyn `CodeAction`s; they should apply the action, compare all changed documents, rerun the
generator/analyzer, and compile/emit the resulting assembly.

### before / after cases

| Test name | Expected fix behavior |
|---|---|
| `RemoveReadonlyBasic` | Single-document `public readonly partial record struct Value(int amount);` becomes `public partial record struct Value(int amount);`; attributes, parameter list, and trivia remain unchanged. The action is offered for SPIRE030 only. |
| `RemoveReadonlyNonPartial` | Non-partial readonly record becomes mutable. After the fix, SPIRE030 disappears; SPIRE028 may remain if `[Serializable]`/field attributes are still missing. The fix must not silently add serialization attributes. |
| `RemoveReadonlyMultiPart` | The same record symbol is declared readonly in two authored partial declarations across documents, with the positional list in one part; one action removes `readonly` from every authored part and leaves the unrelated sibling type unchanged. |
| `RemoveReadonlyPreservesTrivia` | Modifier has comments/directives and unusual modifier ordering; remove only the `readonly` token and preserve comments, attributes, accessibility, `partial`, and line endings. |
| `RemoveReadonlyMixedUnion` | Union has readonly and mutable variants; apply the fix to the readonly diagnostic and verify only that variant changes. The mutable sibling declaration and any generated declarations remain byte-for-byte untouched. |
| `RemoveReadonlyFullyAnnotated` | Fully annotated readonly variant still offers the fix despite no SPIRE028. After applying it, SPIRE030 disappears and the existing serialization annotations remain. |
| `RemoveReadonlyNoActionForOtherDiagnostic` | Invoke the provider for SPIRE028, SPIRE029, SPIRE_DU013, or a stale diagnostic span; no SPIRE030 action is registered and the source is unchanged. |
| `RemoveReadonlyCompilesAndEmits` | After applying the fix to a fielded variant, rerun generation and emit/load the assembly; the payload conversion/reconstruction path still works and no C# errors are introduced. |

## Coverage notes

- A readonly record variant with positional data is warned even when its generated or authored
  fields carry `[SerializeField]`/`[SerializeReference]`; those attributes cannot make Unity serialize
  readonly fields.
- SPIRE028 should be suppressed only for the affected readonly variant. Other variants in the same
  union continue to receive their normal SPIRE028 diagnostics.
- SPIRE029 is orthogonal and must remain reportable alongside SPIRE030.
- No native Unity asset round-trip is claimed by these analyzer/code-fix tests; this rule explains
  the documented Unity limitation and preserves runtime generation behavior.
