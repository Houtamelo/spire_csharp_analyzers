# Struct union code-fix coverage

This matrix covers the approved struct-union generated-arm policy and the
field-name collision rules. Every generated struct arm uses a property pattern
with `property_name: var local`. Record and class generated-arm behavior stays
outside this change and remains covered by the existing record/class tests.

## AddMissingArms

| Case | Coverage |
| --- | --- |
| `AddMissingArms_StructSharedArityNullableReference_Expression` | Shared one-field arity with `Effect?`, an existing positional arm, and a missing expression arm. The new arm captures `effect: var effect` and executes for null and non-null payloads. |
| `AddMissingArms_StructUniqueArityNullableReference_Statement` | Unique typed arity with `Effect?`, an existing positional arm, and a missing switch-statement arm. |
| `AddMissingArms_StructNullableArrayAndValue_Expression` | `string[]?` and `int?` fields in a multi-field expression arm; typed helper calls prove both captures. |
| `AddMissingArms_StructNonNullableControl_Statement` | Nonnullable `Effect` field in a switch statement; the generated arm still uses `effect: var effect`. |
| `AddMissingArms_StructNullableDisabledReference_Expression` | `#nullable disable` reference field. The property `var` capture must still match null and non-null runtime values. |
| `AddMissingArms_StructCompatibleNullableMerge_Expression` | Same field name and semantic reference type across `Effect?` and `Effect` variants. The generated shared property is captured with `var` and accepts the combined nullable contract. |
| `AddMissingArms_StructIncompatibleFieldCollision_Expression` | `Count(int value)` and `Text(string value)`. The missing arm must use the qualified `text_value: var value` property. |
| `AddMissingArms_StructAcronymCollision_Expression` | `HTTPResponse(int value)` and `URL(string value)`. The qualified property verifies acronym-aware casing (`url_value`). |
| `AddMissingArms_StructKeywordCapture_Expression` | A parameter named `@event`; the generated property and local capture must remain valid escaped identifiers. |
| `AddMissingArms_StructEnclosingLocalCollision_Expression` | An enclosing `value` local forces the generated `Count(int value)` arm to use the unique capture `value1` while preserving the original field/property name. |
| `AddMissingArms_StructTupleElementNameCollision_Expression` | Tuple fields with the same underlying `(Effect?, int)` type but different element names must share `value: var value`; the nullable nested element is exercised at runtime. |
| `AddMissingArms_StructEnclosingPatternCollision_Expression` | An enclosing `if (input is int value)` pattern forces the generated `Count(int value)` arm to use `value1`. |

The existing struct snapshot pairs under `CodeFix/cases/AddMissingArms_*` are
updated only for generated struct arms: newly inserted fields use property
patterns and `var` captures. Existing user-written arms are unchanged.

## ExpandWildcard

| Case | Coverage |
| --- | --- |
| `ExpandWildcard_StructSharedArityNullableReference_Expression` | Shared `Effect?` field and existing positional arm. The wildcard replacement uses `effect: var effect` and a typed runtime helper. |
| `ExpandWildcard_StructNullableValue_Expression` | Nullable value field `int? count`; the replacement uses `count: var count` and matches null and non-null values. |
| `ExpandWildcard_StructNullableArray_Expression` | Nullable array field `string[]? items`; the replacement uses `items: var items` and matches null and non-null arrays. |
| `ExpandWildcard_StructPropertyCoveredVariant_Expression` | A property-pattern `Ready` arm followed by `_` must generate only the uncovered `ApplyEffect` arm. |

## Name and generic edge cases

| Case | Coverage |
| --- | --- |
| `AddMissingArms_StructUnderscoreParameter_Expression` | A factory parameter named `_` must produce a real capture `_1`, so the runtime body can consume the value instead of treating it as a discard. |
| `AddMissingArms_StructTrailingLocalCollision_Expression` | A local declared after the switch still participates in the declaration-space collision check; the generated capture is `value1`. |
| `AddMissingArms_GenericStructKindPrefix_Expression` | A closed generic union requires `Command<int>.Kind` in the generated property pattern. |
| `AddMissingArms_NestedGenericContainingKindPrefix_Expression` | A union nested in `Outer<T>` requires `Outer<int>.Command.Kind` in the generated property pattern. |

Wildcard expansion remains limited to switch expressions. Record/class wildcard
behavior and switch-statement wildcard support are outside this change.

## Runner contract

`NullablePatternCodeFixTests` applies the real generator, analyzer, code action
or refactoring, and workspace change. It then:

1. Compares the changed document with the hand-written expected source.
2. Compiles the changed project and checks that SPIRE009/SPIRE011 are absent.
3. Replaces only the generated throw/body in a runtime probe with a typed helper
   call. This makes the inferred `var` type part of compilation and executes
   null and non-null factories where applicable.
4. Emits and loads each probe assembly with runtime-compatible references.

The collision cases also compile the generated property API and run the missing
arm, so a field-name conflict or an incorrect qualified property fails before
the code-fix output can be accepted.
