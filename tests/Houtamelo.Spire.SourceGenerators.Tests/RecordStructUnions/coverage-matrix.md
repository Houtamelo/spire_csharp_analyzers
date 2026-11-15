# SPIRE_DU020 test coverage matrix

`SPIRE_DU020` is a generator diagnostic. The generator must report an error on
the authored `record` keyword of a `[DiscriminatedUnion] record struct` and
must not emit the union implementation for that declaration. The diagnostic is
independent of Unity, JSON settings, readonly modifiers, generic parameters,
or the nested variant forms. A valid union elsewhere in the same compilation
must continue to generate.

The case files under `RecordStructUnions/cases/` use the standard
`//@ should_fail` / `//@ should_pass` headers and `//~ ERROR` markers. These
cases establish diagnostic presence and line location. Direct behavioral tests
must additionally inspect severity, exact span/message, generated hint names,
and compiler diagnostics.

## Generator diagnostic cases

### should_fail

| File name | Coverage |
|---|---|
| `Detect_RecordStructUnion_Basic` | Public partial `record struct` union with one method-declared variant. Exactly one `SPIRE_DU020` on `record`; no generated union source. |
| `Detect_RecordStructUnion_Fieldless` | Fieldless outer `record struct` union. The absence of variants does not make the outer declaration valid. |
| `Detect_RecordStructUnion_Bodyless` | A bodyless `partial record struct State;` is rejected before generation. |
| `Detect_RecordStructUnion_Positional` | Outer record struct with a positional parameter list. Report `SPIRE_DU020` before any positional-record handling; do not preserve or emit record members. |
| `Detect_RecordStructUnion_Readonly` | `readonly partial record struct` union. Readonly is allowed for ordinary struct unions, but `record` remains an error. |
| `Detect_RecordStructUnion_Generic` | Generic outer `record struct` union with a method variant. Report only `SPIRE_DU020` for the outer shape; do not replace it with generic-layout diagnostics. |
| `Detect_RecordStructUnion_Nested` | Nested `[DiscriminatedUnion] partial record struct` inside a non-generic containing type. Report on the nested union's `record` token and keep its containing type context. |
| `Detect_RecordStructUnion_Json` | Record-struct union requesting System.Text.Json/Newtonsoft JSON. Report `SPIRE_DU020` before JSON-reference/converter/schema generation; no JSON source is emitted for that union. |
| `Detect_RecordStructUnion_MultiplePartDeclarations` | Same outer union declared as `partial record struct` in two authored parts. Report once on the declaration selected by the generator and do not create a conflicting generated partial declaration. A direct test must verify no CS0261 cascade. |
| `Detect_RecordStructUnion_AttributeOnPlainPartialPart` | A malformed group has the attribute on a plain struct part and a record-struct part represented by a separate Roslyn symbol. Report on the record token and emit no implementation; existing input compiler errors remain. |
| `Detect_RecordStructUnion_MultipleUnions` | One invalid record-struct union and one valid ordinary struct union in the same compilation. Report `SPIRE_DU020` only for the invalid union; the valid sibling's generated API remains available. |

### should_pass

| File name | Coverage |
|---|---|
| `NoReport_StructUnion` | Ordinary mutable `partial struct` union with method variants. Existing generated source remains valid. |
| `NoReport_ReadonlyStructUnion` | `readonly partial struct` union with method variants. Readonly is not confused with `record`. |
| `NoReport_RecordClassUnion` | Supported `[DiscriminatedUnion] partial record` union using its existing nested-record inheritance path. This is not a struct-based union. |
| `NoReport_UnannotatedRecordStruct` | An ordinary nested or top-level `record struct` without `[DiscriminatedUnion]`. No generator diagnostic. |
| `NoReport_NestedRecordStructVariant` | Ordinary struct union containing `[Variant] public record struct Value(int amount);`. The nested record payload is allowed and must not be mistaken for the outer union. |
| `NoReport_RecordStructContainingValidUnion` | A record-struct containing type with an inner `[DiscriminatedUnion] public partial struct State`. The containing record is not itself the annotated union. The inner ordinary struct union remains generated. |
| `NoReport_SemanticAttributeLookalike` | Source-local lookalike attributes named `DiscriminatedUnionAttribute`/`VariantAttribute` in another namespace. Only the real Spire attribute identity triggers generation and `SPIRE_DU020`. |
| `NoReport_ValidSiblingWithInvalidNestedPayloadShape` | A valid ordinary struct union with an unannotated nested record struct or helper record. It should not receive an outer-union diagnostic merely because nested records exist. |

## Direct generator and compiler integration

Create a Unity-independent behavioral test class and a Unity-tagged class
using the real generator harness. These tests should run the generator, inspect
`GeneratorDriverRunResult`, inspect `OutputCompilation.GetDiagnostics()`, and
compile/emit valid siblings where applicable.

