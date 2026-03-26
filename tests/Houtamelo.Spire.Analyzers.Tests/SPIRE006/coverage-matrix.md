# SPIRE006 Test Coverage Matrix

Detection mechanism: `IInvocationOperation`. Two code paths:
1. `Array.Clear(array)` (1-param) and `Array.Clear(array, index, length)` (3-param) — extract element type from `IArrayTypeSymbol.ElementType`
2. `Span<T>.Clear()` (instance method) — extract T from `ContainingType.TypeArguments[0]`

---

## Category A: Array.Clear 1-param — struct variants and basic detection

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_ArrayClear1_EnforceInitializationStruct` | Ensure SPIRE006 IS triggered when `Array.Clear(arr)` is called with a `EnforceInitializationStruct[]` argument. |
| `Detect_ArrayClear1_RecordStruct` | Ensure SPIRE006 IS triggered when `Array.Clear(arr)` is called with a `EnforceInitializationRecordStruct[]` argument. |
| `Detect_ArrayClear1_ReadonlyStruct` | Ensure SPIRE006 IS triggered when `Array.Clear(arr)` is called with a `EnforceInitializationReadonlyStruct[]` argument. |
| `Detect_ArrayClear1_MultidimArray` | Ensure SPIRE006 IS triggered when `Array.Clear(arr)` is called with a `EnforceInitializationStruct[,]` (multidimensional) argument. |
### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_ArrayClear1_PlainStruct` | Ensure SPIRE006 is NOT triggered when `Array.Clear(arr)` is called with a `PlainStruct[]` argument. |
| `NoReport_ArrayClear1_BuiltinInt` | Ensure SPIRE006 is NOT triggered when `Array.Clear(arr)` is called with an `int[]` argument. |
| `NoReport_ArrayClear1_ClassArray` | Ensure SPIRE006 is NOT triggered when `Array.Clear(arr)` is called with a reference-type array (`string[]`). |
| `NoReport_ArrayClear1_EmptyEnforceInitializationStruct` | Ensure SPIRE006 is NOT triggered when `Array.Clear(arr)` is called with an `EmptyEnforceInitializationStruct[]` (fieldless [EnforceInitialization] struct). |
| `NoReport_ArrayClear1_ArrayTypedVar` | Ensure SPIRE006 is NOT triggered when `Array.Clear(arr)` is called with a variable typed as `Array` (non-generic, element type unresolvable). |
| `NoReport_ArrayClear1_JaggedArray` | Ensure SPIRE006 is NOT triggered when `Array.Clear(arr)` is called with a `EnforceInitializationStruct[][]` — element type is `EnforceInitializationStruct[]` (reference type, not a struct). |

---

## Category B: Array.Clear 3-param — struct variants and basic detection

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_ArrayClear3_EnforceInitializationStruct` | Ensure SPIRE006 IS triggered when `Array.Clear(arr, 0, n)` is called with a `EnforceInitializationStruct[]` argument. |
| `Detect_ArrayClear3_RecordStruct` | Ensure SPIRE006 IS triggered when `Array.Clear(arr, 0, n)` is called with a `EnforceInitializationRecordStruct[]` argument. |
| `Detect_ArrayClear3_ReadonlyStruct` | Ensure SPIRE006 IS triggered when `Array.Clear(arr, 0, n)` is called with a `EnforceInitializationReadonlyStruct[]` argument. |
| `Detect_ArrayClear3_MultidimArray` | Ensure SPIRE006 IS triggered when `Array.Clear(arr, 0, n)` is called with a `EnforceInitializationStruct[,]` argument. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_ArrayClear3_PlainStruct` | Ensure SPIRE006 is NOT triggered when `Array.Clear(arr, 0, n)` is called with a `PlainStruct[]` argument. |
| `NoReport_ArrayClear3_BuiltinInt` | Ensure SPIRE006 is NOT triggered when `Array.Clear(arr, 0, n)` is called with an `int[]` argument. |
| `NoReport_ArrayClear3_EmptyEnforceInitializationStruct` | Ensure SPIRE006 is NOT triggered when `Array.Clear(arr, 0, n)` is called with an `EmptyEnforceInitializationStruct[]` (fieldless). |
| `NoReport_ArrayClear3_ArrayTypedVar` | Ensure SPIRE006 is NOT triggered when `Array.Clear(arr, 0, 3)` is called with a variable typed as `Array`. |

---

## Category C: Span<T>.Clear() — struct variants and basic detection

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SpanClear_EnforceInitializationStruct` | Ensure SPIRE006 IS triggered when `span.Clear()` is called on a `Span<EnforceInitializationStruct>` local variable. |
| `Detect_SpanClear_RecordStruct` | Ensure SPIRE006 IS triggered when `span.Clear()` is called on a `Span<EnforceInitializationRecordStruct>` local variable. |
| `Detect_SpanClear_ReadonlyStruct` | Ensure SPIRE006 IS triggered when `span.Clear()` is called on a `Span<EnforceInitializationReadonlyStruct>` local variable. |
| `Detect_SpanClear_Parameter` | Ensure SPIRE006 IS triggered when `span.Clear()` is called on a `Span<EnforceInitializationStruct>` method parameter. |
| `Detect_SpanClear_AsSpanChain` | Ensure SPIRE006 IS triggered when `arr.AsSpan().Clear()` is called where `arr` is `EnforceInitializationStruct[]` (chained call). |
| `Detect_SpanClear_ArraySegmentAsSpan` | Ensure SPIRE006 IS triggered when `((Span<EnforceInitializationStruct>)segment).Clear()` is called via explicit cast to `Span<EnforceInitializationStruct>`. |
| `Detect_SpanClear_ViaMemory` | Ensure SPIRE006 IS triggered when `memory.Span.Clear()` is called where `memory` is `Memory<EnforceInitializationStruct>` — `.Span` returns `Span<T>`, then `.Clear()` is the detected method. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SpanClear_PlainStruct` | Ensure SPIRE006 is NOT triggered when `span.Clear()` is called on a `Span<PlainStruct>`. |
| `NoReport_SpanClear_BuiltinInt` | Ensure SPIRE006 is NOT triggered when `span.Clear()` is called on a `Span<int>`. |
| `NoReport_SpanClear_EmptyEnforceInitializationStruct` | Ensure SPIRE006 is NOT triggered when `span.Clear()` is called on a `Span<EmptyEnforceInitializationStruct>` (fieldless). |
| `NoReport_SpanClear_GenericTypeParam` | Ensure SPIRE006 is NOT triggered when `span.Clear()` is called inside a generic method on `Span<T>` where T is an unresolved type parameter. |

