# Variant-payload core-generation coverage matrix

This matrix defines phase 1 of the union storage redesign. It covers the
generator-driver and runtime-generated-code contract only. JSON, Unity
serialization/property drawers, analyzers, code fixes, and native Mono/IL2CPP
validation belong to later phases and are listed as gates at the end.

The input declarations use the redesigned API: `[DiscriminatedUnion]` has no
`Layout` or `GenerateDeconstruct` argument, and `ReferenceStorage` is a named
property with `ReadGlobalCfg`, `Auto`, `Typed`, or `Shared`. Generated payload
types are public ordinary structs under a sibling `{UnionName}Types.Variants`
container. They have private `_`-prefixed serialized fields, public getter-only
properties, constructors, and typed `Deconstruct` methods. Runtime data helpers
are implementation details under `{UnionName}Types.Fields`; snapshots should
assert their shape where it is part of this contract, while behavioral tests
must exercise the public API rather than generated private names.

## Snapshot tests

Each row is an `input.cs`/`output.cs` pair under this directory. The output
assertions should use stable generated text checks or the existing snapshot
normalization conventions; they must not assert compiler-generated whitespace or
ordering that the design leaves unspecified.

### Basic payload representation

| Case | Input | Required output assertions |
|---|---|---|
| `BasicPayload` | Namespace-level `Shape` with `Circle(float radius)`, `Rectangle(float width, float height)`, and `Point()` | Emits `ShapeTypes.Variants.Circle`, `.Rectangle`, and `.Point` payload structs with private fields, public getters, constructors, and typed `Deconstruct`; union factories, `Kind`, and variant-qualified accessors exist. |
| `FieldlessAndFielded` | One fieldless and two fielded variants with different arities | Empty payload has no fields and a parameterless constructor/zero-argument `Deconstruct`; each fielded payload retains declaration order and names. |
| `PayloadAccessibility` | Internal union plus a private nested union in a non-generic containing type | The `Types` container matches the union accessibility; `Variants` and payloads are public within it, while `Fields` remains internal. The hierarchy remains nested in the declaring scope. |
| `ManyVariants` | At least eight variants mixing zero, one, and four fields | Every declared variant receives exactly one payload type/accessor/factory; enum values preserve declaration order. |
| `MutablePayloadNoFlatProperties` | Variants with repeated parameter names (`x`, `value`) and distinct names | Payload fields are private and getter-only; union has no old flat `x`, `value`, or union-level `Deconstruct(out Kind, ...)` members. |

### Unmanaged payload helpers

| Case | Input | Required output assertions |
|---|---|---|
| `UnmanagedAutoSubset` | `A(Vector2 position, int count)`, `B(Vector3 direction)`, `C()` using concrete unmanaged structs and primitives | Emits `ShapeTypes.Fields.Unmanaged` with an explicit outer helper overlaying eligible per-variant Auto-layout subsets at offset zero; no generated `Pack` or size/alignment constants. |
| `UnknownSizeUnmanaged` | User-defined unmanaged `UnknownBlock` (multiple unmanaged fields, no generator-known size) in one variant and a primitive in another | The unknown-size concrete field is still included in its variant payload; generation does not require a size catalog, `sizeof`, target profile, or manual offset calculation. |
| `GenericDependentValue` | `Option<T> where T : unmanaged` with `Some(T value, float scale)` and `None()` | `T value` remains ordinary typed storage outside explicit helpers; concrete `float scale` is in the unmanaged subset; `Option<T>` and `OptionSome<T>` preserve the type parameter/constraint. |
| `MixedGenericFields` | Generic union with `T value`, a concrete unmanaged field, a reference field, and an empty variant | Only the unresolved generic field uses typed fallback; concrete value and reference storage follow their independent policies without a generic explicit-layout helper. |
| `NonGenericContainingType` | `Outer.Command` nested in a non-generic type | `Outer.CommandTypes` contains `Fields` and `Variants`; concrete unmanaged overlay is loadable and public payload names are `Outer.CommandTypes.Variants.Run`-style nested types. |

### Reference storage and Auto selection

