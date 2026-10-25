# Phase 2 consumer coverage matrix: variant payload unions

This matrix defines the consumer work that follows the phase 1 variant-payload
generator. Every analyzer and code-fix case must run the real
`DiscriminatedUnionGenerator` first and then inspect the resulting compilation;
the declarations in the cases use the new public API. These are generator-driver
integration cases, not tests against hand-written stand-ins.

The struct union surface is now:

```cs
[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command Move(Vector2 destination, float speed);
    [Variant] public static partial Command Stop();
}
```

The generated payload types are `CommandMove` and `CommandStop`, and the union
accessors are `move` and `stop`. A variant field is therefore matched through a
qualified union property such as
`{ kind: Command.Kind.Move, move: { destination: var destination, speed: var speed } }`.
The analyzer must not treat `destination` or `speed` as old flat union members.
Payload values can also be consumed directly after an accessor (`command.move`)
and use their generated typed `Deconstruct` method.

The old Layout, boxed strategies, and union-level `(Kind, fields...)`
deconstruction are intentionally absent from these cases. Record-union controls
retain their existing nested-type behavior and are included wherever the
consumer is meant to remain unchanged.

## Exhaustiveness analyzer (`SPIRE009`)

### Struct union pattern forms

#### should_fail

| File name | Description |
|---|---|
| `StructPayloadSwitchExpressionMissingVariant` | `Move` payload property arm plus `Stop` omitted from a switch expression. |
| `StructPayloadSwitchStatementMissingVariant` | A switch statement handles `Move` through its nested payload pattern and omits `Stop`. |
| `StructPayloadFieldlessMissing` | A fielded `Move` arm exists but the fieldless `Stop` arm is missing. |
| `StructPayloadMultiFieldMissing` | A multi-field payload arm handles only one variant of a three-variant union. |
| `StructPayloadQualifiedFieldMissing` | A property pattern mentions `move: { destination: var _ }` but omits a second variant. |
| `StructPayloadGuardedArmMissing` | A complete-looking `move` arm has a `when` guard and must not cover every `Move` value. |
| `StructPayloadOrPatternPartial` | An `or` pattern combines two payload variants but leaves a third variant uncovered. |
| `StructPayloadNestedSwitchMissing` | An inner switch over the same generated union omits a variant while the outer switch is exhaustive. |
| `StructPayloadGenericMissing` | `Result<T>` at namespace scope has a payload arm for `ok` and omits `error`. |
| `StructPayloadReadonlyMissing` | A readonly union uses the new qualified payload property and misses one variant. |
| `StructPayloadAcronymMissing` | `HTTPRequest`/`URL` variants use `httpRequest`/`url` accessors and omit one variant. |

#### should_pass

| File name | Description |
|---|---|
| `PassStructPayloadSwitchExpressionFull` | All fielded and fieldless variants are covered by qualified property patterns. |
| `PassStructPayloadSwitchStatementFull` | A switch statement has one qualified payload arm per variant. |
| `PassStructPayloadKindOnlyFieldless` | Fieldless variants use `{ kind: Command.Kind.Stop }`; no fabricated payload field is required. |
| `PassStructPayloadNestedFieldFull` | Every arm matches `move: { destination: var _, speed: var _ }` with exact payload fields. |
| `PassStructPayloadOrPatternFull` | Explicit `or` arms together cover all variants without a guard. |
| `PassStructPayloadKindComplement` | A specific kind arm plus `not` that same kind explicitly covers the remaining kind domain, following the existing pattern-analysis complement semantics. |
| `PassStructPayloadWildcardFull` | A final discard arm intentionally covers all remaining variants. |
| `PassStructPayloadGenericFull` | A closed `Result<int>` switch covers both generated payload accessors. |
| `PassStructPayloadLambdaFull` | A lambda switch expression covers every variant. |
| `PassStructPayloadLocalFunctionFull` | A local function uses the qualified payload patterns exhaustively. |
| `PassStructPayloadExpressionBodiedFull` | An expression-bodied member matches all variants. |

