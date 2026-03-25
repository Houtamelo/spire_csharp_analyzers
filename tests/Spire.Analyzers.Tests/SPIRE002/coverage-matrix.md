# SPIRE002 Test Coverage Matrix

## Category A: Empty and no-member struct/record struct

These are the most fundamental triggering cases — the type has literally no members.

### should_fail

| File Name                            | Description                                                                                                                                |
|--------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| `Detect_EmptyBody_Struct`            | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a plain struct with an empty body.                                     |
| `Detect_EmptyBody_RecordStruct`      | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to an empty record struct with no positional parameters (semicolon form). |
| `Detect_EmptyBody_ReadonlyStruct`    | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a `readonly struct` with an empty body.                                |
| `Detect_EmptyBody_RefStruct`         | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a `ref struct` with an empty body.                                     |
| `Detect_EmptyBody_ReadonlyRefStruct` | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a `readonly ref struct` with an empty body.                            |

### should_pass

| File Name                        | Description                                                                                            |
|----------------------------------|--------------------------------------------------------------------------------------------------------|
| `NoReport_EmptyBody_NoAttribute` | Ensure that SPIRE002 is NOT triggered when a struct has an empty body but no `[EnforceInitialization]` attribute. |

## Category B: Non-auto properties only (no backing field)

Non-auto (computed) properties do not generate backing fields; the struct is still fieldless.

### should_fail

| File Name                                   | Description                                                                                                                                                                                      |
|---------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Detect_NonAutoProperty_ExpressionBody`     | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct whose only member is an expression-bodied property (`int Value => 42;`).                                            |
| `Detect_NonAutoProperty_GetOnlyBlock`       | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct whose only member is a get-only property with block accessor (`int Value { get => 42; }`).                          |
| `Detect_NonAutoProperty_GetSetBlock`        | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct whose only member is a non-auto get/set property with block accessors (`int Value { get { return 42; } set { } }`). |
| `Detect_NonAutoProperty_MultipleProperties` | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct with multiple non-auto properties (all expression-bodied) and no instance fields.                                   |

### should_pass

| File Name                        | Description                                                                                                                                                                          |
|----------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `NoReport_AutoProperty_GetSet`   | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a struct with a standard auto-property (`int Value { get; set; }`), because it generates a backing field.    |
| `NoReport_AutoProperty_GetOnly`  | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a struct with a get-only auto-property (`int Value { get; }`), because it generates a backing field.         |
| `NoReport_AutoProperty_InitOnly` | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a struct with an init-only auto-property (`int Value { get; init; }`), because it generates a backing field. |

## Category C: Static-only members (static fields, constants, static properties)

Static members do not count as instance fields; the struct is still fieldless from the analyzer's perspective.

### should_fail

| File Name                            | Description                                                                                                                                          |
|--------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Detect_StaticFieldOnly_Struct`      | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct containing only a static field.                                         |
| `Detect_ConstantOnly_Struct`         | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct containing only a `const` field.                                        |
| `Detect_StaticPropertyOnly_Struct`   | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct containing only a static auto-property.                                 |
| `Detect_StaticAndConstOnly_Struct`   | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct containing both a static field and a constant (but no instance fields). |
| `Detect_MultipleStaticFields_Struct` | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct with multiple static fields and no instance fields.                     |

### should_pass

| File Name                                | Description                                                                                                                                                      |
|------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `NoReport_InstanceAndStaticField_Struct` | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a struct that has both an instance field and a static field (instance field is present). |
| `NoReport_InstanceFieldOnly_Struct`      | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a struct with a single explicit instance field.                                          |

## Category D: Methods, indexers, and other non-field members only

Methods and indexers do not contribute instance fields.

### should_fail