---

## Category D: Syntactic expression contexts — Array.Clear

These cases verify detection fires regardless of where the Clear call appears in an expression.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_ArrayClear1_InForLoop` | Ensure SPIRE006 IS triggered when `Array.Clear(arr)` appears inside a `for` loop body. |
| `Detect_ArrayClear1_InForeachLoop` | Ensure SPIRE006 IS triggered when `Array.Clear(arr)` appears inside a `foreach` loop body. |
| `Detect_ArrayClear1_InWhileLoop` | Ensure SPIRE006 IS triggered when `Array.Clear(arr)` appears inside a `while` loop body. |
| `Detect_ArrayClear1_InLambda` | Ensure SPIRE006 IS triggered when `Array.Clear(arr)` appears inside a lambda body (`Action a = () => Array.Clear(arr)`). |
| `Detect_ArrayClear1_InAsyncMethod` | Ensure SPIRE006 IS triggered when `Array.Clear(arr)` appears in an `async` method body. |
| `Detect_ArrayClear1_InStaticMethod` | Ensure SPIRE006 IS triggered when `Array.Clear(arr)` appears in a `static` method. |
| `Detect_ArrayClear1_InNestedType` | Ensure SPIRE006 IS triggered when `Array.Clear(arr)` appears in a method of a nested class/struct. |

---

## Category E: Syntactic expression contexts — Span<T>.Clear()

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SpanClear_InForLoop` | Ensure SPIRE006 IS triggered when `span.Clear()` appears inside a `for` loop body. |
| `Detect_SpanClear_InForeachLoop` | Ensure SPIRE006 IS triggered when `span.Clear()` appears inside a `foreach` loop body. |
| `Detect_SpanClear_InWhileLoop` | Ensure SPIRE006 IS triggered when `span.Clear()` appears inside a `while` loop body. |
| `Detect_SpanClear_InLambda` | Ensure SPIRE006 IS triggered when `span.Clear()` appears inside a lambda/delegate body. |
| `Detect_SpanClear_InAsyncMethod` | Ensure SPIRE006 IS triggered when `span.Clear()` appears in an `async` method body. |
| `Detect_SpanClear_InStaticMethod` | Ensure SPIRE006 IS triggered when `span.Clear()` appears in a `static` method. |
| `Detect_SpanClear_InNestedType` | Ensure SPIRE006 IS triggered when `span.Clear()` appears in a method of a nested class. |

---

## Category F: NoReport — out-of-scope Clear() methods

These cases confirm that similarly-named Clear() methods on other types are NOT flagged.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_ListClear_EnforceInitializationStruct` | Ensure SPIRE006 is NOT triggered when `list.Clear()` is called on a `List<EnforceInitializationStruct>` (removes elements, does not zero). |
| `NoReport_DictionaryClear_EnforceInitializationStruct` | Ensure SPIRE006 is NOT triggered when `dict.Clear()` is called on a `Dictionary<int, EnforceInitializationStruct>`. |
| `NoReport_CustomClear_EnforceInitializationStruct` | Ensure SPIRE006 is NOT triggered when a user-defined `void Clear()` method is called on a custom type that holds `EnforceInitializationStruct`. |

---

## Category G: NoReport — type boundary edge cases

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_ArrayClear1_InterfaceTypedVar` | Ensure SPIRE006 is NOT triggered when the argument to `Array.Clear` is typed as an interface (non-array static type). |
| `NoReport_SpanClear_GenericConstrainedToStruct` | Ensure SPIRE006 is NOT triggered when `span.Clear()` is called inside a generic method `where T : struct` — T is still unresolved. |
| `NoReport_ArrayClear1_NullableAnnotation` | Ensure SPIRE006 is NOT triggered when `Array.Clear(arr)` is called with a `PlainStruct?[]` — nullable annotation does not make a plain struct into a EnforceInitialization struct. |

---

## Category H: NoReport — fieldless [EnforceInitialization] struct across all three call forms

(Fieldless check is critical per rule spec — deserves its own dedicated category.)

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_Fieldless_ArrayClear1` | Ensure SPIRE006 is NOT triggered for `Array.Clear(arr)` when element type is `EmptyEnforceInitializationStruct` (fieldless, [EnforceInitialization]). |
| `NoReport_Fieldless_ArrayClear3` | Ensure SPIRE006 is NOT triggered for `Array.Clear(arr, 0, n)` when element type is `EmptyEnforceInitializationStruct`. |
| `NoReport_Fieldless_SpanClear` | Ensure SPIRE006 is NOT triggered for `span.Clear()` when span is `Span<EmptyEnforceInitializationStruct>`. |
| `NoReport_Fieldless_AsSpanClear` | Ensure SPIRE006 is NOT triggered for `arr.AsSpan().Clear()` when `arr` is `EmptyEnforceInitializationStruct[]`. |