### Record and unaffected controls

#### should_fail

| File name | Description |
|---|---|
| `RecordNestedVariantMissing` | Existing nested record variant pattern omits one record subtype. |
| `RecordNestedPayloadGuardedMissing` | Existing record subtype arm is guarded and another subtype is missing. |

#### should_pass

| File name | Description |
|---|---|
| `PassRecordNestedVariantsFull` | Existing record union subtype matching remains exhaustive. |
| `PassNonUnionStructUnaffected` | A normal struct with a `switch` receives no union exhaustiveness diagnostic. |
| `PassStructPayloadSwitchWithDiscard` | Generated struct union plus discard remains accepted. |

## Type safety analyzer (`SPIRE011`, `SPIRE012`)

The existing analyzer has two distinct contracts. Property patterns are normal
C# partial patterns: omitting payload properties is valid, and no Spire field
count diagnostic should be invented for an omitted property. An explicit type
pattern is valid when its matched type is the property's actual type; an
incompatible type remains a normal C# type error or a targeted Spire mismatch
only where the analyzer can resolve the generated member. Nullable reference
annotations do not change runtime type identity, and a non-null type pattern on
a nullable reference is a legitimate filtering pattern.

Positional diagnostics apply only when Roslyn has successfully bound a
deconstruction method. A generated payload's own `Deconstruct` has exactly its
payload fields and no `Kind`; wrong positional arity usually fails ordinary C#
binding before the analyzer can inspect it. Keep `SPIRE012` coverage for valid
union deconstruction operations whose bound metadata exposes a count mismatch;
do not turn every ordinary C# arity error into a new Spire policy.

### Nested payload property patterns

#### should_pass

| File name | Description |
|---|---|
| `PassPayloadPropertyPartial` | `{ kind: Command.Kind.Move, move: { speed: var _ } }` omits `destination`; partial property patterns remain valid. |
| `PassPayloadPropertyKindOnly` | `{ kind: Command.Kind.Stop }` matches a fieldless variant without requiring a payload member. |
| `PassPayloadPropertyExactFields` | A nested payload pattern names both fields with `var` bindings. |
| `PassPayloadPropertyCompatibleExplicitType` | `speed: float speed` uses the actual declared field type and remains valid. |
| `PassPayloadPropertyNullableFilter` | A nullable `string?` payload is filtered with a non-null `string` type pattern; annotations do not create a Spire mismatch. |
| `PassPayloadPropertyTupleNames` | Tuple element names do not change property-pattern type compatibility. |
| `PassPayloadPropertyGeneric` | A closed generic payload uses a partial property pattern over its concrete `T` field. |
| `PassPayloadPropertyDiscard` | `_` is accepted for a payload field without a count/type diagnostic. |
| `PassPayloadPropertyNestedUnion` | A nested union payload property is matched using ordinary nested C# property-pattern rules. |

#### should_fail (compiler/type-check controls, not invented SPIRE012 cases)

| File name | Description |
|---|---|
| `CompilerPayloadPropertyUnknownMember` | `move: { extra: var _ }` names no generated payload field; assert the ordinary compiler diagnostic. |
| `CompilerPayloadPropertyIncompatibleType` | A payload property uses an actually incompatible explicit type; assert the compiler/targeted analyzer diagnostic produced by the bound operation. |
| `CompilerPayloadAccessorWrongType` | The accessor itself is matched as the wrong payload struct type; assert ordinary C# type checking. |

### Union positional type checks (`SPIRE011`)

#### should_fail

