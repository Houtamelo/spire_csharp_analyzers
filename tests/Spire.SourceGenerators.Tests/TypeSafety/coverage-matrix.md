# SPIRE011 / SPIRE012 Test Coverage Matrix

This matrix covers only **new** cases not yet present in the existing 11 tests.

---

## Category A: SPIRE011 — Type mismatch in property patterns

Property patterns on struct unions expose named properties (`{ radius: ... }`). The analyzer
must flag any explicit type annotation on a property sub-pattern for a variant field (the spec
says the user must use `var`). No existing tests exercise property-pattern type checking.

### should_fail

| File Name | Description |
|-----------|-------------|
| `PropertyPattern_ExplicitType_SingleField` | Property pattern uses explicit `double` type on `radius` field (`{ radius: double r }`) — must use `var` |
| `PropertyPattern_ExplicitType_WrongType` | Property pattern uses wrong type (`int` instead of `double`) on `radius` field — wrong AND not `var` |
| `PropertyPattern_ExplicitType_MultiField` | Multi-field variant; property pattern puts explicit types on both fields |
| `PropertyPattern_ExplicitType_OneOfTwo` | Two-field variant; only the second field has explicit type in property pattern |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_PropertyPattern_VarBinding` | Property pattern uses `var` binding for field — correct form (`{ radius: var r }`) |
| `Pass_PropertyPattern_ConstantSubPattern` | Property pattern tests a constant value (`{ radius: 0.0 }`) — no type annotation to check |
| `Pass_PropertyPattern_KindOnly` | Property pattern matches only `kind` field, no variant field type patterns at all |
| `Pass_PropertyPattern_WildcardSubPattern` | Property pattern uses `_` discard for field — `{ radius: _ }` |

---

## Category B: SPIRE011 — Type mismatch in non-standard positions and patterns

The existing fail tests only cover position 1 (first field after kind) with simple bindings.

### should_fail

| File Name | Description |
|-----------|-------------|
| `TypeMismatch_ThirdField` | Three-field variant; wrong type on the third (position 3) field, correct on 1 and 2 |
| `TypeMismatch_SecondOfThree` | Three-field variant; wrong type only on the second field |
| `TypeMismatch_MultipleWrongInSameArm` | Two wrong types in the same positional arm (`string` where `int`, `bool` where `float`) |
| `TypeMismatch_SwitchStatement` | Switch statement (not expression) with wrong type in positional case label |
| `TypeMismatch_IfIsPattern` | `if (s is (Kind.X, string bad))` — wrong type in if/is positional pattern |
| `TypeMismatch_NumericsIntVsFloat` | Variant field is `float`; pattern uses `int` — numerically similar but wrong unmanaged type |
| `TypeMismatch_ReferenceField` | Variant field is `string`; pattern uses `int` — ref vs value type mismatch |
| `TypeMismatch_ArrayField` | Variant field is `int[]`; pattern uses `string[]` — array type mismatch |
| `TypeMismatch_ReadonlyStruct` | `readonly partial struct` union; wrong positional type on field |
| `TypeMismatch_MultipleArms` | Two separate arms each with a wrong type — two SPIRE011 diagnostics in same switch |
| `TypeMismatch_GenericVariantField` | Generic union `Result<T,E>` struct; positional arm uses wrong concrete type for a type-parameter field |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_SwitchStatement_CorrectTypes` | Switch statement (not expression) with all correct positional types |
| `Pass_ReadonlyStruct_CorrectTypes` | `readonly partial struct` union; all correct types in positional switch |
| `Pass_WhenGuard_CorrectTypes` | Positional arm with a `when` guard and correct type — guard must not confuse the analyzer |
| `Pass_NestedSwitch` | Outer switch on one union, inner switch on another union inside the arm body — each correct |
| `Pass_ExpressionBodied` | Expression-bodied method using switch expression; correct positional types |
| `Pass_Lambda` | Lambda returning a switch expression on a union with correct positional types |
| `Pass_AsyncMethod` | `async` method; correct positional types — ensure async context does not suppress analysis |

