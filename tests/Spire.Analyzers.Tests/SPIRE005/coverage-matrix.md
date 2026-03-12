# SPIRE005 Test Coverage Matrix

## Category A: Generic overload — `Activator.CreateInstance<T>()`

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_GenericOverload_LocalVariable` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitStruct>()` result is assigned to a local variable. |
| `Detect_GenericOverload_ReturnStatement` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitStruct>()` is returned directly. |
| `Detect_GenericOverload_MethodArgument` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitStruct>()` is passed as a method argument. |
| `Detect_GenericOverload_FieldInitializer` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitStruct>()` is used in a static field initializer. |
| `Detect_GenericOverload_LambdaBody` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitStruct>()` is called inside a lambda body. |
| `Detect_GenericOverload_TernaryExpression` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitStruct>()` appears in a ternary expression branch. |
| `Detect_GenericOverload_AsyncMethod` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitStruct>()` is called inside an async method. |
| `Detect_GenericOverload_RecordStruct` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitRecordStruct>()` is used with a [MustBeInit] record struct. |
| `Detect_GenericOverload_ReadonlyStruct` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitReadonlyStruct>()` is used with a [MustBeInit] readonly struct. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_GenericOverload_PlainStruct` | Ensure that SPIRE005 is NOT triggered when `CreateInstance<PlainStruct>()` is used on an unmarked struct. |
| `NoReport_GenericOverload_EmptyMustInitStruct` | Ensure that SPIRE005 is NOT triggered when `CreateInstance<EmptyMustInitStruct>()` is used on a fieldless [MustBeInit] struct. |
| `NoReport_GenericOverload_ClassType` | Ensure that SPIRE005 is NOT triggered when `CreateInstance<string>()` is used on a reference type. |
| `NoReport_GenericOverload_BuiltinType` | Ensure that SPIRE005 is NOT triggered when `CreateInstance<int>()` is used on a built-in value type. |
| `NoReport_GenericOverload_GenericTypeParam` | Ensure that SPIRE005 is NOT triggered when `CreateInstance<T>()` is used where T is an unresolvable generic type parameter. |

## Category B: Type-only overload — `Activator.CreateInstance(Type)`

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_TypeOnly_LocalVariable` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct))` is assigned to a local variable. |
| `Detect_TypeOnly_ReturnStatement` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct))` is returned from a method. |
| `Detect_TypeOnly_MethodArgument` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct))` is passed as a method argument. |
| `Detect_TypeOnly_LambdaBody` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct))` is called inside a lambda body. |
| `Detect_TypeOnly_TernaryExpression` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct))` appears as a ternary branch. |
| `Detect_TypeOnly_RecordStruct` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitRecordStruct))` is used with a [MustBeInit] record struct. |
| `Detect_TypeOnly_ReadonlyStruct` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitReadonlyStruct))` is used with a [MustBeInit] readonly struct. |
| `Detect_TypeOnly_NestedInClass` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct))` is called inside a nested class. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_TypeOnly_PlainStruct` | Ensure that SPIRE005 is NOT triggered when `CreateInstance(typeof(PlainStruct))` is used on an unmarked struct. |
| `NoReport_TypeOnly_EmptyMustInitStruct` | Ensure that SPIRE005 is NOT triggered when `CreateInstance(typeof(EmptyMustInitStruct))` is used on a fieldless [MustBeInit] struct. |
| `NoReport_TypeOnly_ClassType` | Ensure that SPIRE005 is NOT triggered when `CreateInstance(typeof(object))` is used on a class. |
| `NoReport_TypeOnly_BuiltinType` | Ensure that SPIRE005 is NOT triggered when `CreateInstance(typeof(int))` is used on a built-in value type. |
| `NoReport_TypeOnly_TypeVariable` | Ensure that SPIRE005 is NOT triggered when `CreateInstance(someTypeVariable)` is used with a non-static type argument. |

## Category C: NonPublic overload — `Activator.CreateInstance(Type, bool)`

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_NonPublic_TrueFlag` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct), nonPublic: true)` is called. |
| `Detect_NonPublic_FalseFlag` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct), nonPublic: false)` is called. |
| `Detect_NonPublic_RecordStruct` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitRecordStruct), true)` is used with a [MustBeInit] record struct. |
| `Detect_NonPublic_ReturnStatement` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct), true)` is returned from a method. |
| `Detect_NonPublic_LambdaBody` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct), false)` is called inside a lambda body. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_NonPublic_PlainStruct` | Ensure that SPIRE005 is NOT triggered when `CreateInstance(typeof(PlainStruct), true)` is used on an unmarked struct. |
| `NoReport_NonPublic_EmptyMustInitStruct` | Ensure that SPIRE005 is NOT triggered when `CreateInstance(typeof(EmptyMustInitStruct), true)` is used on a fieldless [MustBeInit] struct. |
| `NoReport_NonPublic_TypeVariable` | Ensure that SPIRE005 is NOT triggered when `CreateInstance(someTypeVariable, true)` is used with a non-static type. |