| File name | Description |
|---|---|
| `TypeMismatch_PositionalSecondField` | A valid union deconstruction pattern binds the second field with an incompatible type. |
| `TypeMismatch_PositionalThirdField` | A three-field union deconstruction pattern has a wrong type only at field three. |
| `TypeMismatch_PositionalMultipleFields` | Two incompatible bound positional fields produce two `SPIRE011` diagnostics. |
| `TypeMismatch_PositionalSwitchStatement` | The mismatch appears in a switch statement case rather than an expression arm. |
| `TypeMismatch_PositionalIfIsPattern` | A valid `is` deconstruction pattern has one incompatible field type. |
| `TypeMismatch_PositionalReferenceAndValue` | A reference field is bound with an unrelated value type. |
| `TypeMismatch_PositionalGenericClosed` | A closed generic union field is bound with a type incompatible with its closed parameter. |
| `TypeMismatch_PositionalReadonlyUnion` | A readonly struct union retains the same positional type check. |

#### should_pass

| File name | Description |
|---|---|
| `Pass_PositionalVarBindings` | `var` bindings infer all payload field types. |
| `Pass_PositionalExactTypes` | Explicit positional types equal the generated deconstruct parameter types. |
| `Pass_PositionalObjectDiscard` | `object`/`object?` bindings remain accepted by the existing boxing-compatible rule. |
| `Pass_PositionalGenericClosed` | A closed generic union uses compatible concrete field types. |
| `Pass_PositionalSwitchStatement` | Correct positional types in a switch statement produce no Spire mismatch. |
| `Pass_PositionalNestedSwitch` | Separate unions in nested switches are analyzed independently. |
| `Pass_PositionalWhenGuard` | A guard does not alter an otherwise compatible positional type check. |
| `Pass_PositionalLambdaAndAsync` | Correct positional types remain valid in lambda and async contexts. |

### Positional field-count diagnostic (`SPIRE012`)

These cases are limited to a deconstruction operation that Roslyn has actually
bound to a union deconstruct method with inspectable parameter metadata. If a
wrong-arity payload deconstruction has no `DeconstructSymbol`, record it as an
ordinary compiler-error control instead of requiring `SPIRE012`.

#### should_fail

| File name | Description |
|---|---|
| `FieldCount_BoundUnionDeconstructMismatch` | A valid bound union deconstruction exposes a parameter/subpattern count mismatch to the analyzer. |
| `FieldCount_BoundMultipleArms` | Two independently bound deconstruction operations expose count mismatches and each location is reported. |
| `Compiler_FieldCount_UnboundPayloadDeconstruct` | A generated payload `(var a, var b)` with invalid arity fails ordinary C# binding; no Spire count policy is asserted. |

#### should_pass

| File name | Description |
|---|---|
| `Pass_PayloadDeconstructExactArity` | Payload accessor deconstruction has exactly the generated payload field count. |
| `Pass_PayloadDeconstructPartialPropertyAlternative` | A partial property pattern avoids positional deconstruction and remains valid. |
| `Pass_FieldlessPayloadNoDeconstructOutputs` | Fieldless payload is matched through its accessor/kind without invented output slots. |

The phase-2 implementation should preserve the distinction between generated
payload values and the enclosing union. Once `var payload = command.move` has
been compiled, `payload.speed` is ordinary payload code and does not receive
union-level `SPIRE011`, `SPIRE012`, `SPIRE013`, or `SPIRE014` treatment. Do not
reuse old flat-member tests as evidence for the new nested payload API.

## Field-access safety (`SPIRE013`, `SPIRE014`)

These cases exercise union accessors, not detached payload values. Once a
payload has been copied to a local (`var payload = command.move`), its fields
are ordinary payload fields and no discriminator guard is needed. Accessing a
union accessor/property must still be tied to the matching `kind` state.

### Wrong variant (`SPIRE013`)

#### should_fail

| File name | Description |
|---|---|
| `WrongAccessorInMoveArm` | `Move` arm reads `command.stop` or a `Stop` payload field. |
| `WrongAccessorInPropertyPattern` | A `Move` discriminator arm selects `stop: { ... }`. |
| `WrongAccessorInNestedSwitch` | Outer `Move` guard contains an inner `Stop` accessor read. |
| `WrongAccessorInLambda` | Lambda captures a union and reads a different variant accessor under a guard. |
| `WrongAccessorMultipleLines` | One method contains two wrong accessor reads; both locations are reported. |
| `WrongAccessorGenericUnion` | `Result<T>` accesses the error payload inside an `ok` arm. |
| `WrongAccessorAcronymVariant` | `httpRequest` is read under a `URL` arm. |

