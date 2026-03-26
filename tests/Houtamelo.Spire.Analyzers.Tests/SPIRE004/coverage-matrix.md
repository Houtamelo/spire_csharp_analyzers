# SPIRE004 Test Coverage Matrix

**Rule**: `new T()` on [EnforceInitialization] struct without parameterless constructor is equivalent to `default(T)`
**Trigger**: `ObjectCreationExpression` (IOperation) — parameterless call on a [EnforceInitialization] struct that has no user-defined parameterless constructor and at least one field/auto-property without an initializer.
**Two syntactic forms**: `new T()` (explicit empty argument list) and `new T { }` (object initializer, no parens).

---

## Category A: `new T()` explicit syntax — expression contexts

Detection cases using `EnforceInitializationNoCtor` (the canonical [EnforceInitialization] struct with fields and no parameterless ctor). One case per syntactic location where `new T()` can appear.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_NewT_LocalVariable` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is assigned to a local variable. |
| `Detect_NewT_FieldInitializer` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is used as an instance field initializer. |
| `Detect_NewT_StaticFieldInitializer` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is used as a static field initializer. |
| `Detect_NewT_AutoPropertyInitializer` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is used as an auto-property initializer. |
| `Detect_NewT_ReturnStatement` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is used in a return statement. |
| `Detect_NewT_ExpressionBodiedMethod` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is the body of an expression-bodied method. |
| `Detect_NewT_MethodArgument` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is passed as a positional method argument. |
| `Detect_NewT_NamedArgument` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is passed as a named method argument. |
| `Detect_NewT_ConstructorArgument` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is passed as an argument to a constructor. |
| `Detect_NewT_LambdaBody` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` appears in the body of a lambda. |
| `Detect_NewT_TernaryExpression` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` appears as a branch of a ternary expression. |
| `Detect_NewT_NullCoalescing` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` appears as the right operand of a null-coalescing expression. |

---

## Category B: `new T()` — additional expression contexts

Continuation of Category A covering more syntactic positions.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_NewT_AssignmentStatement` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is used in a standalone assignment statement. |
| `Detect_NewT_OutParameterAssignment` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is assigned to an out parameter. |
| `Detect_NewT_AsyncMethodReturn` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is returned from an async method. |
| `Detect_NewT_ForLoopBody` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` appears inside a for loop body. |
| `Detect_NewT_ForeachLoopBody` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` appears inside a foreach loop body. |
| `Detect_NewT_WhileLoopBody` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` appears inside a while loop body. |
| `Detect_NewT_ArrayInitializerElement` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is an element in an array initializer. |
| `Detect_NewT_CollectionInitializerElement` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is added as a collection initializer element. |
| `Detect_NewT_TupleElement` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is used as a tuple element. |
| `Detect_NewT_SwitchExpressionArm` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` appears as the value of a switch expression arm. |
| `Detect_NewT_NestedInClass` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` appears inside a nested class. |
| `Detect_NewT_PropertyGetter` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is returned from a property getter. |
| `Detect_NewT_ExpressionBodiedProperty` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is the body of an expression-bodied property. |

---

## Category C: `new T { }` object initializer syntax

The `new T { }` form (no explicit parentheses) calls the same implicit parameterless constructor and must also be flagged. These cases test the object initializer path distinct from the explicit `new T()` path.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_ObjectInit_LocalVariable` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor { }` (object initializer, no parens) is assigned to a local variable. |
| `Detect_ObjectInit_ReturnStatement` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor { }` is used in a return statement. |
| `Detect_ObjectInit_MethodArgument` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor { }` is passed as a method argument. |
| `Detect_ObjectInit_FieldInitializer` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor { }` is used as a field initializer. |
| `Detect_ObjectInit_LambdaBody` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor { }` appears in a lambda body. |
| `Detect_ObjectInit_WithExplicitParens` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor() { }` (explicit empty parens + empty initializer) is used in a local variable. |
| `Detect_ObjectInit_WithFieldAssignments` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor { Value = 42, Name = "x" }` — object initializer with field assignments still calls the implicit parameterless ctor. |

---

## Category D: Struct variants — fields, auto-properties, and mixed

Testing different struct shapes that trigger the rule. Each covers a type from _shared.cs where `new T()` should be flagged.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_PartialFieldsInitialized_LocalVariable` | Ensure that SPIRE004 IS triggered when using `new EnforceInitializationPartialFieldsInitialized()` — struct with SOME fields having initializers. |
| `Detect_PartialFieldsInitialized_ReturnStatement` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationPartialFieldsInitialized()` is returned from a method. |
| `Detect_AutoPropsNoCtor_LocalVariable` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationAutoPropsNoCtor()` — struct with auto-properties but no parameterless ctor. |
| `Detect_AutoPropsNoCtor_ReturnStatement` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationAutoPropsNoCtor()` is returned from a method. |
| `Detect_AutoPropsPartialInitialized_LocalVariable` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationAutoPropsPartialInitialized()` — struct with SOME auto-props initialized. |
| `Detect_MixedPartialInitialized_LocalVariable` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationMixedPartialInitialized()` — struct with mix of fields/auto-props, not all initialized. |
| `Detect_ReadonlyStruct_LocalVariable` | Ensure that SPIRE004 IS triggered when `new` is used on a readonly [EnforceInitialization] struct without parameterless ctor. |
| `Detect_RefStruct_LocalVariable` | Ensure that SPIRE004 IS triggered when `new` is used on a ref [EnforceInitialization] struct without parameterless ctor. |
| `Detect_ReadonlyRefStruct_LocalVariable` | Ensure that SPIRE004 IS triggered when `new` is used on a readonly ref [EnforceInitialization] struct without parameterless ctor. |