## Category D: Params overload — `Activator.CreateInstance(Type, params object[])` — null/empty args patterns

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_Params_NullArgs` | Ensure that SPIRE005 IS triggered when args is `(object[])null`. |
| `Detect_Params_DefaultArgs` | Ensure that SPIRE005 IS triggered when args is `default(object[])`. |
| `Detect_Params_DefaultLiteralArgs` | Ensure that SPIRE005 IS triggered when args is the bare `default` literal. |
| `Detect_Params_ZeroLengthArray` | Ensure that SPIRE005 IS triggered when args is `new object[0]`. |
| `Detect_Params_EmptyArrayLiteral` | Ensure that SPIRE005 IS triggered when args is `new object[] { }`. |
| `Detect_Params_ArrayEmptyHelper` | Ensure that SPIRE005 IS triggered when args is `Array.Empty<object>()`. |
| `Detect_Params_RecordStruct_NullArgs` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitRecordStruct), (object[])null)` is used with a [MustBeInit] record struct. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_Params_NonEmptyArgs` | Ensure that SPIRE005 is NOT triggered when args contains actual values (`new object[] { 42 }`). |
| `NoReport_Params_VariableArgs` | Ensure that SPIRE005 is NOT triggered when args is a variable (non-static, non-empty cannot be determined). |
| `NoReport_Params_MethodReturnArgs` | Ensure that SPIRE005 is NOT triggered when args comes from a method return value. |
| `NoReport_Params_PlainStruct_NullArgs` | Ensure that SPIRE005 is NOT triggered when `CreateInstance(typeof(PlainStruct), (object[])null)` is used on an unmarked struct. |
| `NoReport_Params_EmptyMustInitStruct_NullArgs` | Ensure that SPIRE005 is NOT triggered when `CreateInstance(typeof(EmptyMustInitStruct), (object[])null)` is used on a fieldless [MustBeInit] struct. |

## Category E: Args+ActivationAttributes overload — `Activator.CreateInstance(Type, object[], object[])`

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_ArgsActivation_BothNull` | Ensure that SPIRE005 IS triggered when both args and activationAttributes are `null`. |
| `Detect_ArgsActivation_ArgsNullAttrsNonNull` | Ensure that SPIRE005 IS triggered when args is null but activationAttributes is non-null. |
| `Detect_ArgsActivation_ArgsEmpty_ZeroLength` | Ensure that SPIRE005 IS triggered when args is `new object[0]`. |
| `Detect_ArgsActivation_ArgsEmpty_Literal` | Ensure that SPIRE005 IS triggered when args is `new object[] { }`. |
| `Detect_ArgsActivation_ArgsArrayEmpty` | Ensure that SPIRE005 IS triggered when args is `Array.Empty<object>()`. |
| `Detect_ArgsActivation_ArgsDefault` | Ensure that SPIRE005 IS triggered when args is `default(object[])`. |
| `Detect_ArgsActivation_RecordStruct` | Ensure that SPIRE005 IS triggered when used with a [MustBeInit] record struct and null args. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_ArgsActivation_NonEmptyArgs` | Ensure that SPIRE005 is NOT triggered when args contains actual values. |
| `NoReport_ArgsActivation_VariableArgs` | Ensure that SPIRE005 is NOT triggered when args is a variable. |
| `NoReport_ArgsActivation_PlainStruct` | Ensure that SPIRE005 is NOT triggered when the target type is an unmarked struct. |
| `NoReport_ArgsActivation_EmptyMustInitStruct` | Ensure that SPIRE005 is NOT triggered when the target type is a fieldless [MustBeInit] struct. |

## Category F: BindingFlags overload — `Activator.CreateInstance(Type, BindingFlags, Binder, object[], CultureInfo)`

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_BindingFlags_NullArgs` | Ensure that SPIRE005 IS triggered when args is `null`. |
| `Detect_BindingFlags_DefaultArgs` | Ensure that SPIRE005 IS triggered when args is `default(object[])`. |
| `Detect_BindingFlags_ZeroLengthArgs` | Ensure that SPIRE005 IS triggered when args is `new object[0]`. |
| `Detect_BindingFlags_EmptyArrayLiteral` | Ensure that SPIRE005 IS triggered when args is `new object[] { }`. |
| `Detect_BindingFlags_ArrayEmptyHelper` | Ensure that SPIRE005 IS triggered when args is `Array.Empty<object>()`. |
| `Detect_BindingFlags_RecordStruct` | Ensure that SPIRE005 IS triggered when used with a [MustBeInit] record struct and null args. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_BindingFlags_NonEmptyArgs` | Ensure that SPIRE005 is NOT triggered when args contains actual constructor arguments. |
| `NoReport_BindingFlags_VariableArgs` | Ensure that SPIRE005 is NOT triggered when args is a variable. |
| `NoReport_BindingFlags_PlainStruct` | Ensure that SPIRE005 is NOT triggered when the target type is an unmarked struct with null args. |
| `NoReport_BindingFlags_EmptyMustInitStruct` | Ensure that SPIRE005 is NOT triggered when the target type is a fieldless [MustBeInit] struct with null args. |

## Category G: Full overload — `Activator.CreateInstance(Type, BindingFlags, Binder, object[], CultureInfo, object[])`

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_FullOverload_NullArgs` | Ensure that SPIRE005 IS triggered when args is `null`. |
| `Detect_FullOverload_DefaultArgs` | Ensure that SPIRE005 IS triggered when args is `default(object[])`. |
| `Detect_FullOverload_ZeroLengthArgs` | Ensure that SPIRE005 IS triggered when args is `new object[0]`. |
| `Detect_FullOverload_EmptyArrayLiteral` | Ensure that SPIRE005 IS triggered when args is `new object[] { }`. |
| `Detect_FullOverload_ArrayEmptyHelper` | Ensure that SPIRE005 IS triggered when args is `Array.Empty<object>()`. |
| `Detect_FullOverload_RecordStruct` | Ensure that SPIRE005 IS triggered when used with a [MustBeInit] record struct and null args. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_FullOverload_NonEmptyArgs` | Ensure that SPIRE005 is NOT triggered when args contains actual constructor arguments. |
| `NoReport_FullOverload_VariableArgs` | Ensure that SPIRE005 is NOT triggered when args is a variable. |
| `NoReport_FullOverload_PlainStruct` | Ensure that SPIRE005 is NOT triggered when the target type is an unmarked struct with null args. |
| `NoReport_FullOverload_EmptyMustInitStruct` | Ensure that SPIRE005 is NOT triggered when the target type is a fieldless [MustBeInit] struct with null args. |

## Category H: Not flagged — plain/unmarked structs and reference types

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_PlainStruct_GenericOverload` | Ensure that SPIRE005 is NOT triggered for `CreateInstance<PlainStruct>()`. |
| `NoReport_PlainStruct_TypeOnly` | Ensure that SPIRE005 is NOT triggered for `CreateInstance(typeof(PlainStruct))`. |
| `NoReport_ClassType_GenericOverload` | Ensure that SPIRE005 is NOT triggered for `CreateInstance<List<int>>()`. |
| `NoReport_ClassType_TypeOnly` | Ensure that SPIRE005 is NOT triggered for `CreateInstance(typeof(object))`. |
| `NoReport_BuiltinInt_GenericOverload` | Ensure that SPIRE005 is NOT triggered for `CreateInstance<int>()`. |
| `NoReport_BuiltinInt_TypeOnly` | Ensure that SPIRE005 is NOT triggered for `CreateInstance(typeof(long))`. |
| `NoReport_StringType_TypeOnly` | Ensure that SPIRE005 is NOT triggered for `CreateInstance(typeof(string))`. |

