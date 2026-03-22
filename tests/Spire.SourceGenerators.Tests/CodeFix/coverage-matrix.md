# Code Fix Coverage Matrix — SPIRE009 (AddMissingArms) and SPIRE011 (FixFieldType)

All 20 existing cases are documented in the task description and already implemented.
This matrix covers only NEW cases not yet present.

---

## Category A: AddMissingArms — Switch Statement (not expression)

The exhaustiveness analyzer fires on both switch expressions and switch statements.
No existing code fix test uses a switch statement. The fix must work in statement context too.

| Case Name | Description |
|-----------|-------------|
| `AddMissingArms_Property_SwitchStatement` | before: switch statement on a 3-variant union (Circle/Rectangle/Square) with only Circle arm (property pattern). after: Rectangle and Square arms appended as `case { kind: ..., field: type name }: throw new System.NotImplementedException();` |
| `AddMissingArms_Deconstruct_SwitchStatement` | before: switch statement on the same 3-variant Shape union with only Circle arm (deconstruct pattern). after: Rectangle and Square deconstruct arms appended. |
| `AddMissingArms_Property_SwitchStatement_AllFieldless` | before: switch statement on an all-fieldless Light union (Red/Yellow/Green) covering only Red. after: Yellow and Green fieldless arms appended. Exercises fieldless + statement mode together. |

---

## Category B: AddMissingArms — All Arms Missing

No existing test starts from a completely empty switch expression (zero non-wildcard arms). This is the maximum-addition scenario and stresses ordering and comma placement.

| Case Name | Description |
|-----------|-------------|
| `AddMissingArms_Property_AllMissing` | before: switch expression with zero arms (empty `{ }`) on a 2-variant Result union. after: both Ok and Err arms inserted. Tests that the fix works when there are no existing arms to infer style from — style detection must fall back or default to property. |
| `AddMissingArms_Deconstruct_AllMissing` | before: switch expression with zero arms on the same 2-variant Result union, using deconstruct style (no existing arm to detect from). after: both arms inserted in deconstruct style. (?) Note: if the fix cannot detect style from an empty switch, clarify with lead which style is used as default. |

---

## Category C: AddMissingArms — Variant Names That Are Contextual Keywords

C# keywords and contextual keywords are legal as variant names when `@`-escaped. The code fix must emit the kind reference correctly without breaking identifier syntax.

| Case Name | Description |
|-----------|-------------|
| `AddMissingArms_Property_KeywordVariantName` | before: union with variants named `@event` and `@class` (keyword identifiers), switch covers only `@event`. after: `@class` arm added using the correct `Kind.@class` reference and `@class`-named field binding. Tests that the fix does not emit bare `class` (a keyword), which would be a syntax error. |
| `AddMissingArms_Deconstruct_KeywordVariantName` | before: same keyword-named union, switch in deconstruct style covering only `@event`. after: `@class` deconstruct arm added with correct escaping. |

---

## Category D: AddMissingArms — Special Field Types

Existing tests use primitive types (int, float, double, string). The fix derives field types from the factory method's parameters; these cases verify it handles non-trivial types.

| Case Name | Description |
|-----------|-------------|
| `AddMissingArms_Property_NullableField` | before: union with a variant `Some(string? value)` (nullable reference type), switch missing the Some arm. after: Some arm added with `string? someValue` in property style. Verifies nullable annotation in the generated type annotation. |
| `AddMissingArms_Deconstruct_NullableField` | before: same nullable-field union, deconstruct switch missing Some arm. after: deconstruct arm `(Union.Kind.Some, string? value)` added. |
| `AddMissingArms_Property_ArrayField` | before: union with a variant `Batch(int[] items)`, switch missing Batch. after: Batch arm added with `int[] items` in property style. |
| `AddMissingArms_Property_EnumField` | before: union where one variant carries an enum field (`Status status`), switch missing that variant. after: arm added using the fully-qualified or minimally-qualified enum type. |
| `AddMissingArms_Property_StructField` | before: union where one variant carries a custom value-type field (e.g., `Point point`), switch missing that variant. after: arm added with the struct type as the field annotation. |
| `AddMissingArms_Property_GenericUnionField` | before: union where a variant holds a generic field, e.g., `Wrapped(List<int> items)`, missing arm. after: arm added with `List<int> items` in property style. |

---

## Category E: AddMissingArms — Context Variations

Covers syntactic positions not yet tested: local functions, lambdas, expression-bodied properties, nested classes, and multiple switches in one file.

