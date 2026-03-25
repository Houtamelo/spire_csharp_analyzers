# SPIRE008 Test Coverage Matrix

`RuntimeHelpers.GetUninitializedObject(Type)` has exactly one overload. The detection dimensions are:
- Expression context where the call appears
- Struct variant of the target type
- Call form (normal, fully qualified, static import)
- False-positive scenarios (type not flaggable)

---

## Category A: Standard call — expression contexts with `EnforceInitializationStruct`

The core detection path: `RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationStruct))` in various positions.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_LocalVariable` | Ensure that SPIRE008 IS triggered when the result is assigned to a local variable (`var x = RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationStruct))`). |
| `Detect_DiscardAssignment` | Ensure that SPIRE008 IS triggered when the result is discarded (`_ = RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationStruct))`). |
| `Detect_ReturnStatement` | Ensure that SPIRE008 IS triggered when the call is directly returned from a method. |
| `Detect_MethodArgument` | Ensure that SPIRE008 IS triggered when the call is passed as a method argument. |
| `Detect_TernaryExpression` | Ensure that SPIRE008 IS triggered when the call appears in the true branch of a ternary expression. |
| `Detect_CastResult` | Ensure that SPIRE008 IS triggered when the result is immediately cast to the target struct type. |
| `Detect_LambdaBody` | Ensure that SPIRE008 IS triggered when the call is inside a lambda body. |
| `Detect_AsyncMethod` | Ensure that SPIRE008 IS triggered when the call is inside an async method. |
| `Detect_ForeachLoopBody` | Ensure that SPIRE008 IS triggered when the call is inside a foreach loop body. |
| `Detect_WhileLoopBody` | Ensure that SPIRE008 IS triggered when the call is inside a while loop body. |
| `Detect_SwitchExpressionArm` | Ensure that SPIRE008 IS triggered when the call appears in a switch expression arm. |
| `Detect_NullCoalescingExpression` | Ensure that SPIRE008 IS triggered when the call appears as the right-hand side of a null-coalescing expression. |

## Category B: Struct variants

Tests that all relevant `[EnforceInitialization]` struct variants are flagged.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_RecordStruct` | Ensure that SPIRE008 IS triggered when the target type is a `[EnforceInitialization]` record struct (`EnforceInitializationRecordStruct`). |
| `Detect_ReadonlyStruct` | Ensure that SPIRE008 IS triggered when the target type is a `[EnforceInitialization]` readonly struct (`EnforceInitializationReadonlyStruct`). |
| `Detect_ReadonlyRecordStruct` | Ensure that SPIRE008 IS triggered when the target type is a `[EnforceInitialization]` readonly record struct (defined inline in the case file). |
| `Detect_RefStruct` | Ensure that SPIRE008 IS triggered when the target type is a `[EnforceInitialization]` ref struct (defined inline; `GetUninitializedObject` would fail at runtime, but should still be flagged). |

## Category C: Alternative call forms

Tests that detection is not defeated by alternate syntax for the same call.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_FullyQualifiedCall` | Ensure that SPIRE008 IS triggered when called with the fully qualified name `System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationStruct))`. |
| `Detect_StaticImport` | Ensure that SPIRE008 IS triggered when called via `using static System.Runtime.CompilerServices.RuntimeHelpers;` and then `GetUninitializedObject(typeof(EnforceInitializationStruct))`. |
| `Detect_NestedInStaticClass` | Ensure that SPIRE008 IS triggered when the call appears inside a static method of a static class. |
| `Detect_NestedInClass` | Ensure that SPIRE008 IS triggered when the call is made inside a method of a nested class. |

## Category D: Not flagged — unresolvable or non-concrete type argument

The type argument cannot be resolved to a concrete `[EnforceInitialization]` struct.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_TypeVariable_LocalVar` | Ensure that SPIRE008 is NOT triggered when the Type argument is a local variable (`Type t = ...; RuntimeHelpers.GetUninitializedObject(t)`). |
| `NoReport_TypeVariable_FieldValue` | Ensure that SPIRE008 is NOT triggered when the Type argument is a field value (not a `typeof` literal). |
| `NoReport_TypeVariable_MethodReturn` | Ensure that SPIRE008 is NOT triggered when the Type argument comes from a method return value (e.g., `obj.GetType()`). |
| `NoReport_GenericTypeParam_Typeof` | Ensure that SPIRE008 is NOT triggered when `typeof(T)` is used inside a generic method where T is an unconstrained type parameter. |

## Category E: Not flagged — wrong target type (not a [EnforceInitialization] struct)

Tests that the analyzer does not fire on types that are not `[EnforceInitialization]` structs.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_PlainStruct` | Ensure that SPIRE008 is NOT triggered when the target is a plain struct without `[EnforceInitialization]` (`typeof(PlainStruct)`). |
| `NoReport_ClassType` | Ensure that SPIRE008 is NOT triggered when the target is a reference type (`typeof(object)`). |
| `NoReport_BuiltinType_Int` | Ensure that SPIRE008 is NOT triggered when the target is a built-in value type (`typeof(int)`). |
| `NoReport_StringType` | Ensure that SPIRE008 is NOT triggered when the target is `typeof(string)`. |
| `NoReport_EnumType` | Ensure that SPIRE008 is NOT triggered when the target is an enum type (`typeof(MyEnum)`). |
| `NoReport_InterfaceType` | Ensure that SPIRE008 is NOT triggered when the target is an interface type (`typeof(IMyInterface)`). |
| `NoReport_EmptyEnforceInitializationStruct` | Ensure that SPIRE008 is NOT triggered when the target is a fieldless `[EnforceInitialization]` struct (SPIRE002 handles this case). |
