# SPIRE009 Test Coverage Matrix — New Cases

This matrix lists only new cases not already covered by the 29 existing tests.

---

## Category A: Readonly and single-variant struct unions

### should_fail

| File Name | Description |
|-----------|-------------|
| `ReadonlyStruct_MissingVariant` | `readonly partial struct` union, switch expression missing one variant |
| `Struct_SingleVariant_Missing` | Union with exactly one variant, switch expression has zero arms |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_ReadonlyStruct_FullCoverage` | `readonly partial struct` union, all variants covered via positional patterns |
| `Pass_Struct_SingleVariant_Full` | Union with exactly one variant, switch expression covers that variant |

---

## Category B: Fieldless and mixed-fieldless variant unions

Fieldless variants are declared as `[Variant] public static partial Shape None();` — zero parameters.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Struct_Fieldless_MissingVariant` | All variants are fieldless, switch missing one |
| `Struct_Mixed_MissingFieldless` | Mix of fielded and fieldless variants; switch covers fielded but omits fieldless |
| `Struct_Mixed_MissingFielded` | Mix of fielded and fieldless variants; switch covers fieldless but omits fielded |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_Struct_AllFieldless_Full` | All variants fieldless, switch covers all via positional patterns |
| `Pass_Struct_Mixed_Full` | Mix of fielded and fieldless variants, all covered |
| `Pass_Record_Fieldless_Full` | Record union with a fieldless variant (`record None()`), all covered |

---

## Category C: Many-variant unions (5+ variants)

### should_fail

| File Name | Description |
|-----------|-------------|
| `Struct_FiveVariants_MissingOne` | 5-variant struct union, switch missing exactly one variant |
| `Struct_FiveVariants_MissingThree` | 5-variant struct union, switch covers only two |
| `Record_FiveVariants_MissingOne` | 5-variant record union, switch missing one |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_Struct_FiveVariants_Full` | 5-variant struct union, all five covered |
| `Pass_Struct_FiveVariants_OrPattern` | 5-variant struct union, all covered via two `or` pattern arms |

---

## Category D: Generic struct unions

Struct unions with type parameters (e.g., `partial struct Result<T, E>`).

### should_fail

| File Name | Description |
|-----------|-------------|
| `GenericStruct_MissingVariant` | Generic struct union (`Result<T, E>`), switch expression missing one variant |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_GenericStruct_FullCoverage` | Generic struct union, all variants covered |

---

## Category E: Multiple switches in same file / method

### should_fail

| File Name | Description |
|-----------|-------------|
| `TwoSwitches_BothMissing` | Same method contains two switch expressions, both missing variants; both flagged |
| `TwoSwitches_OneMissing` | Same method contains two switch expressions, only one missing a variant; only that one flagged |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_TwoSwitches_BothFull` | Same file has two switch expressions on different union instances, both fully covered |

---

## Category F: Contextual switches (async, lambdas, expression-bodied, local functions)

### should_fail

| File Name | Description |
|-----------|-------------|
| `Struct_AsyncMethod_Missing` | Switch expression inside an `async Task<int>` method, missing a variant |
| `Struct_Lambda_Missing` | Switch expression inside a lambda body, missing a variant |
| `Struct_LocalFunction_Missing` | Switch expression inside a local function, missing a variant |
| `Struct_ExpressionBodied_Missing` | Switch expression as expression-bodied member (`=>`), missing a variant |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_Struct_AsyncMethod_Full` | Switch expression in async method, all variants covered |
| `Pass_Struct_Lambda_Full` | Switch expression in lambda, all variants covered |
| `Pass_Struct_LocalFunction_Full` | Switch expression in local function, all variants covered |
| `Pass_Struct_ExpressionBodied_Full` | Expression-bodied member with full coverage |

---

## Category G: Nested namespace / nested class declarations

### should_fail

| File Name | Description |
|-----------|-------------|
| `NestedNamespace_MissingVariant` | Union declared inside nested namespace, switch missing variant |
| `Struct_NestedInClass_Missing` | Union declared inside a class (inner type), switch missing variant |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_NestedNamespace_Full` | Union in nested namespace, all variants covered |

---

## Category H: `when` guard edge cases

### should_fail

