# Ordinary Overlap constructor-initialization coverage

This is the bounded test contract for replacing ordinary (non-Unity-serialized)
`Layout.Overlap` factory writes through `Unsafe.AsRef` with constructor
initialization. It covers generated source, emitted assemblies, and the
consumer reference sets that motivated the change.

Implemented coverage uses the eight existing snapshots, the new parameter-mapping
and nullable-reference snapshots, and nine compile/emit/load behavioral tests in
`OverlapConstructorTests.cs`. The remaining proposed snapshot names below are
research candidates; their construction cases are covered behaviorally. Ref-like
fields and user-defined constructor collisions remain outside this change.

The change applies only to non-Unity-serialized struct unions. It must preserve
the existing explicit field offsets, readonly backing fields, factory and
property signatures, deconstruct overloads, JSON behavior, and value semantics.
The Unity-serialized Overlap path, record/class unions, and `Layout.UnsafeOverlap`
remain controls. Generic Overlap unions and unions nested in generic containing
types remain excluded by the existing `SPIRE_DU005` restriction.

The existing ordinary Overlap snapshots are under
`tests/Houtamelo.Spire.SourceGenerators.Tests/cases/discriminated_union/struct_overlap/`.
The existing compile-and-load harness is
`tests/Houtamelo.Spire.SourceGenerators.Tests/Behavioral/`, and the checked-in
runtime types are `tests/Houtamelo.Spire.BehavioralTests/Types/OverlapUnions.cs`
with assertions in `tests/Houtamelo.Spire.BehavioralTests/Tests/OverlapTests.cs`.

## Section 1: Snapshot tests

Each snapshot is an `input.cs`/`output.cs` pair. Existing cases whose generated
factory bodies contain `Unsafe.AsRef` must be updated to the approved
constructor form. The expected output must still contain the same explicit
offsets, backing-field names, field types, readonly modifiers, public factory
signatures, properties, and deconstruct overloads.

### Existing ordinary Overlap cases

| Case name | Input shape | Constructor change contract |
| --- | --- | --- |
| `struct_overlap/AllUnmanaged` | `Circle(double)`, `Rectangle(float, float)`, `Square(int)` | Every value field is initialized through the generated construction path; double, float, and int values map to their original physical fields. No `Unsafe` token or compiler-host reference is emitted. |
| `struct_overlap/RefOverlap` | `Text(string)`, `Error(object)` | Reference fields are initialized without `Unsafe`; non-null identity and null values remain possible. Nullable annotations and reference field offsets are unchanged. |
| `struct_overlap/Namespaced` | Namespaced `Ok(int)` and `Err(string)` | Namespace qualification and the generated API remain unchanged while both factories use the safe construction path. |
| `struct_overlap/MixedManaged` | `Click(int, int, string)` and `Hover(float, float)` | Ordered primitive and managed arguments are assigned to the same physical fields as before; no managed field is omitted or routed through a value-only constructor. |
| `struct_overlap/Region3Boxed` | `Point` value fields stored in the existing boxed region-3 representation | A non-primitive value type remains accepted and boxed as it is today; constructor initialization must preserve the box/unbox path and the generated property/deconstruct casts. |
| `struct_overlap/Readonly` | `readonly struct` with `A(int)` and `B(string)` | The union remains readonly, all generated storage fields remain readonly, and factories compile without mutating readonly storage through `Unsafe`. |
| `struct_overlap/NestedInClass` | Non-generic containing class with nested Overlap struct | Nested qualification, accessibility, offsets, and factory construction remain valid. |
| `struct_overlap/Fieldless` | `Ident(string)`, `Number(int)`, and `Eof()` | Fieldless construction still emits no argument write, while fielded variants use constructor initialization and preserve the existing shared object deconstruct behavior. |

### New focused ordinary Overlap snapshots

