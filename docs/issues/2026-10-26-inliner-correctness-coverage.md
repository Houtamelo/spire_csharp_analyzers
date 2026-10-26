# Inliner correctness coverage matrix

This matrix defines the regression contract for the approved inliner batch. It
covers `[InlinerStruct]` forwarders and `[Inlinable]` twin methods. The current
24 performance snapshot fixtures remain the compatibility baseline; existing
generated text stays unchanged unless a fixture explicitly exercises one of the
newly corrected cases below.

The batch fixes three source-generation contracts:

- Every emitted identifier is escaped using its source symbol's metadata/name
  contract. This includes contextual-keyword declaring types, methods,
  parameters used in generated calls, and generic type parameters.
- Generated generic declarations preserve all source constraints that affect
  their validity (`class`/`struct`/`unmanaged`/`notnull`, base types,
  interfaces, and `new()`). Existing unsupported parameter modifiers and
  diagnostics remain unchanged.
- Generated hint names are deterministic and unique for every emitted member.
  The approved strategy is to derive every hint from the symbol's complete
  metadata identity: namespace segments, all enclosing type metadata names,
  method metadata name, generic arity, and parameter signature (so overloads
  cannot overwrite one another). The filename key should be an injective,
  filename-safe encoding: encode each UTF-8 metadata component as uppercase
  hexadecimal with an explicit byte length, prefix components by their role
  (`N`, `T`, `M`, `P`), and join only with a fixed ASCII separator. Empty
  namespace/parameter lists have an explicit zero marker. This preserves
  generic arity (the metadata backtick is encoded, not discarded) and means
  names that differ only by punctuation, keyword escaping, nesting, or generic
  arity cannot conflate. Keep public generated type/member names unchanged;
  only the generated hint identity changes. Append the emitter suffix
  (`.InlinerStruct.g.cs` or `.InlinableTwin.g.cs`). Do not depend on global
  collision bookkeeping or discovery order.

The emitted forwarder must compile and execute the original method exactly once.
The tests intentionally exclude the separate inliner body-rewriting limitation
reported in Q18 and the existing unsupported ref/out/params diagnostics.

## Section 1: Snapshot tests

Snapshot tests use the existing `InlinerStructSnapshotTestBase` and
`InlinableSnapshotTestBase`. Each new case has an `input.cs` and hand-written
`output.cs`; the running generator must never create the expected output.

### Category A: existing baseline that must stay stable

| Case | Description |
|---|---|
| `static_void_arity0` | Existing static void forwarder with no parameters. |
| `static_void_arity1` | Existing reference parameter and one value parameter shape. |
| `static_void_arity3` | Existing multi-argument action interface. |
| `static_void_arity8` | Maximum supported static arity. |
| `static_func_arity0` | Existing zero-argument function return. |
| `static_func_arity1` | Existing one-argument function return. |
| `instance_void_arity1` | Existing receiver plus one user parameter. |
| `instance_func_arity2` | Existing receiver plus two user parameters and return. |
| `generic_method` | Existing unconstrained method type parameter. |
| `generic_type` | Existing generic containing type and receiver qualification. |
| `nested_types` | Existing nested containing type path. |

All eleven outputs and their generated behavior are compatibility controls. Their
public generated type names remain unchanged; their hint names should move to
the complete metadata-derived scheme along with every other inliner.

### Category B: escaped and collision-prone identifiers

| Case | Description |
|---|---|
| `keyword_method` | `[InlinerStruct]` on a method declared as `@class`; generated struct name and call remain legal. |
| `keyword_declaring_type` | Method inside a contextual-keyword type such as `@event`; every generated type reference is escaped. |
| `keyword_parameter` | Parameter named `@value`; generated interface and forwarding call preserve a legal source representation. |
| `keyword_generic_parameter` | Generic parameter named `@delegate`; constraints and all generated uses remain legal. |
| `keyword_inlinable_host` | `[Inlinable]` method with keyword host/parameter names; twin signature and `Invoke` call compile. |
| `same_method_name_different_namespaces` | Two `Alpha.Samples.Run`/`Beta.Samples.Run` declarations produce distinct hints. |
| `same_method_name_nested_types` | `OuterA.Inner.Run` and `OuterB.Inner.Run` produce distinct hints. |
| `same_sanitized_metadata_names` | Symbols whose namespace/type spellings differ only by characters sanitized for hint names still receive distinct deterministic hints. |
| `generic_method_name_collision` | Generic and nongeneric hosts with the same simple method name do not overwrite generated files. |