| File Name | Description |
|-----------|-------------|
| `Struct_WhenGuard_AndMissingArm` | Switch has `when` guard on one arm AND is also missing another variant entirely (two issues, one diagnostic) |
| `Struct_SwitchStmt_WhenGuard` | Switch statement with a `when` guard on the only arm covering a variant |
| `Record_WhenGuard_MissingTwo` | Record union switch with two variants guarded by `when`, neither treated as covered |
| `Struct_PropertyPattern_WhenGuard` | Property pattern arm with `when` guard — not exhaustive |

### should_pass

None new in this category (behavior already proven by existing `Struct_WhenGuard` fail case and pass cases).

---

## Category I: `or` / `and` / `not` complex patterns

### should_fail

| File Name | Description |
|-----------|-------------|
| `Struct_NotPattern_Missing` | `not` pattern used as single arm (e.g., `not (Shape.Kind.Circle, _)`); doesn't name variants explicitly, missing |
| `Struct_OrPattern_WithWhen_Missing` | `or` pattern covers all variant kinds but one arm has `when` guard — not exhaustive |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_Struct_OrPattern_AllFive` | 5-variant union, single arm covering all five with `or` pattern |
| `Pass_Struct_OrPattern_SwitchStmt` | Switch statement where all variants covered via `or` patterns in case labels |

---

## Category J: Switch statement additional patterns

### should_fail

| File Name | Description |
|-----------|-------------|
| `Record_SwitchStmt_Missing` | Record union in switch statement, missing one variant |
| `Struct_SwitchStmt_PropertyPattern_Missing` | Switch statement using property patterns, missing one variant |
| `Struct_SwitchStmt_WhenGuard_NoMissingArm` | Switch statement with a `when` guard on the only arm for one variant (that variant is not fully covered) |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_Record_SwitchStmt_Full` | Record union switch statement, all variants covered |
| `Pass_Class_SwitchStmt_Full` | Class union switch statement, all variants covered |
| `Pass_Struct_SwitchStmt_PropertyPattern_Full` | Switch statement using property patterns on struct union, all covered |

---

## Category K: Class union additional coverage

### should_fail

| File Name | Description |
|-----------|-------------|
| `Class_WhenGuard` | Class union switch where an arm has `when` guard — not fully covered |
| `Class_PropertyPattern_Missing` | Class union using property pattern, missing a variant |
| `Class_OrPattern_Partial` | Class union `or` pattern covers some but not all variants |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_Class_PropertyPattern_Full` | Class union all variants covered via property patterns |
| `Pass_Class_OrPattern_Full` | Class union all variants covered via `or` pattern |
| `Pass_Suppressor_ClassAllCovered` | CS8509 suppressed for fully covered class union |
| `Pass_Class_AsyncMethod_Full` | Class union switch in async method, all covered |

---

## Category L: Suppressor edge cases

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_Suppressor_SwitchStmt_StructFull` | CS8509 NOT a switch expression concern — suppressor applies to switch expression only; switch statement with all covered compiles without CS8509 anyway |
| `Pass_Suppressor_GenericStructFull` | Generic struct union switch expression fully covered, CS8509 suppressed |
| `Pass_Suppressor_FiveVariantsFull` | 5-variant struct union fully covered, CS8509 suppressed |

---

## Category M: Misc / edge cases that try to break the analyzer

### should_fail

| File Name | Description |
|-----------|-------------|
| `Struct_SwitchInTernary_Missing` | Switch expression used inside a ternary-like context (as value in `condition ? switchExpr : other`), missing variant |
| `Struct_PassedAsArgument_Missing` | Union value passed as method argument, returned switch expression on that parameter is missing variant |
| `Struct_FieldInitializer_Missing` | Switch expression used as a field initializer value (static field), missing a variant |
| `Struct_LINQ_Missing` | Switch expression used inside a LINQ `Select` lambda, missing a variant |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_Struct_SwitchOnProperty_Full` | Switch on a union-typed property, all covered |
| `Pass_Struct_SwitchOnReturnValue_Full` | Switch on inline method return value, all covered |
| `Pass_NonUnion_ClassSwitch` | Switch on a plain (non-DU) class hierarchy with type patterns — not a DU, no diagnostic |
| `Pass_Struct_SwitchInConditional_Full` | Switch expression inside a conditional expression, all covered |
