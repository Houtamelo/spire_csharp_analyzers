# SPIRE016 Code Fix Coverage Matrix

## Fix description

Replaces a non-constant integer-to-enum or enum-to-enum cast that triggers SPIRE016 with a safe
`SpireEnum<TEnum>.From(value)` call (non-flags) or `SpireEnum<TEnum>.FromFlags(value)` call (flags).
The fix applies only when the diagnostic's `IsConstant` property is `"false"`.

`_shared.cs` defines:
- `StatusNoZero` (`[EnforceInitialization]` enum, no zero member: Active=1, Inactive=2, Pending=3)
- `FlagsNoZero` (`[EnforceInitialization]` `[Flags]` enum, no zero member: Read=1, Write=2, Execute=4)

---

## Already covered (do NOT add duplicates)

| Case Name | Description |
|-----------|-------------|
| `NonFlags_LocalVar` | `(StatusNoZero)v` in local variable initializer → `SpireEnum<StatusNoZero>.From(v)` |
| `NonFlags_ReturnStatement` | `(StatusNoZero)v` as return expression → `SpireEnum<StatusNoZero>.From(v)` |
| `NonFlags_MethodArgument` | `(StatusNoZero)v` as method argument → `SpireEnum<StatusNoZero>.From(v)` |
| `Flags_LocalVar` | `(FlagsNoZero)v` in local variable initializer → `SpireEnum<FlagsNoZero>.FromFlags(v)` |

---

## Category A: Non-flags — expression context variations

These cases confirm the fix applies correctly regardless of where the cast expression appears.

| Case Name | Description |
|-----------|-------------|
| `NonFlags_TernaryConditionBranch` | before: `(StatusNoZero)v` as the false-branch of a ternary `condition ? StatusNoZero.Active : (StatusNoZero)v`. after: false-branch replaced with `SpireEnum<StatusNoZero>.From(v)`. |
| `NonFlags_SwitchExpressionArm` | before: `(StatusNoZero)rawValue` as the default arm of a switch expression. after: `SpireEnum<StatusNoZero>.From(rawValue)`. |
| `NonFlags_LambdaBody` | before: lambda `() => (StatusNoZero)intParam` captured from outer scope. after: `() => SpireEnum<StatusNoZero>.From(intParam)`. |
| `NonFlags_ExpressionBodiedMethod` | before: expression-bodied method `=> (StatusNoZero)param`. after: `=> SpireEnum<StatusNoZero>.From(param)`. |
| `NonFlags_TupleElement` | before: `((StatusNoZero)val, "label")` — cast is a tuple element. after: `(SpireEnum<StatusNoZero>.From(val), "label")`. |
| `NonFlags_PropertyGetter` | before: a property with a block getter that returns `(StatusNoZero)_rawValue`. after: returns `SpireEnum<StatusNoZero>.From(_rawValue)`. |
| `NonFlags_AsyncMethodReturn` | before: `async Task<StatusNoZero>` method that returns `(StatusNoZero)intParam` after `await Task.Yield()`. after: `SpireEnum<StatusNoZero>.From(intParam)`. |
| `NonFlags_LocalFunction` | before: a local function inside a method that returns `(StatusNoZero)v`. after: `SpireEnum<StatusNoZero>.From(v)`. |
| `NonFlags_YieldReturn` | before: iterator method `IEnumerable<StatusNoZero>` that `yield return (StatusNoZero)v`. after: `yield return SpireEnum<StatusNoZero>.From(v)`. |
| `NonFlags_Nested_InsideArithmeticCast` | before: `(StatusNoZero)(x + 1)` — source is a non-constant arithmetic expression. after: `SpireEnum<StatusNoZero>.From(x + 1)`. |
| `NonFlags_ForeachBody` | before: `(StatusNoZero)rawInt` inside a foreach body assigned to a local. after: `SpireEnum<StatusNoZero>.From(rawInt)`. |

---

## Category B: Flags — expression context variations

These cases confirm `FromFlags` is chosen correctly for `[Flags]` enums.