## Category I: Not flagged — fieldless [MustBeInit] struct

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_EmptyMustInit_GenericOverload` | Ensure that SPIRE005 is NOT triggered for `CreateInstance<EmptyMustInitStruct>()` (fieldless, SPIRE002 handles this). |
| `NoReport_EmptyMustInit_TypeOnly` | Ensure that SPIRE005 is NOT triggered for `CreateInstance(typeof(EmptyMustInitStruct))`. |
| `NoReport_EmptyMustInit_NonPublic` | Ensure that SPIRE005 is NOT triggered for `CreateInstance(typeof(EmptyMustInitStruct), true)`. |
| `NoReport_EmptyMustInit_ParamsNullArgs` | Ensure that SPIRE005 is NOT triggered for `CreateInstance(typeof(EmptyMustInitStruct), (object[])null)`. |

## Category J: Not flagged — unresolvable type (variable, method return, generic type param)

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_TypeVariable_TypeOnly` | Ensure that SPIRE005 is NOT triggered when the Type argument is a local variable (`Type t = ...; Activator.CreateInstance(t)`). |
| `NoReport_TypeVariable_NonPublic` | Ensure that SPIRE005 is NOT triggered when the Type argument is a variable in the nonPublic overload. |
| `NoReport_TypeVariable_ParamsOverload` | Ensure that SPIRE005 is NOT triggered when the Type argument is a variable in the params overload. |
| `NoReport_MethodReturnType_TypeOnly` | Ensure that SPIRE005 is NOT triggered when the type comes from a method return (`GetType()`). |
| `NoReport_GenericTypeParam_GenericOverload` | Ensure that SPIRE005 is NOT triggered when T is an unconstrained generic type parameter. |
| `NoReport_GenericTypeParam_TypeOnly` | Ensure that SPIRE005 is NOT triggered when `typeof(T)` is used with a generic type parameter. |

