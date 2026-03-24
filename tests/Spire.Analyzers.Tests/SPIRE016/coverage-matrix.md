# SPIRE016 Test Coverage Matrix

## Category A: `default` / `default(T)` on [MustBeInit] enum with no zero-valued member

All cases use `StatusNoZero` (Active=1, Inactive=2, Pending=3) or `FlagsNoZero` (Read=1, Write=2, Execute=4).
`default(T)` produces value 0, which is not a named member — always flagged.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_Default_LocalVariable` | Ensure that SPIRE016 IS triggered when `StatusNoZero x = default;` is used in a local variable declaration. |
| `Detect_ExplicitDefault_LocalVariable` | Ensure that SPIRE016 IS triggered when `StatusNoZero x = default(StatusNoZero);` is used in a local variable declaration. |
| `Detect_Default_ReturnStatement` | Ensure that SPIRE016 IS triggered when a method with return type `StatusNoZero` returns `default`. |
| `Detect_ExplicitDefault_ReturnStatement` | Ensure that SPIRE016 IS triggered when a method returns `default(StatusNoZero)`. |
| `Detect_Default_MethodArgument` | Ensure that SPIRE016 IS triggered when `default` is passed as a `StatusNoZero` argument to a method. |
| `Detect_ExplicitDefault_MethodArgument` | Ensure that SPIRE016 IS triggered when `default(StatusNoZero)` is passed as a method argument. |
| `Detect_Default_FieldInitializer` | Ensure that SPIRE016 IS triggered when a `StatusNoZero` field is initialized with `default`. |
| `Detect_Default_TernaryExpression` | Ensure that SPIRE016 IS triggered when `default` is the false-branch of a ternary expression targeting `StatusNoZero`. |
| `Detect_Default_LambdaBody` | Ensure that SPIRE016 IS triggered when a lambda with return type `StatusNoZero` returns `default`. |
| `Detect_Default_SwitchExpressionArm` | Ensure that SPIRE016 IS triggered when a switch expression arm yields `default` of type `StatusNoZero`. |
| `Detect_Default_AsyncMethodReturn` | Ensure that SPIRE016 IS triggered when an `async Task<StatusNoZero>` method returns `default`. |
| `Detect_Default_YieldReturn` | Ensure that SPIRE016 IS triggered when `default` appears in a `yield return` in an `IEnumerable<StatusNoZero>` iterator. |
| `Detect_Default_NullCoalescing` | Ensure that SPIRE016 IS triggered when `default` is the right-hand side of a `??` expression targeting `StatusNoZero`. |
| `Detect_ExplicitDefault_TupleElement` | Ensure that SPIRE016 IS triggered when `default(StatusNoZero)` is used as an element in a tuple expression. |
| `Detect_Default_FlagsNoZero` | Ensure that SPIRE016 IS triggered when `default` is used with `FlagsNoZero` (no zero member). |

---

## Category B: `default` / `default(T)` on [MustBeInit] enum WITH zero-valued member — not flagged

All cases use `StatusWithZero` (None=0), `ColorImplicitZero` (Red=0 implicit), or `FlagsWithZero` (None=0).
`default(T)` produces value 0, which matches a named member — safe.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_Default_StatusWithZero_LocalVariable` | Ensure that SPIRE016 is NOT triggered when `StatusWithZero x = default;` (zero member `None=0` exists). |
| `NoReport_ExplicitDefault_StatusWithZero_ReturnStatement` | Ensure that SPIRE016 is NOT triggered when returning `default(StatusWithZero)` (zero member exists). |
| `NoReport_Default_ColorImplicitZero_LocalVariable` | Ensure that SPIRE016 is NOT triggered when `ColorImplicitZero x = default;` (first member `Red` is implicitly 0). |
| `NoReport_ExplicitDefault_ColorImplicitZero_MethodArgument` | Ensure that SPIRE016 is NOT triggered when `default(ColorImplicitZero)` is passed as a method argument. |
| `NoReport_Default_FlagsWithZero_LocalVariable` | Ensure that SPIRE016 is NOT triggered when `default` targets `FlagsWithZero` (zero member `None=0` exists). |
| `NoReport_Default_StatusWithZero_TernaryExpression` | Ensure that SPIRE016 is NOT triggered when `default` is in a ternary expression targeting `StatusWithZero`. |
| `NoReport_Default_StatusWithZero_AsyncMethod` | Ensure that SPIRE016 is NOT triggered when an async method returns `default` of type `StatusWithZero`. |