| Case Name | Description |
|-----------|-------------|
| `Flags_ReturnStatement` | before: method returns `(FlagsNoZero)v`. after: `SpireEnum<FlagsNoZero>.FromFlags(v)`. |
| `Flags_MethodArgument` | before: `(FlagsNoZero)v` passed as method argument. after: `SpireEnum<FlagsNoZero>.FromFlags(v)`. |
| `Flags_TernaryConditionBranch` | before: `condition ? FlagsNoZero.Read : (FlagsNoZero)v` — cast is the false-branch. after: `SpireEnum<FlagsNoZero>.FromFlags(v)`. |
| `Flags_SwitchExpressionArm` | before: switch expression default arm is `(FlagsNoZero)rawValue`. after: `SpireEnum<FlagsNoZero>.FromFlags(rawValue)`. |
| `Flags_LambdaBody` | before: lambda returns `(FlagsNoZero)intParam`. after: `SpireEnum<FlagsNoZero>.FromFlags(intParam)`. |
| `Flags_ExpressionBodiedMethod` | before: `=> (FlagsNoZero)param`. after: `=> SpireEnum<FlagsNoZero>.FromFlags(param)`. |
| `Flags_AsyncMethodReturn` | before: async method returns `(FlagsNoZero)intParam` after `await Task.Yield()`. after: `SpireEnum<FlagsNoZero>.FromFlags(intParam)`. |

---

## Category C: Source integer type variations (non-flags)

These cases verify the fix correctly preserves the source expression regardless of the integer type of the cast operand. The `From` overloads in `SpireEnum<T>` accept all integer types.

| Case Name | Description |
|-----------|-------------|
| `NonFlags_Source_LongVariable` | before: `(StatusNoZero)longVar` where `longVar` is `long`. after: `SpireEnum<StatusNoZero>.From(longVar)`. |
| `NonFlags_Source_ByteVariable` | before: `(StatusNoZero)byteVar` where `byteVar` is `byte`. after: `SpireEnum<StatusNoZero>.From(byteVar)`. |
| `NonFlags_Source_ShortVariable` | before: `(StatusNoZero)shortVar` where `shortVar` is `short`. after: `SpireEnum<StatusNoZero>.From(shortVar)`. |
| `NonFlags_Source_UIntVariable` | before: `(StatusNoZero)uintVar` where `uintVar` is `uint`. after: `SpireEnum<StatusNoZero>.From(uintVar)`. |
| `NonFlags_Source_MethodReturnValue` | before: `(StatusNoZero)GetValue()` where `GetValue()` returns `int`. after: `SpireEnum<StatusNoZero>.From(GetValue())`. |
| `NonFlags_Source_Parameter` | before: method parameter `int p` — `(StatusNoZero)p`. after: `SpireEnum<StatusNoZero>.From(p)`. |
| `NonFlags_Source_PropertyAccess` | before: `(StatusNoZero)obj.RawValue` where `RawValue` is an `int` property. after: `SpireEnum<StatusNoZero>.From(obj.RawValue)`. |

---

## Category D: Enum-to-enum cast source (non-flags)

The analyzer also fires when the source of the cast is another enum type (not an integer). The fix should wrap the entire cast expression with `From`.

| Case Name | Description |
|-----------|-------------|
| `NonFlags_EnumSource_LocalVar` | before: `StatusNoZero s = (StatusNoZero)plainEnumVar` where `plainEnumVar` is `PlainEnum`. after: `SpireEnum<StatusNoZero>.From((int)plainEnumVar)` (?) — the entire operand including the source enum variable. Note: the exact wrapping behavior depends on what the fix produces. If the fix simply wraps the original cast expression's operand directly, it may produce `SpireEnum<StatusNoZero>.From(plainEnumVar)`. The actual after code should match what the fix emits. |
| `NonFlags_EnumSource_ReturnStatement` | before: method returns `(StatusNoZero)plainEnum` where `plainEnum` is a `PlainEnum` parameter. after: `SpireEnum<StatusNoZero>.From(plainEnum)` or equivalent. |
| `NonFlags_EnumSource_MethodArgument` | before: `Accept((StatusNoZero)plainEnum)`. after: `Accept(SpireEnum<StatusNoZero>.From(plainEnum))` or equivalent. |