#### should_pass

| File name | Description |
|---|---|
| `PassMatchingAccessorInMoveArm` | `Move` arm reads `command.move` and then its payload fields. |
| `PassMatchingAccessorInEachArm` | Every arm reads its matching accessor. |
| `PassPayloadLocalAfterGuard` | Copying `command.move` to a local and reading the copied payload fields is accepted. |
| `PassAccessorAfterKindSwitch` | A switch on `command.kind` with matching cases permits each accessor. |
| `PassAccessorWithOrGuard` | A combined guard explicitly establishes the matching variant before reading its accessor. |
| `PassAccessorGenericClosedType` | A closed generic union reads its matching payload accessor. |

### Unguarded access (`SPIRE014`)

#### should_fail

| File name | Description |
|---|---|
| `UnguardedAccessorInMethod` | Direct `command.move` read outside a kind/pattern guard. |
| `UnguardedAccessorInExpressionBodiedMember` | Expression-bodied method reads an accessor with no guard. |
| `UnguardedAccessorInTernary` | Accessor appears in one ternary branch without a matching condition. |
| `UnguardedAccessorInLambda` | Lambda body reads a union accessor without discriminating first. |
| `UnguardedAccessorInLocalFunction` | Local function reads the accessor without a proven guard. |
| `UnguardedAccessorAfterMutation` | Guard is followed by union reassignment before the accessor read. |
| `UnguardedAccessorAfterAlias` | A copied union alias is changed after the guard; stale proof must not permit the accessor. |
| `UnguardedMultipleAccessors` | Two different direct accessors are read without any discriminator proof. |

#### should_pass

| File name | Description |
|---|---|
| `PassAccessorInKindCase` | `switch (command.kind)` case `Move` reads `command.move`. |
| `PassAccessorInPropertyArm` | `{ kind: Command.Kind.Move, move: var payload }` binds the active payload. |
| `PassAccessorInNestedPropertyArm` | `{ kind: ..., move: { speed: var speed } }` reads only the matching payload. |
| `PassAccessorInIsPattern` | An `is { kind: ..., move: ... }` pattern proves the accessor. |
| `PassAccessorInGuardedIf` | `if (command.kind == Command.Kind.Move)` protects the matching read. |
| `PassCopiedPayloadFields` | `var payload = command.move; payload.speed` has no union guard requirement. |
| `PassAccessorFactorySuffix` | A factory-conflict accessor such as `run_value` is guarded using its normalized name. |
| `PassReadonlyAccessor` | Readonly union matching/accessor use retains the proof behavior. |

## Switch code fixes

Code-fix tests use `CodeFixTestBase` with a real generator output compilation,
an actual diagnostic, and before/after source pairs. The generated arm must
match the new payload shape. For a fielded variant, use the nested property
form rather than the removed flat form:

```cs
{ kind: Command.Kind.Move, move: { destination: var destination, speed: var speed } }
```

For a fieldless variant, generate `{ kind: Command.Kind.Stop }`. A positional
payload form is allowed only where the resulting nested pattern remains
unambiguous and the generated `Deconstruct` arity is exact.

### Add missing arms (`SPIRE009`)

#### Before/after cases