### Category C: generic constraints

| Case | Description |
|---|---|
| `generic_method_unmanaged` | Forwarder for `Transform<T>(T value) where T : unmanaged` emits the same constraint and compiles with an unmanaged instantiation. |
| `generic_method_multiple_constraints` | `where T : class, IFoo, new()` (or the legal equivalent ordering) is copied exactly enough to compile and preserve semantics. |
| `generic_method_notnull_interface` | `where T : notnull, IFoo` remains valid under nullable analysis. |
| `generic_containing_type_constraints` | Generated nested inliner inside `Box<T> where T : unmanaged, IMarker`; enclosing constraints remain valid in all generated references. |
| `nested_generic_containing_types` | Multiple enclosing generic types with independent constraints retain their parameter lists and references. |
| `inlinable_generic_host_constraints` | Generated twin adds its inliner type parameters without dropping constraints from the original generic host method. |
| `inlinable_multiple_generic_delegates` | Two `[Inlinable]` parameters with different `T`/`R` relationships generate distinct constrained inliner parameters. |

### Category D: type and parameter surface

| Case | Description |
|---|---|
| `nullable_reference_parameter_and_return` | Nullable annotations are retained in interface type arguments, return type, and call. |
| `tuple_and_nested_generic_types` | Tuple, `List<T>`, and nested constructed types remain correctly qualified in generated signatures. |
| `static_and_instance_same_simple_name` | Static and instance methods with otherwise identical simple names generate separate valid nested structs. |
| `maximum_arity_with_generic_constraints` | Arity eight plus generic constraints remains within the supported interface family. |
| `inlinable_nullable_conditional` | Existing nullable conditional invocation remains a valid twin and keeps its nullable contract. |
| `inlinable_alias_and_multi_use` | Existing alias/transitive/ternary/multiple-inlinable cases remain structurally unchanged; this is a non-regression group for the new metadata plumbing. |

### Category E: diagnostic boundaries

These are retained in the existing diagnostic suite rather than turning invalid
inputs into generated snapshots.

| Case | Expected result |
|---|---|
| `SPIRE017_*` ref/in/out/ref-readonly/params parameters | Existing diagnostic; no generated forwarder. |
| `SPIRE018_ref_struct` | Existing diagnostic; no generated forwarder. |
| `SPIRE019_*` arity over eight | Existing diagnostic; no generated forwarder. |
| `SPIRE020_struct_name_collision` | Existing diagnostic for a user member collision. |
| `SPIRE027_*` non-partial containing/declaring type | Existing diagnostic. |
| `same_hint_after_qualification` | No diagnostic; complete metadata identity yields a unique hint without global collision bookkeeping. |

The hint-identity cases must assert both the generated hint inventory and the
absence of duplicate generated source. The same metadata identity must map to
the same hint across compilations, while distinct identities must map to
distinct hints. A collision must not be “fixed” by dropping one generator
result or by changing the public generated type name.

## Section 2: Behavioral type definitions

These types belong in the existing `tests/Houtamelo.Spire.BehavioralTests/Types`
surface. They are intentionally small and executable under the current bridge;
the native Unity runs must include the new fact identities in their independent
inventory rather than restoring a hard-coded count.

| Type | Variants/methods | Purpose |
|---|---|---|
| `EscapedInlinerTargets` | Static `@class(int @value)`, generic `Echo<T>(T @value) where T : unmanaged` | Keyword escaping, generic constraint propagation, static forwarding. |
| `ConstrainedInlinerTargets` | `Create<T>(T value) where T : class, IMarker, new()` and `Use<T>(T value) where T : unmanaged, IMarker` | Multiple constraint forms and actual calls. |
| `NestedGenericInlinerTargets<TOuter>` | Nested `Inner<TInner>` with `where TOuter : unmanaged`, `where TInner : IMarker` | Enclosing generic type references and constraints. |
| `CollisionNamespaceA.SameNameTargets` | Static `Run(int)` and one `[Inlinable]` host | First side of a hint collision. |
| `CollisionNamespaceB.SameNameTargets` | Static `Run(int)` and one `[Inlinable]` host | Second side of a hint collision; distinct namespace. |
| `CollisionOuterA.Inner` | Static `Run(int)` | Nested metadata collision control. |
| `CollisionOuterB.Inner` | Static `Run(int)` | Nested metadata collision control. |
| `EscapedInlinableTargets` | Generic `Map<T,R>(T value, [Inlinable] Func<T,R> selector)` with nullable/constraint variants | Twin generation, delegate replacement, and runtime result. |
| `ForwarderReferenceTargets` | Static/instance methods returning nullable references, tuples, and nested generics | Signature and call type preservation. |