| Case | Input | Required output assertions |
|---|---|---|
| `TypedReferenceReuse` | `[DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]` with mutually exclusive `A(string text)`, `B(string? text)`, `C(int[] values)` | Nullable and non-nullable spellings of the same reference type reuse one typed slot; different reference types get compatible separate slots; reads are typed and no managed references overlap through explicit offsets. |
| `TypedReferenceContainingStruct` | Typed mode with `A(ManagedPair pair)` and `B(ManagedPair? pair)` where `ManagedPair` contains a managed reference | The managed struct stays in a typed slot and is not boxed; simultaneous fields are separate; generated accessor preserves the declared payload type. |
| `SharedReferenceReuse` | `[DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]` with four mutually exclusive variants, each containing a different reference type | Shared mode uses one reusable `object?` slot for the mutually exclusive candidates; accessors cast/unbox to the declared type and preserve values. No unrelated managed references are placed at the same explicit field offset. |
| `SharedManagedStructBoxing` | Shared mode with a managed-reference-containing struct field in one variant and a reference field in another | Both candidates can reuse an object slot; the managed struct is boxed on storage and unboxed on access, while the public payload still exposes its value type. |
| `AutoChoosesShared` | Four mutually exclusive variants, each with one different reference candidate | Generated representation selects Shared because `SharedSlots * 2 <= TypedSlots` (1*2 <= 4). Snapshot checks slot shape; runtime checks all four values. |
| `AutoChoosesTyped` | Four variants with the same reference type, or a set whose reused typed count is less than twice Shared | Generated representation selects Typed because Shared does not meet the half-slot threshold; runtime checks direct typed value preservation. |
| `AutoGlobalConfig` | Omitted/reference `ReadGlobalCfg` attribute under project default `Typed`, then under `Shared`, plus explicit `Auto` under a `Typed` project default | Omitted and explicit `ReadGlobalCfg` inherit the project setting; explicit `Auto` runs slot comparison and does not inherit a forced project mode. A missing project setting falls back to Auto. |
| `ReferenceNullabilityAndTupleNames` | Mutually exclusive fields differing only by nullable annotation or tuple element names | CLR-equivalent reference types reuse the same typed slot; tuple element names and nullable metadata do not create distinct storage types. |

### Generics, nesting, diagnostics, and naming

| Case | Input | Required output assertions |
|---|---|---|
| `GenericUnionAtNamespaceScope` | `Option<T> where T : class` and `Result<TError, TValue>` with constraints | Union and each `Types{arity}.Variants` payload copy the union's own parameters and exact constraints; the containers remain nongeneric, and factories/accessors are correctly closed for runtime reflection. This is valid and has no nesting diagnostic. |
| `GenericAncestorDirect` | `class Container<T> { [DiscriminatedUnion] struct Command { ... } }` | Reports the approved generic-ancestor diagnostic on `Command`, naming `Container`; still emits factories, payload types, accessors, and other API so no missing-generated-member cascade occurs. |
| `GenericAncestorIndirect` | `class Outer<T> { struct Inner { [DiscriminatedUnion] struct Command { ... } } }` | Reports the same diagnostic naming nearest offending generic ancestor `Outer`; `Inner` does not make the union valid and generated declarations retain the full containing generic context. |
| `GenericAncestorsMultiple` | `Outer<T>.Middle<U>.Command` union | Reports one actionable diagnostic identifying the nearest declaring generic ancestor according to the approved rule; generated output retains all containing type parameters and normal API. |
| `UnionGenericInsideNonGeneric` | `class Outer { [DiscriminatedUnion] struct Command<T> where T : unmanaged { ... } }` | Valid because the ancestor is non-generic; `CommandTypes1` remains nongeneric, `Variants.Value<T>` preserves the `T` constraint, and concrete unmanaged fields remain eligible. |
| `CamelCaseAndAcronyms` | Variants `Circle`, `ApplyEffect`, `HTTPRequest`, `URL` | Accessors are `circle`, `applyEffect`, `httpRequest`, and `url`; factory method names remain unchanged. |
| `FactoryAccessorConflict` | Factories `run()` and `Run(int value)` or a factory named `circle` with variant `Circle` | Derived accessor matching a factory gets exactly one `_value` suffix (`run_value`/`circle_value`); all other collisions are left for the C# compiler. |
| `AccessorCollisionCompilerOwned` | `URL` and `Url`, or an authored partial member named `circle` | Generator does not invent custom diagnostics or rename additional members; output contains the natural compiler-visible collision. |