| Case folder | Required behavior |
|---|---|
| `AddMissingArms_VariantPayload_SingleField_Expression` | Add one missing fielded arm with `{ kind: ..., move: { speed: var speed } }`. |
| `AddMissingArms_VariantPayload_MultiField_Expression` | Add all missing fields inside the nested `move` accessor; no newline is inserted between `{` and `}`. |
| `AddMissingArms_VariantPayload_Fieldless_Expression` | Add a kind-only arm for a fieldless variant. |
| `AddMissingArms_VariantPayload_Mixed` | Existing fielded and fieldless arms remain unchanged; only missing arms are appended. |
| `AddMissingArms_VariantPayload_SwitchStatement` | Add missing `case { kind: ..., move: { ... } }:` labels to a statement where supported. |
| `AddMissingArms_VariantPayload_Generic` | Close generated kind/accessor/payload names correctly for `Result<int>`. |
| `AddMissingArms_VariantPayload_NestedUnion` | Generate the qualified nested accessor and payload fields for `Outer.Command`. |
| `AddMissingArms_VariantPayload_KeywordField` | Escape a generated capture for `@event`/`@class` while preserving the source field name. |
| `AddMissingArms_VariantPayload_NullableReference` | Generated nested field pattern preserves nullable reference syntax and compiles. |
| `AddMissingArms_VariantPayload_Acronym` | Use `httpRequest`/`url` accessor names and apply `_value` only for a factory collision. |
| `AddMissingArms_VariantPayload_ExistingNestedPattern` | Do not duplicate an already-covered nested payload arm. |
| `AddMissingArms_VariantPayload_CaptureCollision` | Select a collision-free capture name in the nested pattern. |

### Fix field type (`SPIRE011`)

#### Before/after cases

| Case folder | Required behavior |
|---|---|
| `FixFieldType_VariantPayload_SingleField` | Replace the wrong nested binding type while preserving `kind` and accessor nesting. |
| `FixFieldType_VariantPayload_MultiField` | Fix only the marked field in a multi-field payload pattern. |
| `FixFieldType_VariantPayload_Positional` | Fix the matching typed payload deconstruction slot. |
| `FixFieldType_VariantPayload_Generic` | Use the closed generic payload field type. |
| `FixFieldType_VariantPayload_NullableReference` | Preserve nullable annotations when fixing the binding. |
| `FixFieldType_VariantPayload_Tuple` | Preserve tuple element names and only correct the incompatible type. |
| `FixFieldType_VariantPayload_KeywordCapture` | Escape keyword capture identifiers in the corrected pattern. |
| `FixFieldType_VariantPayload_Nested` | Correct a nested union payload field without rewriting its parent accessor. |

### Code-fix controls

#### should_pass / control

| Case folder | Required behavior |
|---|---|
| `Control_CodeFix_RecordUnion` | Existing record subtype patterns remain byte/structure equivalent after the fix. |
| `Control_CodeFix_NormalStruct` | No Spire code fix is offered for a non-union switch. |
| `Control_CodeFix_PayloadLocal` | A detached payload field mismatch is handled by ordinary C# typing, not a union property code fix. |
| `Control_CodeFix_NoLegacyFlatRewrite` | A generated fix never rewrites a valid new nested payload pattern into the removed flat representation. |

## JSON consumers

JSON tests compile and load generated converters for both `System.Text.Json` and
`Newtonsoft.Json`. They assert the existing wire format, discriminator defaults,
`JsonName` customization, generic closing, and errors. The converter may use
variant payload accessors internally, but no flat union property is required.

### System.Text.Json

| Case | Required assertion |
|---|---|
| `Stj_FieldedPayloadRoundTrip` | `Move(destination, speed)` serializes and restores kind plus both nested payload fields. |
| `Stj_FieldlessPayloadRoundTrip` | `Stop()` preserves the fieldless variant. |
| `Stj_DiscriminatorDefault` | Existing `"kind"` property and variant name remain unchanged. |
| `Stj_CustomDiscriminator` | Named discriminator remains supported and does not emit `kind`. |
| `Stj_JsonNamePayloadFields` | Variant/field `JsonName` overrides remain unchanged while runtime accessors use generated names. |
| `Stj_GenericPayloadRoundTrip` | Closed `Result<int>` and a reference-type closure round-trip through generated converters. |
| `Stj_NullablePayloadField` | Null and non-null managed payload fields preserve their existing JSON behavior. |
| `Stj_UnknownKindThrows` | Unknown discriminator continues to throw `JsonException`. |
| `Stj_MissingDiscriminatorThrows` | Missing discriminator continues to throw `JsonException`. |
| `Stj_RecordControl` | Existing record union converter behavior remains unchanged. |

