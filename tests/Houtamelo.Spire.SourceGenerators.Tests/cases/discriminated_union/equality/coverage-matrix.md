# Variant-payload equality coverage matrix

This matrix defines equality for ordinary, non-`ref` struct unions emitted by
the variant-payload generator. Record and `ref struct` unions keep their
existing behavior. The deferred `union == default` policy belongs to the
future SPIRE003 plan and is out of scope here.

## Contract

For a union `Shape` and canonical payloads under `ShapeTypes.Variants`, the
generated union implements `IEquatable<Shape>` and one
`IEquatable<ShapeTypes.Variants.V>` per variant. It exposes public typed
`Equals` overloads, `Equals(object?)`, `GetHashCode()`, and public static
`==`/`!=` operators for `(Shape, Shape)`, `(Shape, V)`, and `(V, Shape)`.

The union owns all mixed operators. Payload structs do not implement reverse
`IEquatable<Shape>` and do not receive equality operators. A non-public factory
still has a public canonical payload type, so its payload participates in the
same generated comparison surface.

Equality first compares the discriminator. For a matching variant it compares
only that variant's declared fields using `EqualityComparer<TField>.Default`.
`Equals(object?)` accepts another boxed union of the exact union type; a boxed
payload, another union type, or `null` is unequal. `GetHashCode()` uses the same
discriminator and active fields. Generated serialization payloads, callbacks,
managed caches, inactive storage slots, and other implementation fields never
participate. No `Hasher` type is generated.

## Input shapes

These shapes cover the required input dimensions. They can be represented by
focused generator-driver sources and reused by the runtime assertions below;
they do not all require a separate golden snapshot.

| Shape | Declaration to exercise | Equality risk |
|---|---|---|
| `BasicFieldless` | `Shape { Circle(double radius), Square(int side), Point() }` | Same variant, different values, different tags, and zero-field payloads. |
| `ManyFields` | One variant with at least nine fields of mixed primitive/reference types. | Generated comparisons and hash combination remain valid beyond common arities. |
| `Readonly` | `readonly partial struct Immutable { A(int), B(string?) }`. | Typed methods and operators compile with readonly receivers. |
| `GenericClass` | `Option<T> where T : class` with `Some(T? value)` and `None()`. | Generic payload/interface closure and nullable reference comparison. |
| `GenericUnmanaged` | `Option<T> where T : unmanaged` with `Some(T value, float scale)`. | Generic-dependent and concrete unmanaged storage compare together. |
| `GenericLocalNames` | Unconstrained generic parameters named `other`, `obj`, `hash`, `left`, and `right`, closed over reference and value types. | Generated parameters and locals must not shadow field type names. |
| `NullableReferences` | Variants containing `string` and `string?`, including `null`. | Default comparer handles null and nullable annotations. |
| `NullableStorageAnnotations` | Typed slots shared by `List<string>`/`List<string?>` and nullable-annotated tuples; Shared storage with `string?`. | Comparer calls must compile without nullable warnings after annotation merging or shared-storage casts. |
| `TypedManaged` | `ReferenceStorage.Typed`, with a managed-reference-containing `ManagedPair` value. | Equality reads typed storage rather than comparing layout slots or boxes. |
| `SharedReferences` | `ReferenceStorage.Shared`, with mutually exclusive `string`, `int[]`, and `object` fields. | Shared object storage must convert to each logical field type. |
| `AutoReferences` | `ReferenceStorage.Auto` with four distinct reference candidates. | Auto's shared-slot choice must preserve equality semantics. |
| `CustomEquatable` | A field type implementing `IEquatable<T>` with observable typed comparison. | `EqualityComparer<T>.Default` must call typed field equality. |
| `FloatingPoint` | `double` or `float` payload containing `NaN`, `+0`, and `-0`. | Field comparison follows the default comparer, not hand-written `==`. |
| `Arrays` | `int[]` or `string[]` payload, comparing the same reference and two equal-content arrays. | Default array comparer is reference equality; no sequence comparison. |
| `NestedEscaped` | Non-generic containing type with nested union and `@class`/keyword names. | Qualified payload names and escaped identifiers remain valid. |
| `PrivateFactories` | Public union with public, internal, and private `[Variant]` factories. | Every canonical payload gets typed union equality without an accessibility leak. |
| `SerializableAlias` | `[Serializable]` union with `FormerlySerializedAs`, serialized wrappers, and Unity callbacks. | Serialization-only fields and alias/cache state stay excluded. |

Retain record and `ref struct` control shapes: records keep compiler record
equality, while ref structs receive no unsupported interface/operator additions.

## Snapshot cases