---

## Category C: Integer-to-enum cast — constant value not a named member (should fail)

Tests `(MarkedEnum)N` where N is a compile-time constant with no matching named member.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_CastToEnum_ZeroValue_NoZeroMember` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)0` is used (0 is not a named member of `StatusNoZero`). |
| `Detect_CastToEnum_ConstantInvalidValue_ReturnStatement` | Ensure that SPIRE016 IS triggered when a method returns `(StatusNoZero)42` (42 is not a named member). |
| `Detect_CastToEnum_ConstantInvalidValue_MethodArgument` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)99` is passed as a method argument. |
| `Detect_CastToEnum_NegativeValue_LocalVar` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)(-1)` is used (negative, not a named member). |
| `Detect_CastToEnum_InvalidValue_WithZeroEnum` | Ensure that SPIRE016 IS triggered when `(StatusWithZero)99` is used (99 is not a named member even though 0 is). |
| `Detect_CastToEnum_ZeroValue_FlagsNoZero` | Ensure that SPIRE016 IS triggered when `(FlagsNoZero)0` is used (0 is not a named member of `FlagsNoZero`). |
| `Detect_CastToEnum_ConstantInvalidValue_FieldInitializer` | Ensure that SPIRE016 IS triggered when a `StatusNoZero` field is initialized with `(StatusNoZero)100`. |
| `Detect_CastToEnum_ConstantInvalidValue_TernaryExpr` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)5` is in a ternary expression. |
| `Detect_CastToEnum_ConstantInvalidValue_LambdaBody` | Ensure that SPIRE016 IS triggered when a lambda with return type `StatusNoZero` returns `(StatusNoZero)0`. |
| `Detect_CastToEnum_ConstantInvalidValue_SwitchArm` | Ensure that SPIRE016 IS triggered when a switch expression arm yields `(StatusNoZero)0`. |

---

## Category D: Integer-to-enum cast — non-constant / unknown value (should fail)

Tests `(MarkedEnum)variable` or `(MarkedEnum)expr` where the value is not known at compile time.
These are always flagged because the value cannot be verified to be a named member.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_CastToEnum_Variable_LocalVar` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)someInt` (local variable) is assigned locally. |
| `Detect_CastToEnum_Variable_ReturnStatement` | Ensure that SPIRE016 IS triggered when a method returns `(StatusNoZero)intParam`. |
| `Detect_CastToEnum_ArithmeticExpr_LocalVar` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)(x + 1)` is used (non-constant arithmetic expression). |
| `Detect_CastToEnum_Variable_MethodArgument` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)someVar` is passed as a method argument. |
| `Detect_CastToEnum_Variable_LambdaBody` | Ensure that SPIRE016 IS triggered when a lambda returns `(StatusNoZero)intParam`. |
| `Detect_CastToEnum_Variable_TernaryExpr` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)someVar` is in a ternary expression. |
| `Detect_CastToEnum_Variable_WithZeroEnum` | Ensure that SPIRE016 IS triggered when `(StatusWithZero)someVar` is used (value unknown, zero member does not help). |
| `Detect_CastToEnum_Variable_StaticFieldInit` | Ensure that SPIRE016 IS triggered when a `static StatusNoZero` field is initialized with `(StatusNoZero)GetRawValue()`. |
| `Detect_CastToEnum_MethodReturn_LocalVar` | Ensure that SPIRE016 IS triggered when the cast source is a method call: `(StatusNoZero)GetValue()`. |
| `Detect_CastToEnum_Variable_AsyncMethod` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)intParam` is used inside an async method body. |
| `Detect_CastToEnum_Variable_SwitchExpressionArm` | Ensure that SPIRE016 IS triggered when a switch expression arm produces `(StatusNoZero)rawValue`. |
| `Detect_CastToEnum_Variable_ForLoop` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)i` is used inside a for loop body. |

