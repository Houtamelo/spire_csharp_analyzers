# Variant-payload implicit-conversion coverage matrix

This matrix specifies the generated conversion surface for the variant-payload
union strategy. For every public `[Variant]` factory, the generator emits one
public conversion on the union:

```csharp
public static implicit operator Shape(
    ShapeTypes.Variants.Circle variant)
    => Shape.Circle(variant.radius);
```

The conversion consumes the canonical payload type, preserves declaration
order and values, and delegates to the existing factory so all storage paths
remain shared. The operator is generated on struct unions only. Record unions
already have the ordinary derived-record-to-base-record implicit conversion.

Operators are emitted only when the corresponding variant factory is public.
An internal or private factory must not become publicly constructible through an
implicit conversion. `FormerlySerializedAs` payload types are serialization
compatibility types and never receive a second conversion operator.

## Focused generator-driver cases

These are compile/emit/source-inspection cases, not snapshot directories. The
existing snapshot discovery deliberately skips `variant_payload`; adding
`input.cs`/`output.cs` pairs here would duplicate the behavioral generator
harness.

| Case | Input | Required assertions |
|---|---|---|
| `BasicPublicVariants` | Public `Shape` with `Circle(float radius)`, `Rectangle(float width, float height)`, and `Point()` | Output contains one public operator for each canonical payload. One-field, multi-field, and fieldless operators call the matching factory with the expected arguments. The generated compilation has no errors. |
| `OperatorParameterTypes` | Variants using `int`, `string?`, `Vector3`, and a user struct | Each operator parameter is the exact public payload type under `ShapeTypes.Variants`; no `object`, tuple erasure, or factory-parameter list is substituted. |
| `DeclarationOrderPreserved` | A three-field variant whose names differ from their source order in other variants | The conversion forwards `first`, `second`, `third` in declaration order, including when storage slots are shared or overlaid. Runtime values must remain associated with their original names. |
| `SingleVariant` | One public `Only(int value)` factory | Exactly one operator is emitted; the payload still has the normal public constructor/getter and the union conversion compiles. |
| `ManyVariants` | Eight public variants mixing zero, one, and four fields | Every public variant has exactly one operator, with no duplicate signatures or missing fieldless conversion. |
| `GenericOption` | `Option<T> where T : unmanaged` with `Some(T value, float scale)` and `None()` | `OptionTypes1.Variants.Some<T>` and `.None<T>` receive operators returning `Option<T>`; type parameters and the `unmanaged` constraint are copied exactly. |
| `GenericMultiParameter` | `Result<TError, TValue>` with independent constraints and `Ok(TValue value)`, `Error(TError error)` | Both payload operators preserve all type parameters/constraints and return the correctly closed `Result<TError, TValue>`. |
| `NestedNonGeneric` | `Outer.Command` union with `Run(float speed)` and `Stop()` | Operators are declared on `Outer.Command`, accept `Outer.CommandTypes.Variants.Run/Stop`, and compile from a consumer in the containing type. |
| `ReadonlyUnion` | `public readonly partial struct ReadonlyShape` with public variants | Operators compile in a readonly union and do not require mutable receiver state or defensive mutation. |
| `RefStructUnion` | `public ref partial struct RefShape` with a public fieldless and a fielded variant | Operators use ref-safe payload/union types and compile under the preview language version; no boxing or interface conversion is introduced. |
| `NullableAndTupleFields` | Public variants containing `string?`, `(string? label, int count)`, and nullable value types | Operators preserve nullable annotations and tuple element types/names at the payload boundary and forward null values unchanged. |
| `SharedManagedStorage` | `[DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]` with mutually exclusive reference and managed-struct fields | Operators call the ordinary factories; no conversion bypasses shared boxing/unboxing or introduces an alternate storage path. Runtime round-trip preserves identity and value fields. |
| `EscapedNames` | Public variant/field identifiers requiring `@` escaping, such as `@class(@event)` | Generated operator signatures and forwarding expressions escape every identifier correctly and compile without renaming the public payload API. |
| `LegalNameCollisions` | Four independent sources: an `@variant` union, a `Collision` union with a `variant` factory, `GenericUnion<variant>`, and a union named `Variants` | Operator parameter names are generated hygienically; the union type, factory, generic parameter, and `Variants` container never shadow one another or the payload parameter. The `Variants`-named source runs in an isolated compilation because combining it with other generated unions exposes the pre-existing CS0576 alias collision documented in `docs/issues/2026-10-28-generated-variants-alias-collision.md`. |
| `FormerlySerializedAsCanonicalOnly` | `[Serializable, DiscriminatedUnion]` with `[Variant(FormerlySerializedAs = "Old")] Current(int value)` plus `Empty()` | Canonical `Current` and `Empty` payloads receive operators. The generated former-name `Old` payload, when Unity serialization support is present, receives no operator and is not treated as a runtime variant. |
| `NonPublicFactoryIsNotPromoted` | Public union with `public Visible(int)`, `internal Hidden(int)`, and `private Secret(int)` factories | Only `Visible` has a generated operator. A consumer assignment from `Hidden` or `Secret` payload to the union produces the normal compiler conversion error (`CS0029` or equivalent); no generator diagnostic is invented. |
| `UnionDoesNotConvertBack` | Public union with `Value(int)` and a consumer attempting `ShapeTypes.Variants.Value payload = union` | The forward conversion works, while reverse conversion remains unavailable and the consumer receives the normal compiler error. No accidental operator from union to payload is emitted. |
| `RecordInheritanceUnchanged` | Abstract record union with `Success(int)` and `Failure(string)` nested records | Existing derived-record-to-base-record assignment remains valid. No struct-payload operators are added to the record path, and record generation remains otherwise unchanged. |

