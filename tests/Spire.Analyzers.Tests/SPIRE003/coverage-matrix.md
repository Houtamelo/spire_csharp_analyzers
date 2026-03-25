# SPIRE003 Test Coverage Matrix

## AST node types

| Syntax node               | Kind                       | When it appears             |
|---------------------------|----------------------------|-----------------------------|
| `DefaultExpressionSyntax` | `DefaultExpression`        | Explicit `default(T)` form  |
| `LiteralExpressionSyntax` | `DefaultLiteralExpression` | Bare `default` literal form |

Both node types must be detected. The two categories below (A and B) cover the same
set of syntactic contexts, one per node type, then Category C covers struct variants,
Category D covers should_pass exclusions.

---

## Category A: Explicit `default(T)` expression — common contexts

### should_fail

| File Name                                         | Description                                                                                                           |
|---------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------|
| `Detect_ExplicitDefault_LocalVariable`            | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is used in a local variable declaration.             |
| `Detect_ExplicitDefault_FieldInitializer`         | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is used as an instance field initializer.            |
| `Detect_ExplicitDefault_StaticFieldInitializer`   | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is used as a static field initializer.               |
| `Detect_ExplicitDefault_AutoPropertyInitializer`  | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is used as an auto-property initializer.             |
| `Detect_ExplicitDefault_ReturnStatement`          | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is used in a return statement.                       |
| `Detect_ExplicitDefault_ExpressionBodiedMethod`   | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is the body of an expression-bodied method.          |
| `Detect_ExplicitDefault_ExpressionBodiedProperty` | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is the body of an expression-bodied property getter. |
| `Detect_ExplicitDefault_MethodArgument`           | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is passed as a positional method argument.           |
| `Detect_ExplicitDefault_ConstructorArgument`      | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is passed as a constructor argument.                 |
| `Detect_ExplicitDefault_TernaryExpression`        | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` appears in the false branch of a ternary expression. |
| `Detect_ExplicitDefault_LambdaBody`               | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is returned from a lambda body.                      |

### should_pass

| File Name                                   | Description                                                                                                                 |
|---------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------|
| `NoReport_ExplicitDefault_PlainStruct`      | Ensure that SPIRE003 is NOT triggered when `default(PlainStruct)` is used and PlainStruct has no [EnforceInitialization] attribute.    |
| `NoReport_ExplicitDefault_BuiltinType`      | Ensure that SPIRE003 is NOT triggered when `default(int)` is used with a built-in numeric type.                             |
| `NoReport_ExplicitDefault_GenericTypeParam` | Ensure that SPIRE003 is NOT triggered when `default(T)` is used in a generic method where T has no [EnforceInitialization] constraint. |

---

## Category B: Bare `default` literal — common contexts

### should_fail

| File Name                                            | Description                                                                                                                                         |
|------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| `Detect_DefaultLiteral_LocalVariable`                | Ensure that SPIRE003 IS triggered when `default` is assigned to a `EnforceInitializationStruct` local variable.                                                  |
| `Detect_DefaultLiteral_FieldInitializer`             | Ensure that SPIRE003 IS triggered when `default` is used as an instance field initializer for a `EnforceInitializationStruct` field.                             |
| `Detect_DefaultLiteral_StaticFieldInitializer`       | Ensure that SPIRE003 IS triggered when `default` is used as a static field initializer for a `EnforceInitializationStruct` field.                                |
| `Detect_DefaultLiteral_AutoPropertyInitializer`      | Ensure that SPIRE003 IS triggered when `default` is used as an auto-property initializer for a `EnforceInitializationStruct` property.                           |
| `Detect_DefaultLiteral_ReturnStatement`              | Ensure that SPIRE003 IS triggered when `default` is returned from a method whose return type is `EnforceInitializationStruct`.                                   |
| `Detect_DefaultLiteral_ExpressionBodiedMethod`       | Ensure that SPIRE003 IS triggered when `default` is the body of an expression-bodied method returning `EnforceInitializationStruct`.                             |
| `Detect_DefaultLiteral_ExpressionBodiedProperty`     | Ensure that SPIRE003 IS triggered when `default` is the body of an expression-bodied property of type `EnforceInitializationStruct`.                             |
| `Detect_DefaultLiteral_BlockBodyGetter`              | Ensure that SPIRE003 IS triggered when `return default` appears inside a block-body property getter of type `EnforceInitializationStruct`.                       |
| `Detect_DefaultLiteral_MethodArgument`               | Ensure that SPIRE003 IS triggered when `default` is passed as a positional argument to a method expecting `EnforceInitializationStruct`.                         |
| `Detect_DefaultLiteral_NamedArgument`                | Ensure that SPIRE003 IS triggered when `default` is passed as a named argument (`s: default`) to a method expecting `EnforceInitializationStruct`.               |
| `Detect_DefaultLiteral_ConstructorArgument`          | Ensure that SPIRE003 IS triggered when `default` is passed as a constructor argument where the parameter is `EnforceInitializationStruct`.                       |
| `Detect_DefaultLiteral_OptionalParameter`            | Ensure that SPIRE003 IS triggered when `default` is used as the default value of an optional parameter of type `EnforceInitializationStruct`.                    |
| `Detect_DefaultLiteral_Assignment`                   | Ensure that SPIRE003 IS triggered when `default` is assigned to a `EnforceInitializationStruct` variable in a standalone assignment statement (not declaration). |
| `Detect_DefaultLiteral_OutParameterAssignment`       | Ensure that SPIRE003 IS triggered when `default` is assigned to an `out EnforceInitializationStruct` parameter inside a method body.                             |
| `Detect_DefaultLiteral_TernaryExpression`            | Ensure that SPIRE003 IS triggered when `default` appears in a branch of a ternary expression of type `EnforceInitializationStruct`.                              |
| `Detect_DefaultLiteral_NullCoalescing`               | Ensure that SPIRE003 IS triggered when `default` appears as the right-hand side of a `??` expression where the left is `EnforceInitializationStruct?`.           |
| `Detect_DefaultLiteral_SwitchExpressionArm`          | Ensure that SPIRE003 IS triggered when `default` appears as the value of a switch expression arm returning `EnforceInitializationStruct`.                        |
| `Detect_DefaultLiteral_LambdaBody`                   | Ensure that SPIRE003 IS triggered when `default` is returned from a lambda whose return type is `EnforceInitializationStruct`.                                   |
| `Detect_DefaultLiteral_AsyncMethodReturn`            | Ensure that SPIRE003 IS triggered when `return default` is used in an `async Task<EnforceInitializationStruct>` method.                                          |
| `Detect_DefaultLiteral_YieldReturn`                  | Ensure that SPIRE003 IS triggered when `yield return default` is used in an `IEnumerable<EnforceInitializationStruct>` iterator method.                          |
| `Detect_DefaultLiteral_ArrayInitializerElement`      | Ensure that SPIRE003 IS triggered when `default` appears as an element in a `EnforceInitializationStruct[]` array initializer.                                   |
| `Detect_DefaultLiteral_CollectionInitializerElement` | Ensure that SPIRE003 IS triggered when `default` appears as an element in a `List<EnforceInitializationStruct>` collection initializer.                          |
| `Detect_DefaultLiteral_TupleElement`                 | Ensure that SPIRE003 IS triggered when `default` appears as a `EnforceInitializationStruct` element in a tuple literal.                                          |
| `Detect_DefaultLiteral_TupleDeconstruction`          | Ensure that SPIRE003 IS triggered when `default` appears in a tuple deconstruction targeting a `EnforceInitializationStruct` variable.                           |

### should_pass

| File Name                             | Description                                                                                                                  |
|---------------------------------------|------------------------------------------------------------------------------------------------------------------------------|
| `NoReport_DefaultLiteral_BuiltinType` | Ensure that SPIRE003 is NOT triggered when `default` is assigned to an `int` variable.                                       |
| `NoReport_DefaultLiteral_PlainStruct` | Ensure that SPIRE003 is NOT triggered when `default` is assigned to a `PlainStruct` variable with no [EnforceInitialization] attribute. |
| `NoReport_DefaultLiteral_StringType`  | Ensure that SPIRE003 is NOT triggered when `default` is assigned to a `string` variable (reference type).                    |

---

## Category C: Struct type variants

Tests that detection works for all [EnforceInitialization] struct variants defined in `_shared.cs`.

### should_fail

| File Name                                       | Description                                                                                                                                        |
|-------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| `Detect_RecordStruct_ExplicitDefault`           | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationRecordStruct)` is used in a local variable.                                                |
| `Detect_RecordStruct_DefaultLiteral`            | Ensure that SPIRE003 IS triggered when `default` is assigned to a `EnforceInitializationRecordStruct` local variable.                                           |
| `Detect_StructWithAutoProperty_ExplicitDefault` | Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStructWithAutoProperty)` is used in a local variable.                                      |
| `Detect_StructWithAutoProperty_DefaultLiteral`  | Ensure that SPIRE003 IS triggered when `default` is assigned to a `EnforceInitializationStructWithAutoProperty` local variable.                                 |
| `Detect_DefaultLiteral_NestedInClass`           | Ensure that SPIRE003 IS triggered when `default` is assigned to a `EnforceInitializationStruct` variable inside a class that is itself nested in another class. |

---

## Category D: Exclusions — comparisons, fieldless types, and generics

These are cases where `default` appears in a syntactic position that the rule must NOT flag.

### should_pass

| File Name                                        | Description                                                                                                                                                               |
|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `NoReport_Equality_DefaultLiteral`               | Ensure that SPIRE003 is NOT triggered when `s == default` compares a `EnforceInitializationStruct` to `default` (equality check, not creation).                                        |
| `NoReport_Inequality_DefaultLiteral`             | Ensure that SPIRE003 is NOT triggered when `s != default` compares a `EnforceInitializationStruct` to `default` (inequality check, not creation).                                      |
| `NoReport_IsPattern_ExplicitDefault`             | Ensure that SPIRE003 is NOT triggered when `s is default(EnforceInitializationStruct)` is used in a constant pattern match (structural check, not creation).                           |
| `NoReport_EmptyEnforceInitializationStruct_ExplicitDefault`   | Ensure that SPIRE003 is NOT triggered when `default(EmptyEnforceInitializationStruct)` is used, because EmptyEnforceInitializationStruct is fieldless so default is its only value.                 |
| `NoReport_EmptyEnforceInitializationStruct_DefaultLiteral`    | Ensure that SPIRE003 is NOT triggered when `default` is assigned to an `EmptyEnforceInitializationStruct` variable, because the type is fieldless.                                     |
| `NoReport_NonAutoPropertyStruct_ExplicitDefault` | Ensure that SPIRE003 is NOT triggered when `default(EnforceInitializationStructWithNonAutoProperty)` is used, because the type has no instance fields (computed property only).        |
| `NoReport_NonAutoPropertyStruct_DefaultLiteral`  | Ensure that SPIRE003 is NOT triggered when `default` is assigned to a `EnforceInitializationStructWithNonAutoProperty` variable, because the type is fieldless.                        |
| `NoReport_GenericTypeParam_ExplicitDefault`      | Ensure that SPIRE003 is NOT triggered when `default(T)` is used in an unconstrained generic method, because T is not a concrete [EnforceInitialization] type at the definition site. |
| `NoReport_GenericTypeParam_DefaultLiteral`       | Ensure that SPIRE003 is NOT triggered when `return default` is used in an unconstrained generic method returning `T`.                                                     |

---

## Open questions for the lead (marked `?`)

| # | Question                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
|---|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1 | `EnforceInitializationStruct? ns = default;` — the `default` here produces `null` (a null `Nullable<EnforceInitializationStruct>`), not an uninitialized `EnforceInitializationStruct`. Should SPIRE003 flag it? The spec only mentions `config ?? default` (right-hand side of `??`, which IS the uninitialized struct). The left-side `EnforceInitializationStruct? ns = default` case is absent from the spec. This matrix assumes it is NOT flagged (since the value is null, not a default struct). If it should be flagged, add a `Detect_DefaultLiteral_NullableVariable` case and remove `(?)`. |
| 2 | `EqualityComparer<EnforceInitializationStruct>.Default` — this is a static property access named `Default`, not a `default` expression. The spec lists it as NOT flagged. No test case is needed since the analyzer targets `DefaultExpressionSyntax` / `DefaultLiteralExpression` only; a property access will never match. Included here for documentation.                                                                                                                                                                                                    |