---

## Category E: Integer-to-enum cast — constant value IS a named member (should pass)

Tests `(MarkedEnum)N` where N is a compile-time constant that exactly matches a named member's value.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_CastToEnum_ConstantValidValue_LocalVar` | Ensure that SPIRE016 is NOT triggered when `(StatusNoZero)1` is used (`1` matches `Active`). |
| `NoReport_CastToEnum_ConstantValidValue_ReturnStatement` | Ensure that SPIRE016 is NOT triggered when returning `(StatusNoZero)2` (`2` matches `Inactive`). |
| `NoReport_CastToEnum_ConstantValidValue_MethodArgument` | Ensure that SPIRE016 is NOT triggered when `(StatusNoZero)3` is passed as argument (`3` matches `Pending`). |
| `NoReport_CastToEnum_ZeroValue_WithZeroEnum` | Ensure that SPIRE016 is NOT triggered when `(StatusWithZero)0` is used (`0` matches `None`). |
| `NoReport_CastToEnum_ValidValue_FlagsWithZero_One` | Ensure that SPIRE016 is NOT triggered when `(FlagsWithZero)1` is used (`1` matches `Read`). |
| `NoReport_CastToEnum_ValidValue_FlagsWithZero_Zero` | Ensure that SPIRE016 is NOT triggered when `(FlagsWithZero)0` is used (`0` matches `None`). |
| `NoReport_CastToEnum_ValidValue_ColorImplicitZero` | Ensure that SPIRE016 is NOT triggered when `(ColorImplicitZero)0` is used (`0` matches `Red` implicitly). |

---

## Category F: `Unsafe.SkipInit<TEnum>` on [MustBeInit] enum

`Unsafe.SkipInit` leaves the value as whatever garbage memory contained.
Flagged for all [MustBeInit] enums regardless of whether a zero member exists.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SkipInit_NoZeroEnum` | Ensure that SPIRE016 IS triggered when `Unsafe.SkipInit(out StatusNoZero e)` is used. |
| `Detect_SkipInit_WithZeroEnum` | Ensure that SPIRE016 IS triggered when `Unsafe.SkipInit(out StatusWithZero e)` is used (garbage data regardless of zero member). |
| `Detect_SkipInit_ColorImplicitZero` | Ensure that SPIRE016 IS triggered when `Unsafe.SkipInit(out ColorImplicitZero e)` is used. |
| `Detect_SkipInit_FlagsNoZero` | Ensure that SPIRE016 IS triggered when `Unsafe.SkipInit(out FlagsNoZero f)` is used. |
| `Detect_SkipInit_FlagsWithZero` | Ensure that SPIRE016 IS triggered when `Unsafe.SkipInit(out FlagsWithZero f)` is used. |
| `Detect_SkipInit_InLambda` | Ensure that SPIRE016 IS triggered when `Unsafe.SkipInit(out StatusNoZero e)` is inside a lambda body. |
| `Detect_SkipInit_InAsyncMethod` | Ensure that SPIRE016 IS triggered when `Unsafe.SkipInit(out StatusNoZero e)` is inside an async method. |
| `Detect_SkipInit_InStaticMethod` | Ensure that SPIRE016 IS triggered when `Unsafe.SkipInit(out StatusWithZero e)` is in a static method. |
| `Detect_SkipInit_InForLoop` | Ensure that SPIRE016 IS triggered when `Unsafe.SkipInit(out StatusNoZero e)` is inside a for loop body. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SkipInit_PlainEnum` | Ensure that SPIRE016 is NOT triggered when `Unsafe.SkipInit(out PlainEnum e)` is used (enum not marked). |
| `NoReport_SkipInit_BuiltinInt` | Ensure that SPIRE016 is NOT triggered when `Unsafe.SkipInit(out int i)` is used (not an enum). |