### Error continuity and defaults

| Case | Input | Required output assertions |
|---|---|---|
| `DefaultValueUsesFirstKind` | `default(Shape)` with a first fieldless variant and a fielded second variant | Default storage is valid and accesses the declared default/first variant consistently; no null/uninitialized generated storage exception. |
| `CopyIndependence` | Construct two variants, copy each union value, and read/mutate copied public payload values | Value copies retain their own discriminator and field values; constructing or copying one union does not alias another variant's unmanaged or managed storage. |
| `DiagnosedInputStillCompilesGeneratedApi` | Invalid-but-otherwise-constructible union input that produces a Spire error (including generic ancestor cases) and a consumer referencing every generated factory/payload/accessor | The intended Spire diagnostic is present, while generated declarations remain present. There are no secondary missing type/member errors caused solely by suppressed emission. |
| `RemovedLegacyApi` | Any redesigned union with no legacy layout/deconstruct arguments | Old `Layout`, `GenerateDeconstruct`, boxed/unsafe strategies, and union-level typed `Kind` deconstruct APIs are absent from the generated contract; valid source compiles against only the new API. |

At least one snapshot from each table should be run through the real generator
driver with generated source inspected. Cases that intentionally compile with a
diagnostic should assert both the Spire diagnostic and the generated syntax.

## Behavioral type definitions

These types can live in the phase-1 behavioral source set. They should be
compiled by the generator-driver harness and loaded for runtime assertions; do
not depend on private generated helper names in behavioral tests.

| Type | Declaration coverage | Variants |
|---|---|---|
| `Shape` | Basic default mode, mixed fieldless/fielded, payload accessors | `Circle(float radius)`, `Rectangle(float width, float height)`, `Point()` |
| `StorageReuse` | Typed/Shared/Auto reference slot policies | `Text(string text)`, `Numbers(int[] values)`, `Pair(ManagedPair pair)`, `Empty()` |
| `AutoReferenceSet` | Auto Shared threshold | `A(string value)`, `B(int[] value)`, `C(Uri value)`, `D(object value)` |
| `AutoSameReferenceSet` | Auto Typed threshold after same-type reuse | `A(string value)`, `B(string? value)`, `C(string value)`, `D()` |
| `GenericOption<T>` | Namespace-level generic union and typed generic-dependent field | `Some(T value, float scale)`, `None()`; payload is `GenericOptionTypes1.Variants.Some<T>`. |
| `GenericResult<TError, TValue>` | Multiple parameters/constraints copied to sibling payloads | `Ok(TValue value)`, `Error(TError error)` |
| `UnknownBlockUnion` | Concrete unmanaged type whose size is intentionally unknown to generator | `Block(UnknownBlock block)`, `Count(int count)` |
| `NestedShapes.Command` | Non-generic containing type and grouped nested payload placement | `Run(float speed)`, `Stop()` under `NestedShapes.CommandTypes.Variants`. |
| `NamingUnion` | Acronym normalization and factory conflict suffix | `Circle()`, `HTTPRequest()`, `URL()`, `run()` |
| `ManagedPair` | Helper value type containing a managed reference for Typed/Shared behavior | `string Name`, `int Count` |
| `CopyUnion` | Copy/default/value semantics | `Int(int value)`, `Name(string value)`, `Empty()` |

The diagnostic-continuity cases should be separate generator-driver source
strings rather than behavioral types loaded as successful assemblies, since the
expected Spire error intentionally prevents successful compilation.

## Behavioral assertions

### Factories, kind, payload accessors, and values