| Case Name | Description |
|-----------|-------------|
| `AddMissingArms_Property_LocalFunction` | before: switch expression inside a local function (not a member method), missing one arm. after: missing arm added. Tests that the fix finds the correct switch node in a local-function scope. |
| `AddMissingArms_Property_ExpressionBodiedProperty` | before: a property getter `=> someUnion switch { ... };` missing one arm. after: missing arm added. Exercises expression-bodied property context (different parent syntax than a method). |
| `AddMissingArms_Property_NestedClass` | before: union declared inside a namespace, switch inside a class that is itself nested inside another class. after: missing arm added. Tests fully-qualified Kind reference path when the consumer is in a nested class. |
| `AddMissingArms_Property_MultipleSwitch` | before: two switch expressions in one file both missing arms on the same union; the first one is targeted by the fix. after: only the first switch has the arm added (second is unchanged). Verifies the fix applies to exactly one diagnostic site. |
| `AddMissingArms_Deconstruct_LocalFunction` | before: switch inside a local function using deconstruct style, missing one arm. after: missing arm added. |
| `AddMissingArms_Deconstruct_ExpressionBodiedProperty` | before: deconstruct-style switch inside an expression-bodied property, missing one arm. after: missing arm added. |

---

## Category F: AddMissingArms — Two-Variant Unions

Existing tests use 3- or 5-variant unions. A 2-variant union (the minimal non-trivial case) where exactly one arm is present exercises the boundary between "all missing" and "one present".

| Case Name | Description |
|-----------|-------------|
| `AddMissingArms_Property_TwoVariant` | before: 2-variant Result union (Ok/Err), only Ok arm present in property style. after: Err arm appended. Minimal case — exactly one arm missing, one present. |
| `AddMissingArms_Deconstruct_TwoVariant` | before: same 2-variant Result union in deconstruct style, only Ok arm present. after: Err arm appended. |
| `AddMissingArms_Property_TwoFieldless` | before: 2-variant all-fieldless union (Yes/No), only Yes arm present. after: No arm added with no field subpatterns `{ kind: Union.Kind.No }`. |

---

## Category G: FixFieldType — Context Variations

Existing tests exercise the basic single-field and multi-field cases. Missing: different syntactic positions, different wrong-type patterns.

| Case Name | Description |
|-----------|-------------|
| `FixFieldType_Property_LocalFunction` | before: switch inside a local function, property pattern arm has a wrongly-typed declaration (`int bad` instead of `var bad`). after: type replaced with `var`. |
| `FixFieldType_Property_ExpressionBodiedProperty` | before: property getter with switch, property pattern arm has wrong type. after: corrected to `var`. |
| `FixFieldType_Deconstruct_LocalFunction` | before: deconstruct switch inside a local function, wrong field type. after: corrected to the expected type. |
| `FixFieldType_Deconstruct_ExpressionBodiedProperty` | before: deconstruct switch in expression-bodied property, wrong field type. after: corrected. |
| `FixFieldType_Property_ReadonlyRefStruct` | before: `readonly ref partial struct` union (ref struct variant of the exhaustiveness check), property pattern arm with wrong type annotation. after: `var` substituted. Tests that the fix works on ref struct unions (which are structurally similar but carry `ref` modifier). |

---

## Category H: FixFieldType — Wrong Type Varieties

Existing tests always use `string` as the wrong type being corrected. Missing: other wrong types, nullable annotations, and compound types.

| Case Name | Description |
|-----------|-------------|
| `FixFieldType_Deconstruct_IntToFloat` | before: deconstruct arm where a `float` field is annotated as `int`. after: `int bad` replaced with `float bad`. Tests correction to a different primitive (not `string → actual`). |
| `FixFieldType_Deconstruct_WrongNullable` | before: deconstruct arm where a `string?` field (nullable) is annotated as `string` (non-nullable). after: `string bad` replaced with `string? bad`. Verifies nullable annotation is preserved in the expected type. |
| `FixFieldType_Property_WrongNullable` | before: property pattern arm where a `string?` field is annotated `string someField` (non-nullable). after: `string someField` replaced with `var someField`. |
| `FixFieldType_Deconstruct_SecondFieldWrong` | before: multi-field variant where the FIRST field is correctly typed but the SECOND field has the wrong type annotation. after: only the second subpattern's type is corrected. Verifies the fix targets the correct subpattern by FieldIndex, not always the first. |
| `FixFieldType_Deconstruct_ThirdFieldWrong` | before: 3-field variant where the third field (index 2) has the wrong type. after: third subpattern type corrected. |

---

## Category I: FixFieldType — Discard and Var Interactions

Existing tests for typed discard use `string _`. Missing combinations: `var` discard that is unexpectedly flagged, interaction with wildcard in adjacent fields.

| Case Name | Description |
|-----------|-------------|
| `FixFieldType_Deconstruct_ReadonlyStruct` | before: `readonly partial struct` union, deconstruct arm where a field type is wrong. after: type corrected to expected. Tests readonly modifier on the union side for deconstruct. |
| `FixFieldType_Property_MultipleWrong` | before: property pattern with two wrongly-typed field annotations in the same arm. The fix targets the first diagnostic. after: only the first wrong annotation corrected; second remains wrong (since the fix applies one diagnostic at a time). Verifies the fix is scoped to the single flagged `DeclarationPatternSyntax`. |
| `FixFieldType_Deconstruct_MultipleWrong` | before: deconstruct arm with two wrong types (fields 1 and 2 both wrong). Fix targets first diagnostic only. after: only field 1 corrected. Same single-diagnostic scoping verification as above for deconstruct. |