---

## Category G: Array allocation (`new T[n]`) on [MustBeInit] enum arrays

Array elements default to `0` on allocation. Flagged when no named member has value 0.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_ArrayAlloc_NoZeroEnum_LocalVar` | Ensure that SPIRE016 IS triggered when `new StatusNoZero[5]` is allocated locally (elements default to 0, which is unnamed). |
| `Detect_ArrayAlloc_NoZeroEnum_ReturnStatement` | Ensure that SPIRE016 IS triggered when `new StatusNoZero[3]` is returned from a method. |
| `Detect_ArrayAlloc_NoZeroEnum_FieldInitializer` | Ensure that SPIRE016 IS triggered when a `StatusNoZero[]` field is initialized with `new StatusNoZero[10]`. |
| `Detect_ArrayAlloc_NoZeroEnum_MethodArgument` | Ensure that SPIRE016 IS triggered when `new StatusNoZero[4]` is passed as a method argument. |
| `Detect_ArrayAlloc_FlagsNoZero_LocalVar` | Ensure that SPIRE016 IS triggered when `new FlagsNoZero[4]` is allocated (no zero member). |
| `Detect_ArrayAlloc_NoZeroEnum_VariableSize` | Ensure that SPIRE016 IS triggered when `new StatusNoZero[n]` with a runtime variable size is used. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_ArrayAlloc_WithZeroEnum` | Ensure that SPIRE016 is NOT triggered when `new StatusWithZero[5]` is allocated (zero member `None=0` exists). |
| `NoReport_ArrayAlloc_ColorImplicitZero` | Ensure that SPIRE016 is NOT triggered when `new ColorImplicitZero[3]` is allocated (implicit zero member). |
| `NoReport_ArrayAlloc_FlagsWithZero` | Ensure that SPIRE016 is NOT triggered when `new FlagsWithZero[2]` is allocated (zero member `None=0` exists). |
| `NoReport_ArrayAlloc_PlainEnum` | Ensure that SPIRE016 is NOT triggered when `new PlainEnum[5]` is allocated (not marked). |
| `NoReport_ArrayAlloc_ZeroSize` | Ensure that SPIRE016 is NOT triggered when `new StatusNoZero[0]` is allocated (zero-size array has no elements). |

---

## Category H: `Array.Clear` and `Span<T>.Clear()` on [MustBeInit] enum collections

Clearing resets elements to `default(T)=0`. Flagged when no named member has value 0.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_ArrayClear1_NoZeroEnum` | Ensure that SPIRE016 IS triggered when `Array.Clear(statusArr)` is called on a `StatusNoZero[]`. |
| `Detect_ArrayClear3_NoZeroEnum` | Ensure that SPIRE016 IS triggered when `Array.Clear(statusArr, 0, statusArr.Length)` (3-arg overload) is called on a `StatusNoZero[]`. |
| `Detect_ArrayClear_FlagsNoZero` | Ensure that SPIRE016 IS triggered when `Array.Clear(flagsArr)` is called on a `FlagsNoZero[]`. |
| `Detect_SpanClear_NoZeroEnum` | Ensure that SPIRE016 IS triggered when `.Clear()` is called on a `Span<StatusNoZero>`. |
| `Detect_ArrayClear_NoZeroEnum_InLambda` | Ensure that SPIRE016 IS triggered when `Array.Clear` on a `StatusNoZero[]` is inside a lambda. |
| `Detect_SpanClear_NoZeroEnum_InForLoop` | Ensure that SPIRE016 IS triggered when `Span<StatusNoZero>.Clear()` is called inside a for loop. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_ArrayClear_WithZeroEnum` | Ensure that SPIRE016 is NOT triggered when `Array.Clear` is called on a `StatusWithZero[]` (zero member exists). |
| `NoReport_ArrayClear_ColorImplicitZero` | Ensure that SPIRE016 is NOT triggered when `Array.Clear` is called on a `ColorImplicitZero[]` (implicit zero member). |
| `NoReport_ArrayClear_PlainEnum` | Ensure that SPIRE016 is NOT triggered when `Array.Clear` is called on a `PlainEnum[]` (not marked). |
| `NoReport_SpanClear_WithZeroEnum` | Ensure that SPIRE016 is NOT triggered when `.Clear()` is called on a `Span<StatusWithZero>`. |