For compiler-error rows, retain the generated source and assert the expected
Roslyn diagnostic from the output compilation. These cases verify that the
conversion policy is enforced without relying on an analyzer-specific error.

## Behavioral type definitions

The following small set is sufficient for compile-emit-load behavioral tests;
the remaining rows above are source-inspection or diagnostic-only fixtures.

| Type | Declaration coverage | Variants |
|---|---|---|
| `ImplicitConversionShape` | Basic public fieldless/fielded conversion | `Circle(float radius)`, `Rectangle(float width, float height)`, `Point()` |
| `ImplicitConversionOption<T>` | Generic payload and constraint propagation | `Some(T value, float scale)`, `None()`; `where T : unmanaged` |
| `ImplicitConversionManaged` | Nullable, tuple, reference, and Shared managed storage | `Text(string? value)`, `Pair((string? label, int count) value)`, `Managed(ImplicitManagedPair value)`, `Reference(ImplicitReferencePayload value)`, `Empty()` |
| `ImplicitConversionNested.Command` | Non-generic containing type and qualified payload type | `Run(float speed)`, `Stop()` |
| `ImplicitConversionReadonly` | Readonly union conversion | `Value(int value)`, `Empty()` |

`ImplicitManagedPair` and `ImplicitReferencePayload` are the local helpers in
`ImplicitConversionUnions.cs`.
`RefShape`, escaped-name, alias, non-public, reverse-conversion, and record
fixtures should remain focused generator-driver sources because they either
need preview/ref safety, Unity references, or intentional compilation errors.

## Behavioral assertions

| Test | Type | Assertion |
|---|---|---|
| `CirclePayloadImplicitlyConverts` | `ImplicitConversionShape` | Assign `new ImplicitConversionShapeTypes.Variants.Circle(3.5f)` directly to `ImplicitConversionShape`; assert `kind == Circle` and `circle.radius == 3.5f`. |
| `RectanglePayloadImplicitlyConvertsThroughArgument` | `ImplicitConversionShape` | Pass a two-field payload through a method argument without a cast; assert both fields preserve independent values and the union accessor returns them. |
| `PointPayloadImplicitlyConverts` | `ImplicitConversionShape` | Assign the parameterless payload; assert `kind == Point` and its empty `Deconstruct()` remains callable. |
| `GenericPayloadImplicitlyConvertsForMultipleClosedTypes` | `ImplicitConversionOption<int>` and `ImplicitConversionOption<float>` | Assign closed generic `Some<T>` and `None<T>` payloads directly; assert `T`, `scale`, and kind survive for multiple closed types. |
| `NullableTupleAndManagedValuesSurviveImplicitConversion` | `ImplicitConversionManaged` | Convert nullable, tuple, managed-struct, and reference payloads; assert null remains null, tuple values survive, and managed object identity is preserved. |
| `NestedPayloadImplicitlyConverts` | `ImplicitConversionNested.Command` | Assign `ImplicitConversionNested.CommandTypes.Variants.Run` directly and assert the nested union kind/accessor. |
| `ReadonlyPayloadImplicitlyConverts` | `ImplicitConversionReadonly` | Assign payload from a readonly context and assert the conversion compiles and preserves the value. |

## Existing assertions and test impact