---

## Category E: Nested / compound expression contexts

These verify the fix replaces only the cast node and preserves surrounding syntax.

| Case Name | Description |
|-----------|-------------|
| `NonFlags_InsideConditionalAccess` | before: cast result used in null-conditional context `GetBox()?.Process((StatusNoZero)v)`. after: `GetBox()?.Process(SpireEnum<StatusNoZero>.From(v))`. |
| `NonFlags_InsideBinaryExpression` | before: cast is one operand of a binary expression, e.g., condition `(StatusNoZero)v == StatusNoZero.Active`. after: `SpireEnum<StatusNoZero>.From(v) == StatusNoZero.Active`. |
| `NonFlags_InsideObjectInitializer` | before: `new Container { Status = (StatusNoZero)v }`. after: `new Container { Status = SpireEnum<StatusNoZero>.From(v) }`. |
| `NonFlags_MultipleFixesInOneFile` | before: two separate casts in the same file: `(StatusNoZero)v1` and `(StatusNoZero)v2`. after: applying the fix to the first diagnostic fixes only the first cast; the second remains. This tests that the fix is scoped to a single diagnostic location. |
| `NonFlags_InsideInterpolatedString` | before: `$"status={(StatusNoZero)v}"` — cast inside a string interpolation. after: `$"status={SpireEnum<StatusNoZero>.From(v)}"`. |
| `NonFlags_InsideCollectionExpression` | before: `StatusNoZero[] arr = [(StatusNoZero)v]` — cast inside a collection expression. after: `StatusNoZero[] arr = [SpireEnum<StatusNoZero>.From(v)]`. |

---

## Category F: Static field and auto-property initializer contexts

| Case Name | Description |
|-----------|-------------|
| `NonFlags_StaticFieldInitializer` | before: `public static StatusNoZero Field = (StatusNoZero)GetValue();` where `GetValue()` is a static method. after: `SpireEnum<StatusNoZero>.From(GetValue())`. |
| `NonFlags_InstanceFieldInitializer` | before: instance field `public StatusNoZero Field = (StatusNoZero)_rawValue;` where `_rawValue` is another field. after: `SpireEnum<StatusNoZero>.From(_rawValue)`. |

---

## Category G: Struct and nested class contexts

| Case Name | Description |
|-----------|-------------|
| `NonFlags_InsideStruct` | before: cast inside a `struct` method. after: `SpireEnum<StatusNoZero>.From(v)`. Verifies the fix works in value type containers. |
| `NonFlags_InsideNestedClass` | before: cast inside a method of a nested `class` inside another class. after: `SpireEnum<StatusNoZero>.From(v)`. |
| `NonFlags_InsideStaticClass` | before: cast inside a static utility class method. after: `SpireEnum<StatusNoZero>.From(v)`. |

---

## Notes on Category D (enum-to-enum casts)

The `FromEnumCodeFix` spec says it replaces the cast expression with `SpireEnum<T>.From(value)` where `value` is the cast operand. When the operand is itself an enum (e.g., `PlainEnum`), the operand is passed directly into `From`. `SpireEnum<T>.From` accepts integer types, not arbitrary enum types, so the fix may need to insert an intermediate integer cast `(int)plainEnum` or use the operand directly. The exact behavior of the fix implementation determines the correct `after.cs` content. Cases in Category D are marked with `(?)` indicating the lead should verify with the implementer what the correct `after` code is before writing the test case files.

---

## Notes on Category E — MultipleFixesInOneFile

The test runner (`AnalyzerCodeFixTestBase`) applies only the first diagnostic's fix (`fixableDiags[0]`). A case with two casts in the same file will have one fixed and one left unchanged in the `after.cs`. This tests that the fix does not accidentally alter unrelated code.