## Category K: Not flagged — string-based and CreateInstanceFrom overloads

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_StringOverload_AssemblyAndTypeName` | Ensure that SPIRE005 is NOT triggered for `CreateInstance(assemblyName, typeName)` string-based overload. |
| `NoReport_CreateInstanceFrom_StringOverload` | Ensure that SPIRE005 is NOT triggered for `Activator.CreateInstanceFrom(assemblyFile, typeName)`. |

## Category L: Edge cases — expression contexts and call sites

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_GenericOverload_DiscardAssignment` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitStruct>()` result is discarded (`_ = Activator.CreateInstance<MustInitStruct>()`). |
| `Detect_GenericOverload_ForeachLoop` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitStruct>()` is called inside a foreach loop body. |
| `Detect_TypeOnly_WhileLoop` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct))` is called inside a while loop body. |
| `Detect_GenericOverload_NullCoalescing` | Ensure that SPIRE005 IS triggered when `CreateInstance<MustInitStruct>()` appears in a null-coalescing expression. |
| `Detect_TypeOnly_SwitchExpressionArm` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct))` appears in a switch expression arm. |
| `Detect_TypeOnly_NestedInStaticClass` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct))` is called in a static method of a static class. |
| `Detect_Params_NullArgs_LambdaBody` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct), (object[])null)` is inside a lambda body. |
| `Detect_Params_EmptyArgs_TernaryExpression` | Ensure that SPIRE005 IS triggered when `CreateInstance(typeof(MustInitStruct), new object[0])` is a ternary branch. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_TypeOnly_ExpressionBodyMethod` | Ensure that SPIRE005 is NOT triggered when `CreateInstance(typeof(PlainStruct))` is in an expression-bodied method. |
| `NoReport_Params_NonNullVariableArgs_ConditionalContext` | Ensure that SPIRE005 is NOT triggered when args comes from a conditional expression that may be non-empty. |