`GeneratorSnapshotTestBase` skips `variant_payload` and retired `struct_*`
directories. New snapshots live under `discriminated_union/equality`, which the
existing snapshot discovery includes without a dedicated test class.

Three full snapshots are sufficient to pin generated declarations and
representative comparison bodies. Storage-policy combinations are better
covered by compile/emit/load tests, where both logical values and generated
source can be inspected without multiplying golden files.

| Case | Input | Required output assertions |
|---|---|---|
| `BasicEquality` | Namespace-level `Shape` with `Circle(double)`, `Square(int)`, and `Point()`. | Union lists `IEquatable<Shape>` plus all canonical payload interfaces; typed `Equals`, object override, hash override, and both operators for every operand pair are present. Payload structs have no equality members. |
| `GenericNullableEquality` | `Option<T> where T : class` with `Some(T? value)` and `None()`. | Type parameters/constraints are copied to payload interfaces; field comparison uses `EqualityComparer<T?>.Default`; no invalid nullable conversion or boxing helper appears. |
| `NestedReadonlyEquality` | Non-generic host with a nested readonly union containing `A(string?)` and fieldless `B()`. | Qualified payload interfaces, readonly equality methods, and null-aware logical field comparison compile. Unity serialization independence is tested through its real-reference compile/emit/load harness rather than a plain snapshot lacking Unity references. |

Existing fixtures already exercise the input shapes and should be reused as
focused equality sources rather than copied into new snapshots:

| Existing fixture/source | Shape already covered |
|---|---|
| `struct_overlap/Basic`, `struct_overlap/MixedManaged`, `struct_overlap/AllUnmanaged` | Basic, mixed managed/unmanaged, and multi-field storage. |
| `struct_overlap/Readonly`, `struct_overlap/Fieldless`, `struct_overlap/ConstructorNullableReferenceAnnotations` | Readonly, fieldless, and nullable-reference forms. |
| `struct_overlap/NestedInClass`, `struct_boxed_fields/NestedInClass` | Nested qualification and accessibility. |
| `struct_additive/Generic`, `struct_boxed_fields/GenericFallback`, `struct_boxed_tuple/Generic` | Generic and generic-dependent payloads. |
| `VariantPayloadCoreTests` source helpers (`ReferenceSource`, `SimultaneousReferencesSource`, `AutoDistinctReferencesSource`, `GenericSource`, `ReadonlySource`, `NestedSource`, `NamingSource`, `CustomFieldSource`) | Typed/Shared/Auto, generic, readonly, nested, escaped/collision, and custom-field paths. |
| `SerializablePayloadTests`, `UnityVariantPayloadGeneratorTests`, `json_stj/*`, `json_nsj/*` | Serializable markers, aliases, callbacks, and Unity metadata. |

Expected output should be compared with
`GeneratorTestHelper.AreStructurallyEquivalent`; whitespace and generated hint
names are not part of the contract. The helper does not update snapshots: a
mismatch prints expected and actual source. New expected sources are authored
before implementation; any later correction requires reviewing the contract.

## Behavioral assertions

These are compile/emit/load tests in
`tests/Houtamelo.Spire.SourceGenerators.Tests/Behavioral/`; the historical
`tests/Houtamelo.Spire.BehavioralTests` path has no project in the current tree.

### Generated API shape

| Test | Required assertion |
|---|---|
| `UnionImplementsOwnAndPayloadEquatables` | Roslyn symbols show `IEquatable<Union>` and exactly one `IEquatable<CanonicalPayload>` per variant, including private-factory variants. |
| `UnionDeclaresAllEqualityOperators` | Public static `op_Equality` and `op_Inequality` exist for `(U,U)`, `(U,V)`, and `(V,U)` for every canonical payload; return type is `bool`. |
| `TypedEqualsAndOverridesArePublic` | `Equals(U)`, `Equals(V)`, `Equals(object?)`, and `GetHashCode()` have the required accessibility and override/interface relationships. |
| `PayloadsHaveNoReverseEqualitySurface` | Payload structs do not implement `IEquatable<U>`, declare `Equals(U)`, or declare `==`/`!=`. |
| `NoHasherIsGenerated` | No generated nested `Hasher` type or `IEqualityComparer<U>` implementation is present. |

Use Roslyn `INamedTypeSymbol`/`IMethodSymbol` inspection for declarations and
signatures, following the operator checks in
`Behavioral/VariantPayloadImplicitConversionTests.cs`.

### Union-to-union equality