| Case name | Input shape | Required assertions |
| --- | --- | --- |
| `struct_overlap/ConstructorParameterMapping` | `First(int x, int y)` and `Second(int left, int right)` with the same ordered parameter types but different field names | Each factory argument initializes its own physical field. The output must not accidentally map by ordinal alone or reuse the other variant's field. |
| `struct_overlap/ConstructorNullableReferenceAnnotations` | `Required(string value)` and `Optional(string? value)` | Same-CLR-type reference fields retain the existing collision/nullability policy; both factories compile and assign the correct shared or qualified storage without an `Unsafe` write. |
| `struct_overlap/ConstructorNullableValueCollision` | `Plain(int value)` and `Optional(int? value)` | `int` and `int?` remain distinct physical types and generated properties; both constructor paths preserve values and default initialization. |
| `struct_overlap/ConstructorAllFieldless` | At least three fieldless variants | The generated union contains no field initialization statements, retains only the discriminator storage, and every factory returns the correct kind. |
| `struct_overlap/ConstructorSingleVariant` | One fielded variant, such as `Only(decimal value)` | The single factory still gets the correct constructor, explicit field offset, property, and deconstruct shape; no multi-variant assumptions are introduced. |
| `struct_overlap/ConstructorManyVariants` | Eight or more variants mixing fieldless, primitive, reference, and multi-field arms | Every generated constructor/factory pair is distinct and deterministic; no later variant overwrites an earlier mapping and no helper marker fields/types appear. |
| `struct_overlap/ConstructorTupleField` (?) | Tuple-valued fields with differing tuple element names but equivalent CLR tuple types | Add only if tuple fields are accepted by the existing Overlap model. If accepted, preserve tuple element metadata and assign the tuple as one constructor argument; otherwise record the existing diagnostic/compile boundary rather than inventing support. |
| `struct_overlap/ConstructorRefStructField` (?) | A `ref struct` parameter or nested `ref struct` value | Feasibility probe only. Keep the existing acceptance/diagnostic behavior; do not broaden Overlap to ref-like fields as part of this change. |
| `struct_overlap/ConstructorUserCtorCollision` (?) | A user-declared constructor whose signature collides with a constructor the new emitter would generate | Establish the compiler's existing collision result. Do not add speculative diagnostics or silently rename the generated constructor without an approved rule. |

All ordinary Overlap generated-source checks must additionally establish the
following structural invariants:

| Invariant | Expected result |
| --- | --- |
| Unsafe dependency | Generated ordinary Overlap source contains zero references to `Unsafe`, `Unsafe.AsRef`, or `System.Runtime.CompilerServices.Unsafe`. |
| Storage layout | The discriminator and all existing runtime backing fields retain their names, types, offsets, `readonly` modifiers, and visibility. No wrapper, marker field, or additional runtime storage field is emitted. |
| API surface | Static factory parameter lists, generated properties, `Kind`, `kind`, `Is*`, `ToString`, and all existing deconstruct overloads remain present with the same signatures. |
| Initialization form | A factory initializes a fresh union from a zero/default state and assigns only its own active fields. Fieldless factories do not contain a fake payload write. |
| Unity separation | The ordinary fixture remains free of `ISerializationCallbackReceiver`, Unity serialization attributes, payload classes, and drawers. Existing Unity-serialized Overlap fixtures remain covered by their current tests and are not converted by this change. |

## Section 2: Behavioral type definitions

These types belong in the source-generator behavioral harness unless the
existing checked-in behavioral suite is the better owner for JSON or public
API assertions.