---

## Category I: `Activator.CreateInstance` on [MustBeInit] enum types

`Activator.CreateInstance` returns `default(T)=0` for value types. Flagged when no named member has value 0.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_ActivatorCreateInstance_GenericOverload_NoZero` | Ensure that SPIRE016 IS triggered when `Activator.CreateInstance<StatusNoZero>()` is called. |
| `Detect_ActivatorCreateInstance_TypeOf_NoZero` | Ensure that SPIRE016 IS triggered when `Activator.CreateInstance(typeof(StatusNoZero))` is called. |
| `Detect_ActivatorCreateInstance_FlagsNoZero` | Ensure that SPIRE016 IS triggered when `Activator.CreateInstance<FlagsNoZero>()` is called. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_ActivatorCreateInstance_WithZeroEnum` | Ensure that SPIRE016 is NOT triggered when `Activator.CreateInstance<StatusWithZero>()` is called (zero member exists). |
| `NoReport_ActivatorCreateInstance_ColorImplicitZero` | Ensure that SPIRE016 is NOT triggered when `Activator.CreateInstance<ColorImplicitZero>()` is called (implicit zero member). |
| `NoReport_ActivatorCreateInstance_PlainEnum` | Ensure that SPIRE016 is NOT triggered when `Activator.CreateInstance<PlainEnum>()` is called (not marked). |

---

## Category J: Named member access and inherently safe operations — should pass