### Newtonsoft.Json

| Case | Required assertion |
|---|---|
| `Nsj_FieldedPayloadRoundTrip` | Fielded payload round-trip restores kind and all values. |
| `Nsj_FieldlessPayloadRoundTrip` | Fieldless variant round-trip remains valid. |
| `Nsj_DiscriminatorDefault` | Existing `"kind"` wire property remains unchanged. |
| `Nsj_CustomDiscriminator` | Custom discriminator overrides the default. |
| `Nsj_JsonNamePayloadFields` | Existing variant/field naming customization remains unchanged. |
| `Nsj_GenericPayloadRoundTrip` | Closed generic struct payloads serialize and deserialize correctly. |
| `Nsj_NullablePayloadField` | Null and non-null managed references round-trip without selecting another variant. |
| `Nsj_UnknownKindThrows` | Unknown discriminator remains an error. |
| `Nsj_MissingDiscriminatorThrows` | Missing discriminator remains an error. |
| `Nsj_RecordControl` | Existing record converter behavior remains unchanged. |

## Cross-consumer and regression assertions

These are implementation-level integration assertions attached to the cases
above, not mocks:

| Assertion | Evidence required |
|---|---|
| Generated consumers compile | Output compilation has no missing generated member/type diagnostics caused by the redesign. |
| Payload names are stable | Analyzer and code-fix source uses `UnionVariant` sibling types and normalized accessor names consistently. |
| No flat-member dependency | Removing the old flat generated members does not make valid nested payload patterns fail. |
| Typed payload deconstruction | Positional payload patterns bind exactly the payload fields and never treat `Kind` as a deconstruct output. |
| Generic closing | Namespace-level generic unions and payloads preserve type arguments/constraints in analyzer, code-fix, and JSON paths. |
| Record isolation | Existing record union tests continue to pass without being rewritten to struct payload accessors. |
| JSON format stability | Existing discriminator/variant/field JSON text remains unchanged for equivalent values. |
| Newline formatting | Code-fix output contains `case { kind: ..., move: { ... } }:` with inline empty braces where applicable; no newline is inserted directly after the opening brace or before the closing brace of a generated variant arm. |
| Real integration path | Analyzer/code-fix cases use generator-driver output; JSON cases compile and load generated converters; no mock metadata or hand-written generated members substitute for the generator. |

## Implementation/test runner mapping

| Consumer | Existing infrastructure to extend |
|---|---|
| Exhaustiveness | `GeneratorAnalyzerTestBase` + `ExhaustivenessTests`, with cases under `Exhaustiveness/cases`. |
| Field access | `GeneratorAnalyzerTestBase` + `FieldAccessTests`, with cases under `FieldAccess/cases`. |
| Type safety | `GeneratorAnalyzerTestBase` + `TypeSafetyTests`, with cases under `TypeSafety/cases`. |
| Add missing arms | `CodeFixTestBase` + `AddMissingArmsCodeFix`, before/after folders under `CodeFix/cases`. |
| Fix field type | `CodeFixTestBase` + `FixFieldTypeCodeFix`, before/after folders under `CodeFix/cases`. |
| System.Text.Json | `BehavioralTestBase`, `JsonStjRoundTripTests`, and STJ snapshots. |
| Newtonsoft.Json | `BehavioralTestBase`, `JsonNsjRoundTripTests`, and NSJ snapshots. |

Each consumer implementation should add its cases before changing its adapter.
Run the affected generator/analyzer/code-fix/JSON test project after each
consumer group. Native Unity serialization and drawer tests are phase 3 and do
not belong in this matrix.

The `not` complement case is included only in the explicit kind-domain form
already established by pattern-analysis tests. Do not add a generic `not`
pattern expectation for payload fields unless a concrete pattern-analysis case
proves its exhaustiveness result.