| Union name | Description | Variants |
| --- | --- | --- |
| `OverlapConstructorBasic` | Baseline non-Unity Overlap union covering primitive, multi-field, and fieldless factories. | `Circle(double radius)`, `Rectangle(float width, float height)`, `Point()` |
| `OverlapConstructorParameterMapping` | Same ordered parameter types with different physical field names. | `First(int x, int y)`, `Second(int left, int right)` |
| `OverlapConstructorReferences` | Managed reference identity and null handling. | `Text(string? value)`, `Object(object detail)`, `Empty()` |
| `OverlapConstructorBoxedValue` | Existing region-3 boxed value representation. | `Point(Point2 value)`, `Pair(Point2 first, Point2 second)`, `Empty()` |
| `OverlapConstructorNullableFields` | Same-name reference nullability plus a distinct nullable value type. | `Required(string value)`, `Optional(string? value)`, `Maybe(int? value)` |
| `OverlapConstructorReadonly` | Readonly struct construction and reflection of readonly backing fields. | `Number(int value)`, `Text(string text)` |
| `OverlapConstructorNested` | Non-generic containing type and nested union accessibility. | `Outer.Nested.Number(int value)`, `Outer.Nested.Empty()` |
| `OverlapConstructorFieldless` | All variants are fieldless. | `None()`, `Done()`, `Cancelled()` |
| `OverlapConstructorTuple` (?) | Tuple field metadata probe. | `First((int x, int y) point)`, `Second((int left, int right) point)` |

Do not add a positive generic Overlap type: the existing `SPIRE_DU005`
restriction is a regression control. Keep the existing `UnsafeOverlap` types as
a separate control proving that this change does not remove its intentionally
unsafe runtime path.

## Section 3: Behavioral test assertions

### Factory construction and field routing

| Test name | Type | Description |
| --- | --- | --- |
| `Factories_SetKindForEveryVariant` | `OverlapConstructorBasic` | Invoke every factory and compare `kind` with its corresponding `Kind` member. |
| `Factories_PreserveAllPrimitiveFields` | `OverlapConstructorBasic` | Use distinct values for every argument and read every generated property. |
| `Factories_MapSameOrderedTypesByField` | `OverlapConstructorParameterMapping` | Construct both variants with different values and prove `x/y` and `left/right` do not cross-route. |
| `Factories_PreserveReferenceIdentityAndNull` | `OverlapConstructorReferences` | Preserve an object reference by identity and preserve null string/object inputs. |
| `Factories_PreserveRegion3BoxedValue` | `OverlapConstructorBoxedValue` | Construct one- and two-field boxed-value variants and verify property and typed deconstruct values. |
| `Factories_PreserveNullableReferenceAndValueFields` | `OverlapConstructorNullableFields` | Verify `string`/`string?` nullability cases and a non-null `int?` value through the generated properties. |
| `FieldlessFactories_DoNotThrow` | `OverlapConstructorFieldless` | Invoke every fieldless factory and verify only the correct discriminator is observable. |
| `ReadonlyFactories_CompileAndRun` | `OverlapConstructorReadonly` | Construct both variants and verify values while reflection confirms generated runtime fields are init-only. |

### Zeroing, copies, and inactive storage

| Test name | Type | Description |
| --- | --- | --- |
| `FreshFactories_ZeroInactiveFields` | `OverlapConstructorBasic` | For each fielded factory, verify unrelated generated fields retain default values rather than stale data from another construction path. |
| `FieldlessFactories_ZeroAllPayloadStorage` | `OverlapConstructorFieldless` | A fieldless value has no fabricated payload and its discriminator is correct. |
| `DefaultValue_RetainsExistingKindAndDefaults` | `OverlapConstructorBasic` | `default(T)` keeps the existing zero discriminator semantics and default field values. |
| `ValueCopies_DoNotShareConstructionState` | `OverlapConstructorBasic` | Copy a constructed union, construct another variant, and confirm the first copy retains all of its values. |

### Public API, layout, and generated-source dependency checks