| `Detect_RecordStructWithFields_LocalVariable` | Ensure that SPIRE004 IS triggered when `new EnforceInitializationRecordStructWithFields()` — record struct with regular fields, no parameterless ctor. |

> **Resolved**: Added `EnforceInitializationRecordStructWithFields` (record struct with regular fields, no primary ctor), `EnforceInitializationReadonlyNoCtor`, `EnforceInitializationRefNoCtor`, `EnforceInitializationReadonlyRefNoCtor` to _shared.cs. All struct modifier variants are covered.

---

## Category E: Should NOT flag — struct has a user-defined parameterless constructor

These cases verify no false positives when the struct has explicit initialization.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_WithCtor_LocalVariable` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationWithCtor()` — struct has user-defined parameterless ctor. |
| `NoReport_WithCtor_ReturnStatement` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationWithCtor()` is returned — struct has user-defined parameterless ctor. |
| `NoReport_WithCtor_MethodArgument` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationWithCtor()` is passed as a method argument — struct has user-defined parameterless ctor. |
| `NoReport_WithCtor_FieldInitializer` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationWithCtor()` is a field initializer — struct has user-defined parameterless ctor. |
| `NoReport_WithCtor_LambdaBody` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationWithCtor()` appears in a lambda body — struct has user-defined parameterless ctor. |

---

## Category F: Should NOT flag — all fields/auto-props have initializers

These cases verify no false positives when the compiler generates an initializing constructor.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_AllFieldsInitialized_LocalVariable` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationAllFieldsInitialized()` — all fields have default initializers. |
| `NoReport_AllFieldsInitialized_ReturnStatement` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationAllFieldsInitialized()` is returned. |
| `NoReport_AllFieldsInitialized_MethodArgument` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationAllFieldsInitialized()` is a method argument. |
| `NoReport_AutoPropsAllInitialized_LocalVariable` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationAutoPropsAllInitialized()` — all auto-props initialized. |
| `NoReport_MixedAllInitialized_LocalVariable` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationMixedAllInitialized()` — mix of fields and auto-props, all initialized. |

---

## Category G: Should NOT flag — not a [EnforceInitialization] type

These cases verify the rule only applies to types with the [EnforceInitialization] attribute.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_PlainStruct_LocalVariable` | Ensure that SPIRE004 is NOT triggered when `new PlainStruct()` — struct is not marked [EnforceInitialization]. |
| `NoReport_PlainStruct_ReturnStatement` | Ensure that SPIRE004 is NOT triggered when `new PlainStruct()` is returned — struct is not marked [EnforceInitialization]. |
| `NoReport_PlainStruct_MethodArgument` | Ensure that SPIRE004 is NOT triggered when `new PlainStruct()` is a method argument — struct is not [EnforceInitialization]. |
| `NoReport_BuiltinValueType_LocalVariable` | Ensure that SPIRE004 is NOT triggered when `new int()`, `new DateTime()`, or similar built-in value types are instantiated. |
| `NoReport_ClassType_LocalVariable` | Ensure that SPIRE004 is NOT triggered when `new SomeClass()` — class types are not structs. |

---

## Category H: Should NOT flag — fieldless or computed-only types

These cases verify the rule respects the "no instance fields" exemption.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_EmptyEnforceInitializationStruct_LocalVariable` | Ensure that SPIRE004 is NOT triggered when `new EmptyEnforceInitializationStruct()` — [EnforceInitialization] struct with no fields. |
| `NoReport_EmptyEnforceInitializationStruct_ReturnStatement` | Ensure that SPIRE004 is NOT triggered when `new EmptyEnforceInitializationStruct()` is returned — no fields to initialize. |
| `NoReport_ComputedOnly_LocalVariable` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationComputedOnly()` — [EnforceInitialization] struct with only non-auto (computed) properties. |

---

## Category I: Should NOT flag — parameterized constructor calls

These cases verify that `new T(args)` calls with arguments are never flagged, regardless of the struct type.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_ParameterizedCtor_LocalVariable` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationNoCtor(value, name)` — parameterized constructor is used. |
| `NoReport_ParameterizedCtor_ReturnStatement` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationNoCtor(value, name)` is returned. |
| `NoReport_ParameterizedCtor_MethodArgument` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationNoCtor(value, name)` is passed as a method argument. |
| `NoReport_ParameterizedCtor_LambdaBody` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationNoCtor(value, name)` appears in a lambda. |
| `NoReport_ParameterizedCtor_FieldInitializer` | Ensure that SPIRE004 is NOT triggered when `new EnforceInitializationNoCtor(value, name)` is used as a field initializer. |

---

## Category J: Should NOT flag — generic type parameters and special contexts

Edge cases involving generic type parameters, multiple assignments in one statement, and other boundary conditions.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_GenericTypeParam_NewConstraint` | Ensure that SPIRE004 is NOT triggered when `new T()` is used in a generic method where T has the `new()` constraint but is not a concrete [EnforceInitialization] type. |
| `NoReport_PlainStruct_ObjectInitializerWithFields` | Ensure that SPIRE004 is NOT triggered when `new PlainStruct { Value = 42 }` — plain struct with object initializer. |

---

## Resolved Questions

1. **`new T { Field = value }` with field assignments** — **Flagged.** The implicit parameterless ctor still runs, producing a default instance. Added `Detect_ObjectInit_WithFieldAssignments` to Category C.

2. **Record struct** — **Resolved.** Added `EnforceInitializationRecordStructWithFields` (record struct with regular fields, no primary ctor) to _shared.cs. Added `Detect_RecordStructWithFields_LocalVariable` to Category D.

3. **`readonly struct`, `ref struct`, `readonly ref struct`** — **Included.** Added shared types and detection cases in Category D for all three variants.