| Test | Required assertion |
|---|---|
| `SameVariantSameFieldsAreEqual` | Two factories for the same variant and equal values compare true through `Equals`, `==`, and `EqualityComparer<U>.Default`. |
| `SameVariantDifferentFieldIsUnequal` | Changing any logical field makes `Equals` and `==` false and `!=` true. |
| `DifferentVariantsAreUnequal` | Equal-looking fields in different variants compare false because the discriminator differs. |
| `FieldlessVariantsUseOnlyTheTag` | Two instances of one fieldless variant compare equal; two different fieldless variants compare unequal. |
| `ObjectEqualsUsesExactUnionType` | Boxed equal unions compare true; `object.Equals` with `null`, another union type, or a boxed payload compares false. |
| `HashMatchesEquality` | Equal unions have equal hashes; a `HashSet<U>`/`Dictionary<U, ...>` finds an equal value through the default comparer. Do not assert that unequal values always have different hashes. |

### Union-to-payload equality

| Test | Required assertion |
|---|---|
| `UnionEqualsMatchingPayload` | `u.Equals(payload)` is true only when `u` has the payload's variant and equal logical fields. |
| `UnionEqualsNonMatchingPayload` | A union with another tag returns false for the payload overload. |
| `OperatorsWorkInBothOperandOrders` | `(u == payload)` and `(payload == u)` agree; both `!=` forms are the logical negation. |
| `PayloadObjectIsNotAUnion` | `u.Equals((object)payload)` is false; implicit conversion is not used by `Equals(object?)`. |
| `PrivateFactoryPayloadCanCompare` | A payload from an internal/private factory compares through generated union overloads without exposing a factory conversion. |

### Field comparer and storage semantics

| Test | Required assertion |
|---|---|
| `NullFieldsUseDefaultComparer` | Null/non-null reference values follow `EqualityComparer<T>.Default` without throwing. |
| `CustomIEquatableFieldUsesTypedEquals` | A field's `IEquatable<T>.Equals` is observed through union equality, proving no raw-storage or reflection comparison. |
| `NaNUsesDefaultFloatingComparer` | Equal `NaN` fields follow `EqualityComparer<double>.Default`; do not replace it with `double ==`. |
| `ArraysUseDefaultReferenceEquality` | The same array reference compares equal; a different equal-content array does not. |
| `TypedStorageHasLogicalEquality` | Typed managed/unmanaged storage compares active declared fields, not slot bytes or inactive fields. |
| `SharedStorageHasLogicalEquality` | Shared object storage compares after converting to each active declared field type, preserving null and boxed values. |
| `AutoStorageHasLogicalEquality` | Auto-selected storage has the same results as Typed/Shared for equivalent values. |
| `SerializablePayloadAndCacheAreIgnored` | Unity serialized payload/cache state can change without changing equality; source also confirms those fields are absent from equality/hash bodies. |

### Generic, naming, and boundary controls

| Test | Required assertion |
|---|---|
| `GenericReferenceAndValueUnionsCompare` | Closed `Option<string>` and `Option<int>` values use the correct closed payload interfaces and equality methods. |
| `NestedEscapedUnionCompilesAndCompares` | Nested containing types and escaped identifiers compile; signatures refer to qualified payload types. |
| `LargeVariantAndFieldCountsCompile` | More than eight variants and a variant with more than eight fields compile and compare at runtime. |
| `RecordUnionKeepsRecordEquality` | Record source keeps compiler record equality and receives none of the struct payload operators/interfaces. |
| `RefStructKeepsExistingSurface` | Ref struct source remains compilable with its existing surface and receives no unsupported additions. |
| `DefaultComparisonRemainsFutureWork` | No test requires `union == default` or `union.Equals(default)` policy in this change; SPIRE003 owns that decision. |

## Harness and commands

`BehavioralTestBase.CompileAndLoad` in
`tests/Houtamelo.Spire.SourceGenerators.Tests/Behavioral/BehavioralTestBase.cs`
compiles a source string, runs `DiscriminatedUnionGenerator`, emits an in-memory
assembly, and loads it. Use it for runtime equality assertions. Use
`GeneratorTestHelper.RunGenerator`/`GetUnionGeneratedSource` for source and
Roslyn-symbol assertions.

Unity-specific equality/serialization tests should use
`Unity/UnityGeneratorTestHarness.Run` and `EmitAndLoad`, with
`includeUnityReferences: true` for actual Unity metadata. The existing
`Behavioral/UnityRuntimeAssertionTests.cs` demonstrates the no-Unity path by
adding an inline `UnityEngine.Debug` stub and running the same compile/emit/load
pipeline. That stub approach is suitable for assertion plumbing only;
serialized payload metadata still requires real Unity references. Do not claim
Mono/IL2CPP validation from these tests.

Focused commands after test files exist:

```text
dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~VariantPayloadEqualityTests"
dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~VariantPayloadCoreTests"
dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Unity"
```

Run the complete source-generator project after the focused suite passes:

```text
dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests/
```