| Test name | Type | Description |
| --- | --- | --- |
| `GeneratedFields_RetainExplicitOffsetsAndReadonlyStorage` | all positive ordinary Overlap types | Reflection verifies `LayoutKind.Explicit`, existing field offsets, field types, and `FieldInfo.IsInitOnly`; no extra runtime fields are present. |
| `GeneratedApi_RetainsFactoriesPropertiesAndDeconstructs` | `OverlapConstructorBasic` | Reflection checks factory parameter lists, property types, `Kind`, `kind`, `Is*`, and the existing deconstruct overload set. |
| `GeneratedSource_HasNoUnsafeDependency` | all positive ordinary Overlap types | Scan all generated trees and emitted assembly references; neither the source nor the assembly references `System.Runtime.CompilerServices.Unsafe`. |
| `OrdinaryOverlap_CompilesWithoutUnsafeReference` | `OverlapConstructorBasic` | Compile and emit using a real reference set with `System.Runtime.CompilerServices.Unsafe` removed; load and execute the emitted factories. |
| `OrdinaryOverlap_CSharp9WithoutIsExternalInit` | `OverlapConstructorBasic` | Compile and execute with C# 9 and a real reference set lacking `Unsafe` and any accessible `IsExternalInit`; preserve the existing getter-only compatibility behavior where init is unavailable. |
| `OrdinaryOverlap_ModernHostRetainsBehavior` | `OverlapConstructorBasic` | Compile and execute the same source with the modern host/reference set; confirm values, deconstructs, and generated API remain equivalent to the C# 9 run. |
| `UnitySerializedOverlap_RemainsSeparateControl` | existing `Serializable_ExplicitOverlap` fixture | Confirm Unity serialization attributes, payload/callback surface, and mutable construction path remain unchanged by the ordinary Overlap emitter change. |
| `UnsafeOverlap_RemainsSeparateControl` | existing `Layout.UnsafeOverlap` fixtures | Confirm UnsafeOverlap's existing dependency and behavior remain untouched; this change must not broaden or remove its unsafe implementation. |

### Patterns, deconstruct, and JSON

| Test name | Type | Description |
| --- | --- | --- |
| `KindSwitch_ReturnsEveryVariant` | `OverlapConstructorBasic` | Existing kind switch behavior continues to select the correct arm for fielded and fieldless values. |
| `PropertyPatterns_ReadConstructedValues` | `OverlapConstructorBasic` | Property patterns read values initialized by the new constructor path for single- and multi-field variants. |
| `Deconstruct_UniqueAndSharedArities` | `OverlapConstructorBasic` | Typed unique-arity and object shared-arity deconstructs return the correct constructor arguments; fieldless/mismatched arity retains the existing null result. |
| `JsonSystemText_RoundTripsFactories` | existing `JsonShapeOvlStj` | System.Text.Json serialization/deserialization still reconstructs kind and every active field through generated factories. |
| `JsonNewtonsoft_RoundTripsFactories` | existing `JsonShapeOvlNsj` | Newtonsoft.Json serialization/deserialization retains the same round-trip behavior. |

## Compatibility execution contract

The C# 9 compatibility case must use actual Roslyn reference assemblies rather
than adding the current host's `Unsafe` or `IsExternalInit` references. The
harness must assert that `Unsafe` is absent and `IsExternalInit` is absent or
inaccessible before running the generator. The Spire runtime contributes an
internal PolySharp compatibility type, which does not enable consumer init
properties. Compile and emit the generated assembly, load it in the test host,
and execute factory/property/deconstruct assertions. A separate modern-host
run must execute the same behavioral assertions. The two runs prove that the
constructor change removes the runtime dependency without changing supported
behavior on newer hosts.

Editor Mono execution uses its actual runtime references rather than .NET 3.1
type forwarders. It removes assemblies exposing the helper API and asserts that
any remaining private helper metadata is inaccessible, while preserving the
core BCL. The .NET run retains the strict absence check against the 3.1 pack.

The snapshot and behavioral checks must not assert private helper names that
are not part of the API. They must assert the existing public surface,
explicit offsets, readonly storage, and the absence of the unsafe dependency,
because those are the compatibility promises of this change.