| Test | Type | Assertion |
|---|---|---|
| `CircleFactorySetsKindAndPayload` | `Shape` | Factory sets the expected enum value; `circle` accessor returns `ShapeTypes.Variants.Circle` with the original radius. |
| `RectanglePayloadFieldsRoundTrip` | `Shape` | Both public payload fields preserve independent values and are mutable on a copied payload. |
| `PointHasEmptyPayload` | `Shape` | Fieldless factory returns `ShapeTypes.Variants.Point`; its empty `Deconstruct() {}` works. |
| `DefaultShapeIsSafe` | `Shape` | Reading the default union's kind/accessor follows the documented first/default variant behavior without throwing. |
| `CopyDoesNotAlias` | `CopyUnion` | Copying a union preserves discriminator/value and changing a copied payload value does not alter the original. |
| `NoLegacyFlatAccess` | `Shape` | Reflection/compile assertions confirm variant fields are exposed through the payload accessor and old flat fields/union-level deconstruct are absent. |

### Typed, Shared, and Auto references

| Test | Type | Assertion |
|---|---|---|
| `TypedStringAndNullableStringRoundTrip` | `StorageReuse` | String and nullable-string variants preserve values through reused typed storage; null remains null. |
| `TypedManagedStructIsNotBoxed` | `StorageReuse` | Managed-reference-containing struct round trips through Typed storage with its declared value type and fields intact. |
| `SharedDifferentReferenceKindsRoundTrip` | `StorageReuse` | String, array, and managed struct variants round trip through Shared slots without invalid cross-variant reads. |
| `AutoSelectsSharedForFourDistinctReferences` | `AutoReferenceSet` | All four distinct reference variants preserve identity/value; generated output selection is Shared by the half-slot rule. |
| `AutoSelectsTypedAfterSameTypeReuse` | `AutoSameReferenceSet` | Same-type reference variants preserve values and Auto chooses Typed after reuse. |
| `GenericReferenceFallback` | `GenericOption<string>` | Generic-dependent `T` storage remains typed when closed over a reference type; factory/accessor preserves object identity. |

### Generic and unmanaged payload behavior

| Test | Type | Assertion |
|---|---|---|
| `GenericUnmanagedValueAndConcreteFieldRoundTrip` | `GenericOption<int>` | `T value` and concrete `scale` both survive construction/access; payload deconstruct returns both typed values. |
| `GenericPayloadCopiesConstraints` | `GenericResult<Exception, string>` (or equivalent declared constraints) | Closed generic payload/accessor types are discoverable and preserve the source constraints at compilation. |
| `UnknownUnmanagedFieldRoundTrip` | `UnknownBlockUnion` | Unknown-size unmanaged value survives construction and copy without generated size metadata. |
| `NestedPayloadAccessorWorks` | `NestedShapes.Command` | Nested union factory returns `NestedShapes.CommandTypes.Variants.Run` through the accessor with correct qualified type names. |

### Payload deconstruction and naming

| Test | Type | Assertion |
|---|---|---|
| `PayloadDeconstructIsTyped` | `Shape` | `ShapeTypes.Variants.Circle.Deconstruct(out float radius)` (and corresponding multi-field overload) returns fields in declaration order. |
| `PayloadDeconstructDoesNotIncludeKind` | `Shape` | Payload `Deconstruct` contains only payload fields; kind is read from the enclosing union. |
| `AcronymAccessorsUseNormalizedCamelCase` | `NamingUnion` | `httpRequest` and `url` accessors exist and return the correct payload types. |
| `FactoryConflictUsesValueSuffix` | `NamingUnion` | A normalized accessor colliding with a factory is exposed as exactly `{name}_value`, while the factory remains unchanged. |

## Later-phase gates (not phase-1 implementation)

- Phase 2 must use these generated payload/accessor names in exhaustiveness
  analysis, switch code fixes, and both JSON converter implementations while
  preserving existing wire format/configuration.
- Phase 3 must reuse the public mutable payload structs through one generic
  `SerializedPayload<T>`, retain isolated UI Toolkit/IMGUI previews, preserve
  focus and unchanged YAML/managed-reference IDs, and validate Undo, prefabs,
  multi-object editing, nested collections, and Mono/IL2CPP Player reloads.
- Phase 4 must audit every generator/parser/model diagnostic path for continued
  API emission, remove obsolete layout/catalog/deconstruction machinery from
  active paths, and run the final real integration suites.