| File Name                        | Description                                                                                                                                                                       |
|----------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Detect_MethodsOnly_Struct`      | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct containing only instance methods and no fields.                                                      |
| `Detect_IndexerOnly_Struct`      | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct containing only an indexer and no fields.                                                            |
| `Detect_StaticMethodOnly_Struct` | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct containing only a static method and no fields.                                                       |
| `Detect_ConstructorOnly_Struct`  | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct that has only a parameterless constructor and no fields (the constructor does not introduce fields). |

### should_pass

_No should_pass cases unique to this category — covered by Category C and E._

## Category E: Record struct variants (fieldless)

Record structs can also be fieldless — no positional parameters, no instance fields.

### should_fail

| File Name                                 | Description                                                                                                                                  |
|-------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------|
| `Detect_RecordStruct_StaticFieldOnly`     | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a record struct containing only a static field.                          |
| `Detect_RecordStruct_NonAutoPropertyOnly` | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a record struct containing only a non-auto property (expression-bodied). |
| `Detect_RecordStruct_ConstantOnly`        | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a record struct containing only a constant.                              |

### should_pass

| File Name                                     | Description                                                                                                                                                |
|-----------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `NoReport_RecordStruct_PositionalParam`       | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a record struct with a positional parameter, because it generates a backing field. |
| `NoReport_RecordStruct_ExplicitInstanceField` | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a record struct that explicitly declares an instance field in its body.            |
| `NoReport_RecordStruct_AutoPropertyInBody`    | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a record struct whose body contains an auto-property declaration.                  |

## Category F: Struct type variants with fields (struct modifiers)

Verify that the should_pass condition holds across all struct modifiers.

### should_fail

| File Name                         | Description                                                                                                                                                                             |
|-----------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Detect_ReadonlyStruct_EmptyBody` | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a `readonly struct` with an empty body. (Duplicate of Category A to confirm no modifier interferes with detection.) |

### should_pass

| File Name                              | Description                                                                                                                       |
|----------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------|
| `NoReport_ReadonlyStruct_WithField`    | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a `readonly struct` that has a `readonly` instance field. |
| `NoReport_RefStruct_WithField`         | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a `ref struct` that has an instance field.                |
| `NoReport_ReadonlyRefStruct_WithField` | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a `readonly ref struct` that has an instance field.       |

## Category G: Generics

Generic structs should follow the same rules.

### should_fail

| File Name                                  | Description                                                                                                                           |
|--------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| `Detect_GenericStruct_EmptyBody`           | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a generic struct `S<T>` with no instance fields.                  |
| `Detect_GenericStruct_NonAutoPropertyOnly` | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a generic struct `S<T>` whose only member is a non-auto property. |

### should_pass

| File Name                                   | Description                                                                                                                                |
|---------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| `NoReport_GenericStruct_WithTypeParamField` | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a generic struct `S<T>` that has an instance field of type `T`.    |
| `NoReport_GenericStruct_WithConcreteField`  | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a generic struct `S<T>` that has an explicit `int` instance field. |

## Category H: Nested types

Structs defined inside classes or other structs should be analyzed correctly.

### should_fail

| File Name                           | Description                                                                                                                                             |
|-------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Detect_NestedInClass_EmptyStruct`  | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct nested inside a class and that struct has no instance fields.              |
| `Detect_NestedInStruct_EmptyStruct` | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a struct nested inside another struct and that inner struct has no instance fields. |

### should_pass

| File Name                          | Description                                                                                                                                   |
|------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------|
| `NoReport_NestedInClass_WithField` | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a struct nested inside a class and that struct has an instance field. |

## Category I: Partial structs

A partial struct defined across two declarations — the attribute appears on one part, field (if any) on the other.

### should_fail

| File Name                                 | Description                                                                                                                                        |
|-------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| `Detect_PartialStruct_BothPartsFieldless` | Ensure that SPIRE002 IS triggered when `[EnforceInitialization]` is applied to a partial struct and neither partial declaration contains any instance fields. |

### should_pass

| File Name                                 | Description                                                                                                                                                                                             |
|-------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `NoReport_PartialStruct_FieldInOtherPart` | Ensure that SPIRE002 is NOT triggered when `[EnforceInitialization]` is applied to a partial struct whose instance field is declared in a different partial declaration (the combined type has an instance field). |