Use simple marker types (`IMarker`, a reference implementation with a public
parameterless constructor, and an unmanaged marker struct) in the behavioral
test project. Do not put invalid ref-like or unsupported modifier examples into
this executable surface; those remain diagnostic fixtures.

## Section 3: Behavioral test assertions

The current six inliner behavior facts stay intact. Add the following tests to
the existing behavioral test class or a narrowly named companion class.

### Category A: forwarder execution

| Test | Type | Assertion |
|---|---|---|
| `EscapedMethodForwarderExecutes` | `EscapedInlinerTargets` | Invoke the generated `@class` inliner and verify the returned value. |
| `EscapedParameterForwarderExecutes` | `EscapedInlinerTargets` | Invoke with a keyword-named parameter path and verify the value is passed once. |
| `UnmanagedGenericForwarderExecutes` | `EscapedInlinerTargets` | Instantiate `Echo<int>` through the generated inliner and verify the result. |
| `ClassInterfaceNewConstraintForwarderExecutes` | `ConstrainedInlinerTargets` | Instantiate a reference type satisfying all constraints and verify the generated call. |
| `NestedGenericReceiverForwarderExecutes` | `NestedGenericInlinerTargets<int>.Inner<Marker>` | Invoke a nested instance inliner; verify receiver and payload values. |
| `NullableTupleForwarderExecutes` | `ForwarderReferenceTargets` | Verify nullable return and tuple element values survive the forwarder. |

### Category B: `[Inlinable]` twin execution

| Test | Type | Assertion |
|---|---|---|
| `EscapedInlinableTwinExecutes` | `EscapedInlinableTargets` | Pass a generated inliner to the twin and verify the transformed result. |
| `ConstrainedInlinableTwinExecutes` | `ConstrainedInlinerTargets` | Use a generic constrained selector; verify overload resolution selects the twin and executes once. |
| `MultipleInlinableParametersExecute` | Existing `Pipeline` plus a constrained extension | Pre/post generated inliners both execute in order. |
| `NullableConditionalInlinablePreservesResult` | Existing `Notifier` pattern | Null callback does nothing; non-null callback executes once. |
| `AliasInlinableUsesOneCall` | Existing alias cases promoted to behavioral type | A variable/ternary/transitive alias still invokes the generated inliner once. |

### Category C: hint and generated-surface contracts

These can be compile-time behavioral facts plus reflection over the loaded
assembly/source metadata.

| Test | Assertion |
|---|---|
| `SameSimpleNamesProduceDistinctGeneratedHints` | Both namespace-collision inliners are present and callable; no generated source was lost or overwritten. |
| `NestedSimpleNamesProduceDistinctGeneratedHints` | Both nested collision controls compile and execute. |
| `GeneratedGenericConstraintsRemainUsable` | A call site that requires `unmanaged`, `notnull`, and interface/new constraints compiles and executes. |
| `EscapedGeneratedIdentifiersRemainCallable` | Reflection/source invocation proves generated names are legal and the public inliner type is discoverable. |

### Category D: native Unity validation

The existing Unity behavioral bridge must run these same facts in Editor Mono,
the release Mono player, and the release IL2CPP player with High stripping.
The acceptance check must compare exact fact identities and retain the current
backend/runtime markers. No “preparation-only” result counts as native proof.

## Intentional snapshot changes

The eleven existing baseline cases and all 24 existing performance compatibility
fixtures remain controls. New expected files are required for escaped names,
constraints, and collisions. The public generated types and runtime behavior
must remain unchanged, while their hint names are expected to move to the
complete metadata-derived scheme. Update only the fixture manifests and
expected outputs whose source inventory changes; record one representative
metadata-to-hint mapping in the compatibility manifest. Never regenerate the
entire corpus as a shortcut.

No test in this matrix changes the separate `InlinableBodyRewriter` scope,
unsupported modifier diagnostics, public inliner interface arity, or generated
runtime semantics beyond making previously valid source compile and execute.