Direct use of named enum members and operations that do not produce unnamed values.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_NamedMember_LocalVariable` | Ensure that SPIRE016 is NOT triggered when `StatusNoZero x = StatusNoZero.Active;` is used. |
| `NoReport_NamedMember_ReturnStatement` | Ensure that SPIRE016 is NOT triggered when a method returns `StatusNoZero.Inactive`. |
| `NoReport_NamedMember_MethodArgument` | Ensure that SPIRE016 is NOT triggered when `StatusNoZero.Pending` is passed as a method argument. |
| `NoReport_NamedMember_TernaryExpression` | Ensure that SPIRE016 is NOT triggered when both branches of a ternary expression return named `StatusNoZero` members. |
| `NoReport_NamedMember_ArrayInitializerElement` | Ensure that SPIRE016 is NOT triggered when a `StatusNoZero[]` is initialized with named member elements: `{ StatusNoZero.Active, StatusNoZero.Inactive }`. |
| `NoReport_PlainEnum_Default` | Ensure that SPIRE016 is NOT triggered when `default(PlainEnum)` is used (enum not marked `[MustBeInit]`). |
| `NoReport_PlainEnum_Cast_InvalidValue` | Ensure that SPIRE016 is NOT triggered when `(PlainEnum)999` is used (enum not marked). |
| `NoReport_Equality_DefaultComparison` | Ensure that SPIRE016 is NOT triggered when `someStatus == default` is an equality comparison (checking, not producing a value). |
| `NoReport_IsPattern_Default` | Ensure that SPIRE016 is NOT triggered when `someStatus is default(StatusNoZero)` is a pattern check (not creating a value). |
| `NoReport_EnumParse_Named` | Ensure that SPIRE016 is NOT triggered when `Enum.Parse<StatusNoZero>("Active")` is used (string-based, runtime-validated). |
| `NoReport_EnumTryParse` | Ensure that SPIRE016 is NOT triggered when `Enum.TryParse<StatusNoZero>("Active", out var result)` is used. |
| `NoReport_EnumToInt_Cast` | Ensure that SPIRE016 is NOT triggered when casting from enum to int: `int x = (int)StatusNoZero.Active` (not producing an enum value). |

---

## Category K: Edge cases — nullable enums, out parameters, switch expressions, nested contexts

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_Default_OutParameter` | Ensure that SPIRE016 IS triggered when a method assigns `default` to an `out StatusNoZero` parameter. |
| `Detect_Default_InsideSwitchExpression` | Ensure that SPIRE016 IS triggered when a switch expression's default arm returns `default(StatusNoZero)`. |
| `Detect_CastToEnum_Variable_TupleElement` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)val` is used as an element of a tuple expression. |
| `Detect_CastToEnum_Variable_InsideNestedClass` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)val` is used inside a method of a nested class. |
| `Detect_Default_ExpressionBodiedMethod` | Ensure that SPIRE016 IS triggered when an expression-bodied method `=> default` has return type `StatusNoZero`. |
| `Detect_Default_StaticField` | Ensure that SPIRE016 IS triggered when a static `StatusNoZero` field is initialized with `default`. |
| `Detect_CastToEnum_Variable_ForeachBody` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)rawInt` is used inside a foreach loop body. |
| `Detect_CastToEnum_Variable_WhileLoopBody` | Ensure that SPIRE016 IS triggered when `(StatusNoZero)i` is used inside a while loop body. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_Default_NullableNoZeroEnum` | Ensure that SPIRE016 is NOT triggered when `StatusNoZero? x = default;` is used — `default` for `Nullable<T>` produces `null`, not value 0, so no invalid enum value is created. **(?)** |
| `NoReport_NullableEnum_NullAssignment` | Ensure that SPIRE016 is NOT triggered when `StatusNoZero? x = null;` is used (null is a valid nullable state). |
| `NoReport_PatternMatching_IsNamedMember` | Ensure that SPIRE016 is NOT triggered when `if (someStatus is StatusNoZero.Active)` is a pattern match (not value production). |
| `NoReport_SwitchExpression_AllNamedMembers` | Ensure that SPIRE016 is NOT triggered when all arms of a switch expression on `StatusNoZero` return named members. |
| `NoReport_NamedMember_ExpressionBodiedMethod` | Ensure that SPIRE016 is NOT triggered when an expression-bodied method `=> StatusNoZero.Active` returns a named member. |
| `NoReport_NamedMember_AutoPropertyInitializer` | Ensure that SPIRE016 is NOT triggered when a `StatusNoZero` auto-property is initialized with `StatusNoZero.Active`. |

---

## Ambiguous / (?) Cases — Lead should confirm before test-case-writer proceeds

1. **Nullable enum `default` (`StatusNoZero? x = default`)** — `default` for `StatusNoZero?` is `null`, not value `0`. Assigning `null` to a nullable enum does not produce an invalid enum underlying value. This is tentatively listed as `NoReport_Default_NullableNoZeroEnum` (should_pass). The implementer should verify the `IOperation` type produced for this expression and confirm the intended behavior.

2. **`[Flags]` composite values via cast** — `(FlagsNoZero)3` equals `Read | Write` which is an unnamed composite. The rule spec says `[Flags]` composites are out of scope (needs separate design). Since `FlagsNoZero` lacks a `3`-valued named member, the analyzer would flag it as invalid under the current non-`[Flags]`-aware logic. Category C includes `Detect_CastToEnum_ZeroValue_FlagsNoZero` for value `0` (clearly invalid). A case for composite values like `(FlagsNoZero)3` is intentionally omitted pending separate design.

3. **`(MarkedEnum)N` where N is a named member's underlying value expressed as a `const` field** — e.g., `const int Active = 1; (StatusNoZero)Active`. The constant fold should produce the same result as `(StatusNoZero)1`. Category E should cover this if the implementer supports constant folding; no separate case is added unless needed.