The ordinary snapshot base intentionally excludes
`cases/discriminated_union/variant_payload`, so this feature does not need new
input/output snapshot directories. The active Q09 exact-output compatibility
baseline does cover the same generated union source, however. Its
`SPIRE_Q09_ACTUAL_ROOT` archive runs only after `AssertExactOutputs`, so a
baseline mismatch does not produce usable archived output. Update the affected
core, JSON-struct, and Unity base-union expected files by hand with the
approved operator blocks, then recompute their manifest SHA-256 values; do not
promote emitter-derived archive output directly into the accepted baseline.
Four benchmark `*.Bench.g.cs` files also embed the variant-payload emitter, so
the affected set is 17 expected files plus the manifest. Those files and the
manifest (with its `ApprovedChange` provenance) have now been updated. Record,
converter, drawer, and support outputs remain unchanged.

Existing `VariantPayloadCoreTests` source checks are structural (storage,
payload accessibility, and diagnostics) and should remain valid; the focused
driver test owns operator assertions. The canonical/former alias regression
also lives in that driver test and uses the Unity harness, so no existing Unity
test file needs modification. Record behavioral tests should remain unchanged
except for an explicit regression assertion if the record generator is touched.

Suggested ownership:

- `tests/Houtamelo.Spire.BehavioralTests/Types/ImplicitConversionUnions.cs`
  and `tests/Houtamelo.Spire.BehavioralTests/Tests/ImplicitConversionTests.cs`:
  the seven successful runtime tests `CirclePayloadImplicitlyConverts`,
  `RectanglePayloadImplicitlyConvertsThroughArgument`,
  `PointPayloadImplicitlyConverts`,
  `GenericPayloadImplicitlyConvertsForMultipleClosedTypes`,
  `NullableTupleAndManagedValuesSurviveImplicitConversion`,
  `NestedPayloadImplicitlyConverts`, and `ReadonlyPayloadImplicitlyConverts`.
- `tests/Houtamelo.Spire.SourceGenerators.Tests/Behavioral/VariantPayloadImplicitConversionTests.cs`:
  the ten compiler/symbol tests `PublicVariants_ExposeExactOperatorsAndCompileAssignments`,
  `ManyVariants_EmitOneOperatorPerCanonicalPayload`,
  `GenericAndNestedUnions_PreserveTypeParametersAndConstraints`,
  `ReadonlyAndRefStructUnions_CompilePayloadAssignments`,
  `EscapedNullableAndTupleFields_PreservePayloadTypesAndLiftNullableConversion`,
  `NonPublicVariants_DoNotReceivePublicConversions`,
  `ConversionIsForwardOnly`, `FormerNamePayload_IsCanonicalOnly`,
  `EscapedUnionAndGenericParameterNamedVariant_DoNotShadowConversionParameters`,
  and `RecordUnion_RetainsInheritanceConversionWithoutGeneratedOperators`.

## Alias-placement addendum

This is a narrow follow-up to the conversion coverage. Ordinary generated
aliases move inside a named union namespace; no compilation-wide collision
detector or alias renaming is introduced. Global-namespace output keeps the
existing root placement and fallback behavior.

| Case | Input | Required assertions |
|---|---|---|
| `NamedNamespace_GlobalVariantsType` | A named-namespace union with a typed payload-to-union implicit-conversion consumer, plus an unrelated global type named `Variants` | The generated union and conversion consumer compile. The ordinary `Variants` alias is inside the union namespace, so the global type does not cause CS0576. |
| `NamedNamespace_OtherAliasesAndNestedUnion` | A named-namespace union under a containing type, with generated `Fields`/`Serializable` aliases shadowed by global types and a nested union in the same named namespace | `Fields`, `Serializable`, and related ordinary aliases resolve within the generated namespace; nested union output remains compilable and its implicit conversion uses the correct payload type. |
| `NamedNamespace_UsingsStayInNamespace_CSharp9` | The same named-namespace shape compiled with C# 9 parse options | Generated syntax has a `.Usings` list on the namespace declaration and no compilation-unit `using` directives; generated code and the conversion consumer compile under C# 9. |
| `GlobalNamespace_RootAliasFallback` | A global-namespace union with no declared namespace | Ordinary aliases remain at the compilation-unit root, including the existing fallback path; public payload-to-union conversion behavior is unchanged. |
| `SameNamespaceCollision_RemainsCompilerOwned` | A user-authored type colliding with a generated ordinary alias in the union's own namespace | The normal compiler diagnostic remains; no new generator diagnostic, collision scan, alias rename, or policy change is introduced. |

The first four cases belong in the focused generator-driver coverage alongside
`VariantPayloadImplicitConversionTests`; the final case is a diagnostic
regression assertion. Keep the existing isolated `Variants`-name compilation
for the pre-existing global-alias issue documented above.