| Test name | Required assertions |
|---|---|
| `RecordStructUnionDiagnostic_HasErrorSeverityAndExactLocation` | The diagnostic ID is `SPIRE_DU020`, severity is `Error`, message exactly says `Struct-based discriminated unions cannot be records. Remove the 'record' keyword.`, and the source span contains only `record`. |
| `RecordStructUnion_SuppressesAllGeneratedSourcesForRejectedUnion` | No generated tree for the rejected union's core implementation, JSON converter/schema, Unity callbacks, or drawer. The generator diagnostic is still reported. |
| `RecordStructUnion_DoesNotProduceCompilerCascade` | A basic rejected declaration produces no generated CS0261/CS0556/CS0563/CS0738 cascade. User-authored C# errors, if deliberately included, remain distinguishable. |
| `RecordStructUnion_ValidSiblingStillGenerates` | With invalid and valid unions together, the valid union's generated type, factory, equality, and variant payload are present and compile. |
| `RecordStructUnion_UnityUsesDU020AsPrimaryDiagnostic` | Under actual Unity references and symbols, a serializable record-struct union produces `SPIRE_DU020`, not the old Unity serialization diagnostic, and no Unity support source for the rejected union. |
| `RecordStructUnion_JSONDoesNotGenerateConverters` | A rejected record-struct union requesting either JSON library produces no converter/schema source for that union, regardless of referenced JSON assemblies. |
| `RecordStructUnion_ReadonlyGenericNestedShapesAllReportDU020` | Parameterize or separate tests for readonly, generic, and nested outer unions; each reports one `SPIRE_DU020` and no replacement diagnostic such as `SPIRE_DU018`. |
| `RecordStructUnion_DiagnosticIsNotReportedForNestedRecordPayload` | A valid ordinary struct union with a nested record-struct variant compiles and has no `SPIRE_DU020`; payload generation remains available. |

Update the existing readonly-variant regression
`ReadonlyRecordVariantsTests.RecordStructOuterUnion_IsRejectedWithoutNestedReadonlyWarning`:
it should expect `SPIRE_DU020` on the outer `record` keyword, assert no
`SPIRE030`, and assert that generated output does not contain the old CS0261
partial-type cascade.

## Code-fix cases

The dedicated provider is fixable for `SPIRE_DU020` only. Its action title is
`Remove record from union`, with equivalence key
`Spire.Union.RemoveRecord`. Fixture tests need actual generator diagnostics
remapped to workspace syntax trees; analyzer-only collection is insufficient.

### Positive before/after fixtures

| Case name | Expected transformation |
|---|---|
| `SPIRE_DU020_RemoveRecord_Basic` | `public partial record struct State { ... }` becomes `public partial struct State { ... }`; nested record-struct variants and method variants remain unchanged. Regeneration compiles. |
| `SPIRE_DU020_RemoveRecord_Readonly` | `public readonly partial record struct State` becomes `public readonly partial struct State`; readonly and all other modifiers remain. |
| `SPIRE_DU020_RemoveRecord_Generic` | Generic type parameters, constraints, base lists, attributes, and body are preserved while only `record` is removed. |
| `SPIRE_DU020_RemoveRecord_EmptyPositional` | `partial record struct State()` becomes an ordinary `partial struct State` with an empty body/valid declaration. No positional parameter is silently retained as a generated member. |
| `SPIRE_DU020_RemoveRecord_Bodyless` | A bodyless declaration is converted to a valid ordinary struct declaration, preserving accessibility, attributes, semicolon/trivia conventions, and any separate partial body. |
| `SPIRE_DU020_RemoveRecord_MultiplePartialParts` | Every authored partial declaration of the same union symbol loses `record`, including a part without variants. Unrelated types and generated trees are unchanged. |
| `SPIRE_DU020_RemoveRecord_MultiDocument` | Same-symbol partial declarations across two documents are both updated; document identity, comments, directives, CRLF/LF endings, and nested variants remain intact. |
| `SPIRE_DU020_RemoveRecord_MultipleUnions` | Applying the action to one diagnostic changes only that union; a second invalid union is unchanged. Fix All changes both independently. |
| `SPIRE_DU020_RemoveRecord_TriviaAndAttributes` | Comments adjacent to `record`, preprocessor directives, attribute lists, accessibility, `readonly`, `partial`, base lists, and trailing comments survive the edit. |

After each positive fix, rerun the actual generator and compiler. For a
fieldless or method-variant union, emit/load a representative value and verify
the generated equality and variant API. For a union retaining nested authored
record-struct payloads, verify payload conversion/reconstruction remains
available after the outer keyword is removed.

### No-action and refusal cases

| Test name | Expected behavior |
|---|---|
| `RemoveRecord_NonEmptyPositionalUnionHasNoAction` | A record-struct union with a nonempty positional parameter list still reports `SPIRE_DU020`, but the fix offers no action; it must not discard parameters or synthesized properties. |
| `RemoveRecord_OtherDiagnosticHasNoAction` | SPIRE_DU019, SPIRE_DU013, SPIRE030, an analyzer diagnostic, or an unrelated ID produces no `Remove record from union` action. |
| `RemoveRecord_StaleOrEmptyPathDiagnosticHasNoAction` | A stale span, foreign syntax tree, or indeterminate empty-path diagnostic cannot target a current declaration by coincidental position. |
| `RemoveRecord_NonUnionHasNoAction` | An unannotated record struct, record class, or ordinary struct does not receive the action even if its text contains `record`. |
| `RemoveRecord_LookalikeAttributesHasNoAction` | Source-local lookalike attributes do not produce or activate the fix. |
| `RemoveRecord_GeneratedDeclarationHasNoAction` | Generated `.g.cs` declarations are never edited directly. Regeneration from the authored declaration is the only source of generated changes. |
| `RemoveRecord_EmptyPositionalAndBodylessRefusalBoundary` | Empty `()` and bodyless forms are fixable; any nonempty positional parameter in any authored partial part suppresses the action for the whole symbol. |

## Acceptance checks

- Portable generator case files and direct Unity tests cover flagged
  declaration forms; eight case files cover exemptions.
- Direct generator tests verify exact diagnostic shape and no generated-source
  or compiler cascade for rejected unions.
- Six positive code-fix fixture pairs plus direct workspace cases cover
  declaration preservation, partial/multi-document edits, trivia, and regeneration.
- At least seven no-action/refusal tests protect semantic and safety boundaries.
- Unity-dependent tests carry `Trait("Environment", "Unity")` and use real
  Unity references; portable cases do not claim Unity behavior.
- Existing SPIRE030 and record-variant tests continue to pass after the new
  outer-union diagnostic and code fix are added.