---

## Category C: SPIRE012 — Field count mismatch (all new — zero existing fail tests)

No should_fail tests exist for SPIRE012. Every case here is new.

### should_fail

| File Name | Description |
|-----------|-------------|
| `FieldCount_TooMany_OneVariant` | Single-field variant; positional pattern supplies 2 fields after kind |
| `FieldCount_TooFew_TwoFieldVariant` | Two-field variant; positional pattern supplies only 1 field after kind |
| `FieldCount_TooMany_ThreeVariant` | Three-field variant; pattern supplies 4 fields |
| `FieldCount_TooFew_ThreeVariant` | Three-field variant; pattern supplies 2 fields |
| `FieldCount_Fieldless_HasFields` | Fieldless variant (`Eof()`) matched with one positional field — count 0 expected, 1 supplied |
| `FieldCount_TooMany_SwitchStatement` | Switch statement (not expression) with too many fields in case label |
| `FieldCount_TooFew_IfIs` | `if (s is (Kind.X, int a))` where variant X has 2 fields — too few |
| `FieldCount_MultipleArms_BothWrong` | Two arms in same switch, each with wrong field count — two SPIRE012 diagnostics |
| `FieldCount_BoxedFields_TooMany` | `Layout.BoxedFields` union; positional arm supplies too many fields |
| `FieldCount_Additive_TooFew` | `Layout.Additive` union; positional arm supplies too few fields |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_FieldCount_Exact_SingleField` | Single-field variant; positional pattern supplies exactly 1 field — no diagnostic |
| `Pass_FieldCount_Exact_MultiField` | Three-field variant; positional pattern supplies exactly 3 fields |
| `Pass_FieldCount_Fieldless_NoFields` | Fieldless variant matched with `(Kind.Eof, _)` where `_` is not a field but the shared arity slot — no diagnostic (?) |
| `Pass_FieldCount_MixedVariants` | Union with one 1-field and one 2-field variant; each arm has correct count |

---

## Category D: SPIRE011 — Union kinds other than default struct

The existing tests use only plain `partial struct` with the default layout.

### should_fail

| File Name | Description |
|-----------|-------------|
| `TypeMismatch_BoxedFields_Layout` | `Layout.BoxedFields` union; positional arm uses wrong type — Deconstruct emits `object?` for shared arity, but typed for unique-arity variant |
| `TypeMismatch_Additive_Layout` | `Layout.Additive` union; positional arm uses wrong type |
| `TypeMismatch_Overlap_Layout` | `Layout.Overlap` union; positional arm uses wrong type |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_Overlap_CorrectTypes` | `Layout.Overlap` union; all correct positional types |
| `Pass_Additive_CorrectTypes` | `Layout.Additive` union; all correct positional types |
| `Pass_Additive_VarBindings` | `Layout.Additive` union; all `var` bindings — no type check triggered |

---

## Notes / Open Questions

- `(?)` on `Pass_FieldCount_Fieldless_NoFields`: the test instructions say fieldless variants that participate in a shared-arity Deconstruct use `(Kind.Eof, _)` where `_` matches `out object? _f0`. It is unclear whether the analyzer counts this `_` slot as a "field" or recognizes the variant is fieldless. The implementer should clarify; if `(Kind.Eof, _)` is treated as 0 provided fields this is a pass, otherwise it may require `(Kind.Eof)` which is not valid positional syntax. Mark this case as `(?)` pending spec confirmation.

- `TypeMismatch_GenericVariantField`: generic struct unions may not be fully supported by the generator for type-checking purposes (the analyzer would need to instantiate type arguments). Include as a should_fail if the generator produces a typed Deconstruct for a unique-arity generic variant, otherwise remove if the generator falls back to `object?`.

- Property pattern "explicit type" rule (`PropertyPattern_ExplicitType_*`): the spec says "any explicit type (not `var`) on a variant field is wrong". This means even the **correct** type triggers SPIRE011 in property pattern context. The pass cases must use only `var`, constants, or `_` for field sub-patterns.
