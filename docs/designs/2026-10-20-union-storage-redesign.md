# Discriminated union storage redesign

Status: Implemented and validated on 2026-10-23. Current decisions are recorded first.
The earlier field-level storage proposal is preserved below as design history.
The execution status below distinguishes implemented work from pending work.

Design scope: preserve the existing design wherever it does not conflict with
the approved redesign. Discuss new policy only where the new representation or
API requires resolving a conflict. Previously supported settings and behavior
are not open decisions merely because their implementation needs updating.

## Latest decision: variant-payload layout

Approved on 2026-10-22: generated per-variant unmanaged payload structs use
`[StructLayout(LayoutKind.Auto)]`, with no `Pack` setting. Accept the runtime's
chosen layout and padding. Do not add generator-driven field reordering or a
packing search; users needing bespoke storage optimization can author their own
payload types and layout attributes.

This applies to the per-variant payload structs. In the proposed representation,
their surrounding unmanaged overlap helper still uses Explicit layout with each
variant payload at offset zero. User-authored field types retain their own layout
attributes.

This replaces the earlier field-level packing machinery. The public API and
configuration decisions below supersede the older proposal where they differ.
No generator implementation is included in these decisions.

Approved on 2026-10-23: the redesign does not compute unmanaged type sizes or
alignments. Remove the known-layout Unity struct catalog from the planned
implementation and maintenance requirements, including generated size tables,
target profiles, live Unity measurement scripts, and single-field size recursion.
Eligible concrete unmanaged fields use runtime-laid-out payloads without a
size-based type whitelist. Unmanaged classification and generic-dependency checks
still determine storage eligibility; they do not require calculating byte sizes.
The existing catalog surveys, CSV selections, and measurement proposals remain
historical research, not an initial support list or a prerequisite for the rework.

The [Auto/Pack investigation](../reviews/2026-10-22-variant-payload-auto-layout-pattern-order.md)
records the measured padding and backend differences that informed this choice.

## Current API decisions

Approved on 2026-10-22:

- Public variant payloads are ordinary structs with constructors and `Deconstruct`,
  rather than record structs. The later field-visibility decision below supersedes
  their original public-field surface. Reuse them as
  serializable data for Unity-serializable unions, with the shared wrapper
  described below. This supersedes the earlier readonly-payload decision.
  Variant-qualified accessors replace the old flat payload properties; do not
  generate both APIs.
- Remove Additive as an alternative value-storage mode. Concrete unmanaged
  variant payloads use the overlapping representation described above. Remove
  `ValueStorage` entirely rather than expose an enum with one effective choice.
  The earlier removals of the boxed and unsafe-byte strategies still apply.
- Reject explicit-layout overlap of managed references. Reference storage uses
  ordinary typed slots or shared `object?` slots, selected by `ReferenceStorage`.
  Do not generate helpers that expose unrelated reference types at the same
  offset. This supersedes the earlier approval of typed reference overlays.
- Retain `ReferenceStorage`: Typed prioritizes direct typed access; Shared
  prioritizes compact storage and may box structs containing managed references.
- Default to `ReferenceStorage.Auto` when no explicit union or project override
  selects another mode. Retain explicit Typed and Shared overrides.
- Configure reference storage through a named attribute property, such as
  `[DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]`, rather
  than a constructor argument. Omission inherits the project default, falling
  back to Auto. An explicitly supplied Auto selects automatic storage even when
  the project default differs. Both omission and an explicit ReadGlobalCfg use
  the project-default resolution described below.
- No compatibility or migration layer is required for the old `Layout` API,
  configuration, or serialized assets: the asset has not been released.
- Maintain the shared Editor implementation as a C# source template embedded
  in the generator and emitted once per compilation. Replace the escaped
  StringBuilder-based implementation; do not require a shipped Editor DLL.
- Discuss the remaining configuration choices one topic at a time.

Approved, revised on 2026-10-25: a sibling nongeneric `{UnionName}Types` static
class groups generated payloads under `Variants` and runtime storage under
`Fields`. Shape produces `ShapeTypes.Variants.Circle` and `.Rectangle`. A Command
nested inside Combat produces `Combat.CommandTypes.Variants.Run`. The container
matches the union's accessibility; `Variants` and its payloads are public within
it, while `Fields` and storage remain internal. `Fields.Managed` holds managed
slots, and explicit-layout `Fields.Unmanaged` contains the per-variant Auto-layout
types and overlay fields. Generic unions retain the arity suffix on the nongeneric
container (`ShapeTypes1` for `Shape<T>`); payloads individually copy the union's
type parameters and constraints. Alias the nongeneric `Fields` and `Variants`
containers when accessible and unambiguous. Nested payloads retain their containing
type's generic context in emitted source, but the generic-containing-type
restriction below makes that nesting a reported error.

Approved on 2026-10-23:

- Remove the GenerateDeconstruct enum, the attribute's constructor argument and
  property, and the Spire_DU_DefaultGenerateDeconstruct MSBuild setting. Variant
  payload structs always receive their typed Deconstruct methods, with no
  configuration opt-out. Remove the old union-level Deconstruct overloads that
  returned Kind and variant field values; positional matching uses the variant
  accessor's payload instead. Include the associated parsing, configuration,
  documentation, and test updates in the rework.
- Payloads have the union's effective accessibility through the outer types
  container, including private accessibility for nested unions.
- Payload types copy the union's own type parameters and constraints. Type
  parameters from containing types remain inherited through nesting in generated
  declarations, including output retained for diagnosed invalid input.
- Leave generated payload type and accessor name collisions to the C# compiler.
  The factory-conflict suffix below is the one approved naming exception. Do not
  add custom collision diagnostics or other automatic renaming rules.
- A union may itself be generic, but may not be declared inside another generic
  type. Report an error for this nesting and continue generating its code. Do not
  relocate internal helpers outside the offending ancestor to support it.

### Payload field visibility

Approved on 2026-10-23: generated payload structs are non-partial. Each parameter
keeps its original name as a public getter-only property, backed by a private
field prefixed with `_`. Constructors assign the backing fields; `Deconstruct`
reads them. Unity serialization attributes stay on those fields, so Unity stores
`_target` and `_duration` while C# patterns continue using `target` and `duration`.
The fields are not readonly, allowing native Inspector edits. The shared wrapper's
public `value` field is unchanged, and union JSON names remain unchanged.
This supersedes the earlier mutable-public-field surface.

### Unity serialization overhead

Approved on 2026-10-24: the only additional instance storage for Unity
serialization is `_serializedPayload`. Do not generate a hydration flag or other
per-instance bookkeeping. Players encode reconstructed state in that existing
reference using one shared marker, releasing the concrete serialization cache.
Editor caches remain available for stable serialized output. Repeated explicit
hydration preserves runtime values; a new native deserialization callback still
performs payload-authoritative recovery.

### Variant accessor names

Approved on 2026-10-23: derive each variant accessor name using camelCase,
normalizing leading acronyms. Preserve the factory's declared name. If the
derived property name matches a variant factory name in the union, append
`_value` to the property name. Compare unescaped identifiers case-sensitively.

| Factory | Default accessor | Accessor when the default conflicts with a factory |
|---|---|---|
| Circle | circle | circle_value |
| ApplyEffect | applyEffect | applyEffect_value |
| HTTPRequest | httpRequest | httpRequest_value |
| URL | url | url_value |
| run | run | run_value |

Append the suffix once. If the resulting name still conflicts, or multiple
accessors normalize to the same name, leave the remaining conflict to the C#
compiler. This includes conflicts with user-declared fields or properties on
another partial declaration, existing payload types, and occupied suffixed names.
Do not rename factory methods or payload types as part of this rule.

### Explicit project-default selection

Approved on 2026-10-23: keep JsonLibrary.ReadGlobalCfg and add
ReferenceStorage.ReadGlobalCfg. Explicitly requesting project configuration is
part of the API even though the generator can detect omitted properties. This
supersedes the earlier decision to omit the ReferenceStorage sentinel and the
proposal to remove the JsonLibrary sentinel.

- ReferenceStorage has ReadGlobalCfg, Auto, Typed, and Shared. Its attribute
  property defaults to ReadGlobalCfg. Omission or explicit ReadGlobalCfg inherits
  the project default, falling back to Auto. Explicit Auto runs automatic slot
  selection regardless of the project default; Typed and Shared also override it.
- JsonLibrary retains ReadGlobalCfg, None, SystemTextJson, and NewtonsoftJson.
  Its attribute setting defaults to ReadGlobalCfg. Omission or explicit
  ReadGlobalCfg inherits the project default, falling back to None. Explicit None
  disables converter generation even when the project default enables it.
- ReadGlobalCfg selects configuration inheritance; it does not itself select a
  storage representation or JSON converter library. Resolve it before applying
  the selected storage policy or generating converters.

### Existing JSON design retained

Approved on 2026-10-23: retain the existing JSON configuration and behavior.
The new storage representation requires adapting converter implementation, not
redesigning JSON settings or the serialized format.

- Preserve converter-library selection, flag combinations, ReadGlobalCfg,
  project-default inheritance, and the None fallback described above.
- Preserve JsonDiscriminator: omission or explicit null uses
  Spire_DU_DefaultJsonDiscriminator, falling back to "kind". An explicit string
  overrides the project default.
- Preserve existing JSON naming customization and logical variant/payload
  serialization behavior. The new runtime helpers and Unity wrapper are
  implementation details, not a reason to change JSON output.
- Make no additional JSON API or default changes unless a concrete conflict
  with the approved redesign requires one. The explicitly approved removal of
  Layout and GenerateDeconstruct still applies to the attribute API.

### Reference storage

| Mode | Representation | Access and allocation |
|---|---|---|
| ReadGlobalCfg | Resolve the project default, falling back to Auto. | Delegates to the resolved mode. |
| Typed | Ordinary typed slots, reusable by compatible type across mutually exclusive variants. Reference nullability does not distinguish slot types. | Direct typed reads without object-to-payload casts. Structs containing managed references stay inline without boxing. |
| Shared | Ordinary `object?` slots, reusable across different reference types and reference-containing structs in mutually exclusive variants. | Checked casts or unboxing on reads. Reference-containing structs are boxed when stored. |
| Auto | Compare reference-storage slot counts after reuse. Select Shared when its count is at most half Typed; otherwise select Typed. Exclude common union storage from the comparison. | Auto may introduce boxing. Every managed storage slot counts as one, including inline reference-containing structs; this is not a total union-size comparison. |

Fields present together require separate slots in either mode. Neither mode
overlays managed reference fields through explicit layout. The unmanaged payload
overlay remains separate from reference storage. Generic-dependent values retain
the typed fallback below.

Shared optimizes storage reuse; Typed avoids its casts and unboxing. Auto is the
default mode.

Approved on 2026-10-22: Auto compares only reference-storage slots, replacing both
the earlier total-inline-byte comparison and its total-field-count fallback.
Resolve the comparison during generation without runtime size probing or manual
prediction of Auto layout. The discriminator, unmanaged helpers, generic-dependent
typed fallback fields, serialization cache, and generated container wrappers do
not contribute. Look through ManagedFields and count its relevant slots after
reuse. With no reference-storage candidates, select Typed.

For reference-typed fields and structs containing managed references:

- Typed reuses fields of the same type across variants, ignoring reference
  nullability. Its count is the sum of the maximum simultaneous field counts for
  each type.
- Shared reuses object slots across variants. Its count is the maximum number of
  reference-storage candidate fields present together in any variant, including
  structs that Shared would box.
- Select Shared when `SharedSlots * 2 <= TypedSlots`; otherwise select Typed.

For example, four variants with one distinct reference type each need four Typed
slots or one Shared slot, so Auto selects Shared. If all four fields have the same
type, both representations need one slot, so Auto selects Typed. Field names do
not prevent compatible slot reuse.

Approved: each managed storage slot counts as one, whether it holds a reference
or an inline struct containing references. Do not inspect or recursively count
the contents of those types. For example, a struct with two reference members
and an int counts as one Typed slot or one Shared box-reference slot. Apply
same-type reuse before counting; simultaneous fields still need separate slots.
This deliberately treats each slot equally without claiming equal byte sizes.
Auto may box reference-containing structs when Shared wins the overall count.

### Generic-dependent fields

The existing fallback decision remains sufficient: fields depending on unresolved
generic parameters stay in ordinary typed storage. A variant accessor constructs
its public payload from both that storage and the concrete unmanaged helper.
This requires no new storage policy or discriminator-based location selection.

For example, for `Command<T>.A(T value, float duration)` with `T : unmanaged`,
the concrete `duration` belongs to A's overlapping unmanaged payload, while
`value` remains in a typed `T` slot outside the explicit-layout helper. The
accessor returns `new CommandA<T>(typedSlot, unmanaged.a.duration)`. The public
payload may be generic; the explicit-layout helper may not capture `T`.

### Reuse public payloads for Unity serialization

Approved: make public variant payloads ordinary
`[Serializable]` structs with constructors and Deconstruct, and
reuse those types as the serialized data for Unity-serializable unions. This
replaces the earlier readonly-payload decision. Their private fields and public
getters follow the field-visibility decision below. Emit Unity field annotations according to each
parameter's serialization policy, including SerializeReference where requested.
Fields must still satisfy Unity's normal serialization rules.

The current serializer stores only the active variant through a managed-reference
class. Unity does not support structs, even boxed ones, as SerializeReference
values. Preserve the single active-payload representation with one shared generic
class declaration, rather than a separate serializer class declaration per variant:

```cs
[Serializable]
internal sealed class SerializedPayload<T> : UnitySerializationPayloadBase
{
    public T value;
}
```

For a Circle value, the managed-reference field holds a
`SerializedPayload<ShapeTypes.Variants.Circle>` instance whose `value` is serialized inline.
This reuses the payload field declarations and reduces generated serializer type
definitions. Different closed generic wrapper types still exist at runtime; it
does not eliminate every variant-specific runtime type or the allocated active
serialization snapshot. Runtime unmanaged-subset helpers remain a separate matter.

Keep the single active-payload field, rather than separate inline serialized
fields for every variant. Do not generate separate per-variant serializer class
declarations alongside the reusable public structs.

The [serialization reuse probe](../reviews/2026-10-22-variant-payload-auto-layout-pattern-order.md#reusing-public-payload-structs-for-serialization)
validated generic class wrappers around two mutable structs using native assets,
editing through SerializedProperty, nested managed references, and a fresh Editor
process reload on Unity 6000.5.6f1. This is representation evidence, not a generator
implementation or Player validation.

### Selected Editor drawer flow

Approved: retain isolated preview editing and unchanged-snapshot reuse, adapted
to the reusable public payload structs. Keep a small generated adapter for
variant construction and payload-to-union conversion, plus one shared Editor
implementation per compilation. This supersedes the earlier choice to bind
directly to the asset without a preview.

1. Clone the active serialization wrapper and inline serialized data into a
   temporary hidden ScriptableObject. Opening, binding, and repainting the
   Inspector must not replace the asset's payload or change its serialized IDs.
   Preserve intentional user SerializeReference and UnityEngine.Object sharing;
   isolating infrastructure wrappers does not require deep-cloning those objects.
2. Serialize the existing kind field and present it with Unity's normal enum
   control through the isolated editing flow. This changes the current scheme,
   which derives a nonserialized runtime kind from the serialized payload type.
   Variant changes initialize the matching preview payload before committing.
3. Draw the preview's `payload.value` with one native PropertyField and
   serialized-property binding. Let Unity handle ordinary field editing and
   supported field attributes. Use the same isolation mechanism for UI Toolkit
   and IMGUI; the current IMGUI fallback edits actual payload fields directly.
4. Commit actual value or variant edits by constructing a replacement union and
   writing it into the specific asset field. Do not mutate a shared generated
   wrapper in place. Keep the preview synchronized with external asset changes
   and undo/redo without writing back unchanged preview state.
5. Reconstruct optimized runtime storage through OnAfterDeserialize after native
   deserialization. Retain OnBeforeSerialize to prepare snapshots from
   programmatic runtime changes: reuse the current wrapper when its serialized
   fields still match and replace it when they differ. Keep callback work limited
   to managed data operations.
6. Bridge commits and undo/redo to the actual asset, rather than recording only
   preview changes. Prefer Unity's native undo APIs. Removing the current custom
   undo history requires validation; retaining a preview does not establish that
   the existing bookkeeping is either necessary or redundant. Prefab and
   multi-object behavior also require validation.
7. Preserve normal control focus during value edits. Do not manually rebuild
   the payload controls for ordinary field changes; structural variant changes
   can replace the payload UI.

Maintain the shared Editor implementation as a C# source template embedded in
the generator and emitted once per compilation, rather than escaped StringBuilder
statements. This retains the selected distribution model without shipping an
Editor DLL. The adapter must provide actual union conversion; T alone does not
describe how to rebuild the enclosing union.

The [native data-flow prototype](../reviews/2026-10-22-variant-payload-auto-layout-pattern-order.md#native-inspector-data-flow)
on Unity 6000.5.6f1 Editor Mono demonstrated callback reconstruction and native
undo/redo without a preview, but also the alias problem: editing one copied
union's radius from 7 to 12 changed both copies. Direct native editing therefore
does not replace the isolation provided by the current preview architecture.

A [fresh-snapshot prototype](../reviews/2026-10-22-variant-payload-auto-layout-pattern-order.md#fresh-serialization-snapshots)
isolated the copied values. The subsequent
[save-stability probe](../reviews/2026-10-22-variant-payload-auto-layout-pattern-order.md#unchanged-save-stability)
showed unchanged dirty saves and forced reserialization changed wrapper IDs,
while matching-wrapper reuse kept every tested rewrite byte-for-byte stable.
Do not allocate a fresh wrapper on every OnBeforeSerialize as the isolation policy.

The selected preview flow preserves the current architecture's isolation boundary
while simplifying it around `SerializedPayload<T>`. This decision does not prove
that existing undo, synchronization, or field-specific workarounds can all be
removed; validate each simplification against the new representation.

### Deserialization recovery

Approved: retain the previous serializer's payload-authoritative recovery policy.

- A recognized active payload restores its variant and field values. Derive the
  runtime kind from the payload type and synchronize the serialized kind to match,
  even if its saved value names another variant or is invalid. A kind mismatch
  alone does not discard valid payload data or trigger the invalid-payload fallback.
- A null, missing, or unrecognized payload restores the first declared variant
  with default field values, replacing any previous runtime state. This also
  applies when the saved kind is otherwise valid; do not fabricate a different
  variant from that kind. On native deserialization, log
  `invalid union serialization data, defaulting to {FirstVariantName}`.
- Subsequent serialization writes the recovered canonical state. Report invalid
  data during deserialization, rather than repeatedly during Inspector repaint.

The current serializer implements the recognized-payload and first-variant
fallback rules in [UnitySerializationEmitter](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Emit/UnitySerializationEmitter.cs).
The redesign adds a serialized kind field, so correcting it from a recognized
payload is the adaptation of the existing behavior. Intentional Inspector variant
changes initialize the matching preview payload before commit and therefore do
not pass through mismatch recovery.

### Helper placement and generic containing types

Group generated types under the sibling `ShapeTypes` static class. Public
payloads live under `Variants`; implementation-only data lives under internal
`Fields`. Nested data types remain ordinary instance-data structs. The shared
container follows the union's accessibility and remains nongeneric.

For supported input, the container holding explicit-layout helpers is nongeneric
and outside the union itself, including when the union is generic. Generic-dependent
fields remain in ordinary typed slots. A generic union such as Command<T> is
supported at namespace scope or within a nongeneric containing type.

Approved on 2026-10-23: report an error when any ancestor of the union declares
generic type parameters, including when the union itself declares none. Do not
support this case by moving the helper group outside the generic ancestor.
Check the complete containing-type chain; an intervening nongeneric declaration
does not make nesting inside a generic ancestor valid. Identify the nearest
ancestor that declares generic parameters as OuterOffendingClass and locate the
error at the union declaration.

Use the requested diagnostic wording, substituting the two type names:

```text
Discriminated Unions require using `[StructLayout(LayoutKind.Explicit)]` internally, which C# forbids inside generic types, please move the type `{DiscriminatedUnionName}` to outside the type `{OuterOffendingClass}`. Note: The type {DiscriminatedUnionName} itself can be generic, it just can't be placed inside another generic type such as `{OuterOffendingClass}`.
```

Report the diagnostic without suppressing generation: preserve the union's
factories, variant payload types, accessors, and other generated declarations in
their normal scopes. The diagnostic prevents successful compilation; continued
generation prevents unrelated missing-generated-member cascades. This replaces
the earlier proposal to lift storage helpers outside a generic containing type.

The [C# specification](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/classes#15397-nested-types-in-generic-classes)
defines types nested in generic classes as implicitly generic. The installed
Unity 6000.5.6f1 IL2CPP loader rejects generic explicit-layout types in
`Editor/Data/il2cpp/libil2cpp/vm/Class.cpp` with
`because generic types cannot have explicit layout.` The earlier
[Mono/IL2CPP probe](../reviews/2026-10-20-unity-unsized-overlap.md)
validated embedding concrete nongeneric helpers in a generic carrier; it did not
permit making the explicit helpers themselves generic.

### Diagnostic reporting and generation continuity

Approved: reporting an error must not itself stop source generation. Preserve
the declaration information needed to emit the expected API even for diagnosed
invalid input, so the actionable Spire diagnostic is not buried under C# errors
about missing generated functions or types. Separate diagnostic reporting from
emission; error severity is not an emission gate.

The rework includes a review and repair pass across Houtamelo.Spire's existing
source generators, parsers, and declaration models. Find and fix paths that report
an error and suppress otherwise constructible generated API, including parsers
that discard variants or containing-type metadata before emission. Review Unity
drawer/support emission gates as well as the primary runtime output. Extend the
pass to the performance generators, not only discriminated unions.

Current starting points, verified by code inspection on 2026-10-23:

- [DiscriminatedUnionGenerator](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/DiscriminatedUnionGenerator.cs):
  the blanket IsError return, post-validation returns, and CanEmitUnityDrawer gates.
- [UnionParser](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Parsing/UnionParser.cs):
  the generic-overlap error model discards variants and containing types;
  ResolveGlobalConfig also skips error-bearing models.
- [UnionDeclaration](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Model/UnionDeclaration.cs):
  its diagnostic contract currently specifies that errors suppress output.
- [InlinerStructGenerator](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Performance/InlinerStructGenerator.cs):
  reporting a diagnostic currently returns before emission. Audit its model and
  sibling generators before choosing how to preserve their expected APIs.

These are audit entry points, not a completed repair or exhaustive inventory.
Do not merely remove returns when an earlier stage has already erased the model.

Required regression coverage uses real Roslyn generator-driver compilations,
not mocks. Assert the intended diagnostic and the continued generated API for
invalid input, and assert successful output for valid controls. For this nesting
rule cover direct and indirect generic ancestors, multiple generic ancestors,
a union that is itself generic, and valid nongeneric containers. Verify that
ordinary references to generated members do not acquire missing-member/type
errors solely because a Spire error was reported. Run the relevant tests when
these implementation changes and regression cases are added.

### Design status and implementation work

No policy decisions currently listed in this draft remain unresolved. Accessor
naming, Auto selection, unconditional payload deconstruction, preview isolation,
snapshot reuse, and deserialization recovery are settled. Existing JSON
configuration and defaults are retained. Reopen a policy only if a concrete
conflict with the approved redesign is found.

Simplifying undo/synchronization and validating both drawer paths, focus, prefab
behavior, and multi-object editing remain implementation work. The diagnostic
continuity audit and repairs above are required rework tasks as well.

Approved implementation sequence on 2026-10-23:

| Phase | Delivery | Validation required within the phase |
|---|---|---|
| 1. Core generation | Update the attribute/configuration model, emit payload structs under the sibling types container and their typed Deconstruct methods, generate runtime unmanaged helpers and reference-slot reuse, and replace flat properties with the approved accessors. | Real generator-driver compilations and runtime behavior tests for storage modes, generics, copies, deconstruction, default values, naming, and diagnosed generic ancestors with continued output. Validate generated storage on Unity Mono and IL2CPP. |
| 2. Consumers | Adapt exhaustiveness analysis, switch code fixes, and JSON converters to the new payload API while preserving unaffected behavior and JSON format. | Analyzer/code-fix integration cases using actual generated unions and nested variant patterns, plus JSON round trips and configuration tests for both supported libraries. |
| 3. Unity integration | Reuse public payloads through the shared serialized wrapper, retain isolated previews in both drawer paths, and move shared Editor support into the embedded C# source template. | Native asset save/reload and unchanged YAML/ID stability, copied-value isolation, variant switching, nested/collection edits, rendered focus, undo/redo, prefab and multi-object behavior, and Player reconstruction on Mono and IL2CPP. |
| 4. Review and validation | Complete the generator-wide diagnostic-continuity audit and repairs, remove obsolete layout/deconstruction/catalog machinery from the redesign's active paths, and update affected documentation and examples. | Invalid-input compilations retain the expected generated API alongside the intended diagnostic; valid controls compile and work. Run affected suites and the final Unity integration checks, recording backend/version boundaries and unresolved failures explicitly. |

Add and run each phase's regression coverage while implementing it. Do not defer
all testing until the final review. Assertions should exercise generated code,
real compilation, serialization, or native editing behavior rather than mocks.
The phases above are approved execution order, not a claim that implementation
or its validation has already been completed.

Execution status on 2026-10-23:

- Phase 1 core generation is implemented for the runtime representation, attribute
  API, project defaults, and generic-ancestor diagnostic continuity. The dedicated
  generator-driver suite has 53 passing cases, including emitted-IL checks that
  Typed nullability reuse does not box or cast through object.
- The isolated [native fixture](../../tests/UnityVariantPayloadCore/README.md)
  passes 39 combined runtime/serialization assertions on each Unity 6000.5.6f1
  Linux x64 backend, Mono and IL2CPP, using release players with High stripping.
  Both isolated players exit successfully. Native Editor recovery passes 59 checks;
  persistence Write and fresh-process Read pass 92 and 53 checks respectively,
  including callback isolation and a fresh-process no-op save with unchanged IDs.
  CLI regression coverage passes 8 cases.
- Phase 2 consumer adaptation is implemented: analyzer, code-fix, and both JSON
  adapters pass 88 focused cases. With core generation, that is 141 passing cases.
- Phase 3 serialization and drawer source is implemented and passes 11 additional
  structural cases against Unity references. Four additional C# 9/metadata cases
  cover partial declaration opt-in and field annotations. Native
  persistence, recovery, and stripped Player reconstruction pass. The rendered
  Editor gate passes 16 cases and 147 checks with zero failures or unverified
  cases, including focus, shared-node Undo/Redo, external changes, prefabs,
  multi-object editing, collections, and native IMGUI input without refocusing.
- Phase 4 removed obsolete emitters, layout/deconstruction settings, and size
  calculation. The generator-wide diagnostic audit repairs pass 12 new real-driver
  tests, and performance-generator coverage passes 43 cases. The full migrated
  generator suite passes 589 cases with zero skips, including review regressions
  for accessor ownership, escaped JSON names, nullable positional code fixes,
  and benchmark storage containing generated managed unions.
  The compile-time behavioral suite passes 73 cases. The migrated showcase passes
  16 native EditMode tests and preserves its edited values and IDs. Tooling and
  packaging pass 94 cases. The final package's four assemblies match source builds
  and the exact package used by both passing release Players; UnityShowcase resolves
  to that export. No required implementation work remains.

Player coverage has one documented boundary: Unity can canonicalize malformed
Resources during its build. Native malformed-asset callback execution is not
claimed by those Player checks; Editor recovery and direct generated Player
fallback are verified. This does not change the approved recovery policy.

The [reference-overlap probe](../reviews/2026-10-22-variant-payload-auto-layout-pattern-order.md#overlapping-reference-fields)
passed on release Unity 6000.5.6f1 Linux x64 Mono and IL2CPP Players. Reading an
inactive reference view bypassed the normal cast check and produced invalid
array metadata when dereferenced. The design rejects this technique despite
runtime support. The accepted kind-first evaluation assumption remains settled.

Large-payload copy benchmarks are out of scope for this redesign, not pending
validation work.

## Earlier field-level storage proposal

The sections below retain the earlier proposal, including its XLA packing,
known/unknown-size partitioning, and catalog workflow. Those packing rules do not
apply to the per-variant Auto payloads selected above. References below to
retaining Additive, exposing ValueStorage, or migration work are historical and
are superseded by the current API decisions. The older Auto comparisons of total
union bytes or total generated fields are superseded by reference-slot counting.
The former official Unity catalog target, its maintenance and measurement
workflow, and all pending size/alignment-discovery decisions are retired from the
redesign. They do not constrain eligible types in the current proposal.
The older restriction against looking inside any helper for field counts is also
superseded for the managed-data container by the approved counting rule above.
The earlier support for unions inside generic containing types is superseded by
the error policy above; a union's own generic parameters remain supported.

### Confirmed decisions

1. Replace the struct-union `Layout` selection with independent `ValueStorage`
   and `ReferenceStorage` enums. Enum member names and the attribute API are
   provisional.
2. Eliminate `BoxedFields` and `BoxedTuple`. Their historical CoreCLR use cases
   are acknowledged; the redesign prioritizes Unity, where heap allocations
   are costly, and a smaller set of storage policies.
3. For Additive storage, reuse slots by type across variants regardless of field
   names. Fields belonging to the same variant must occupy distinct slots.
   Optimize this mode without requiring unsafe code or field offsets, subject
   to the stable-location constraint below.
4. Reference nullability annotations do not distinguish storage types:
   `Class` and `Class?` can share the same typed slot across variants.
5. Each generated payload property must have one storage location independent
   of the active variant. Compatible occurrences of the same property across
   variants use that same slot or offset. Accessors must not inspect `kind` to
   select a storage location. Accept larger storage when necessary to preserve
   this constraint; do not rely on the compiler eliminating repeated checks.
6. Offset-based storage may partially overlap the byte ranges of different
   unmanaged types with known sizes when those property groups never occur
   together. Preserve the required alignment and each property's fixed offset.
7. Use XLA's greedy best-fit packing heuristics, adapted to variant coexistence,
   for known-size unmanaged byte-range placement. Accept heuristic packing
   quality and choose the smaller result from the two orderings described below.
8. Limit type-layout discovery to primitive types, enums, recognized Unity
   structs, and ordinary single-instance-field wrappers around supported types.
   Do not implement general recursive layout calculation for multi-field user
   structs. The single-field eligibility rules are described below.
9. For offset-based storage, put all eligible known-size unmanaged fields into
   one value-data helper packed with the selected XLA heuristics. Use additional
   offset-zero slot helpers only for concrete unmanaged structs with unknown
   size. Keep these two storage regions separate inside an ordinary-layout union.
10. Assign unknown-size groups by reusing the placement algorithm in unit-slot
    coordinates. Each group occupies one logical slot with unit alignment and
    its exact variant-membership conflict set. Interpret the resulting offset
    as a helper index; every emitted field remains at byte offset zero inside
    its helper. Preserve type identity and prefer compatible same-type reuse
    before cross-type placement, as specified below. Actual sizes remain unknown.
11. Adopt the [664-type Unity list](unity-initial-layout-targets.csv) as the
    official initial catalog target. This includes 333 retained engine selections,
    40 engine additions and 291 accepted optional-package suggestions. Membership
    is settled; size/alignment validation and supported target profiles remain pending.
12. Populate the layout catalog through a manually invoked Unity measurement
    workflow. Generate concrete generic calls to `UnsafeUtility.SizeOf<T>()` and
    `UnsafeUtility.AlignOf<T>()`, retain measurements as JSON, and generate a
    deterministic checked-in `.generated.cs` table in the analyzer project.
    Validate player profiles using Mono/IL2CPP probes; unavailable types and
    unmatched profiles retain unknown layout information.
13. Struct payloads containing managed references follow `ReferenceStorage`.
    Typed stores them inline in ordinary typed slots; Shared boxes them into
    shared `object?` slots. They never enter the unmanaged overlapping helpers.
    This boxing is part of reference storage, not a return of the removed
    `BoxedFields` or `BoxedTuple` layouts.
14. Generic-dependent value fields that cannot enter nongeneric helpers use
    ordinary typed slots, including `T` with an unmanaged constraint. Share
    compatible typed slots across variants under the stable-location rule.
    Other eligible fields may still use overlapping helpers in the same union.
15. When both inline sizes are known, `ReferenceStorage.Auto` selects Shared
    when its total inline union size is at most half the Typed alternative,
    including when this introduces boxing allocations for reference-containing
    structs. Compare the complete inline representations, excluding separately
    allocated boxes and their overhead. Allocation and GC costs do not add a
    separate penalty or veto; users may explicitly select Typed or Shared after
    profiling.
16. If either total inline size is unknown, `ReferenceStorage.Auto` compares the
    total generated instance-field counts of the two outer union representations
    after storage reuse and packing. Select Shared when its count is at most
    half the Typed count; otherwise select Typed. Count unchanged fields too.
    This is a field-count heuristic, not a guarantee of halving the byte size.
17. `ValueStorage.Auto` uses overlapping helpers for eligible concrete unmanaged
    fields in both nongeneric and generic unions. Generic-dependent value fields
    remain in ordinary typed slots. Do not switch the entire union to Additive
    merely because the union or a containing type is generic. Helpers remain
    nongeneric and are declared outside all generic containing types.
18. Drop `UnsafeOverlapUsingBytes` from the redesign; do not carry the existing
    `Layout.UnsafeOverlap` strategy into the new storage API. The planned value
    storage choices are Auto, Additive, and OverlapUsingOffsets. Retain the
    offset-helper approach for overlap in both nongeneric and generic unions.
    Compatibility handling for the removed old API remains part of migration.
19. Unknown-size helper assignment must first reuse a compatible existing slot
    containing the same concrete type. Same-type reuse is also an overlap
    optimization, not an Additive-only feature. Use cross-type best-fit placement
    only when no compatible same-type slot exists. Preserve fixed property
    locations and reject sharing when any assigned groups coexist.

For example, `Teleport(Vector2 target, float duration)` and
`Move(Vector2 destination, float speed)` should share one `Vector2` slot and
one `float` slot. Public field/property names remain independent of the backing
slot assignment.

### Stable storage locations

Treat compatible occurrences of the same generated public property as one
storage group. Each group has one fixed location throughout the generated union.

Groups used together in any variant must occupy disjoint storage. Groups that
never occur together may reuse storage when permitted by the selected storage
policy. Different property names therefore remain eligible for sharing; each
property's assignment stays fixed across all of its occurrences.

For example:

```cs
A(float x, float y);
B(float y, float z);
C(float z, float x);
```

Every pair of properties occurs together somewhere, so this example requires
three float slots: one each for `x`, `y`, and `z`. The float payload occupies
12 bytes, excluding the discriminator and surrounding padding. Two slots would
require a property's location to change between variants, which violates the
decision above.

The byte-range packing algorithm is selected below. Packing each variant
independently is unsuitable when it changes a shared property's location between
variants.

### Packing algorithm: XLA adaptation

Adapt the unsliced placement logic in XLA's `GlobalDecreasingSizeBestFitHeap`
and its `ChooseBestHeapAlgorithm` result selection. The upstream reference is
revision [`fe53c79cbd247bb11fa8b05d32f640be8b2748be`](https://github.com/openxla/xla/blob/fe53c79cbd247bb11fa8b05d32f640be8b2748be/xla/service/heap_simulator/heap_simulator.cc).

Input is one group per shared property, with its known size, required alignment,
variant-membership set, and stable group index. Run the same best-fit placement
with two group orderings:

| Pass | Primary order | Secondary order | Final tie-breaker |
|---|---|---|---|
| Size first | Size descending | Variant count descending | Stable group index ascending |
| Coverage first | Variant count descending | Size descending | Stable group index ascending |

Variant count replaces XLA's lifetime duration as an ordering heuristic. Actual
conflicts use exact membership-set intersection. Variant order and the span
between a property's first and last variants do not define conflicts.

For each group in a pass:

1. Collect the byte ranges of already-placed groups that occur with it in any
   variant. Merge overlapping blocked ranges and derive the free gaps.
2. Find gaps that fit the group starting at the gap's first offset aligned to
   this group's alignment.
3. Choose the smallest suitable gap by its full byte length before alignment;
   break ties by the lowest aligned placement offset. This is XLA's best-fit
   rule, adapted from heap-wide alignment to per-group alignment.
4. Treat the space after the last blocked range as an open-ended tail. Use its
   aligned start when no finite gap fits. That position may reuse existing
   storage occupied only by nonconflicting groups.
5. Commit one fixed offset for the group and update the maximum occupied end.

Compare the candidates using the final value-region size with the padding
required by the selected representation. Keep the smaller candidate; ties retain
the size-first pass. Every occurrence of a property uses its winning offset.

The upstream functions guiding the port are `GetSpatialBufferIntervalCompare`,
`GetTemporalBufferIntervalCompare`, `MakeFreeChunksList`,
`FindUnslicedChunkCandidates`, and `ChooseBestHeapAlgorithm::Finish`. The source
generator implementation remains to be written. Discovery and the helper
representation are selected below; catalog validation and fallback details
remain open.

### Type-size and alignment discovery

For now, establish layout information from these sources only:

| Type | Source of layout information |
|---|---|
| Primitive | Known managed size and supported target alignment rules. Architecture-dependent sizes remain unknown without sufficient target information. |
| Enum | Its underlying integer type. |
| Recognized Unity struct | The approved initial target list, with sizes and alignments validated for the supported profiles and checks for actual type identity and expected layout. |
| Ordinary single-field struct | Recursively inherit its sole instance field's known size and alignment under the eligibility rules below. |
| Other user struct | Unknown, including multi-field structs with layout attributes. |

Size means managed storage size, including padding. Size and alignment can be
unknown independently; byte-range packing requires both and an unmanaged type.
The exact primitive policy, catalog size/alignment data, and target-alignment
profiles remain to be finalized. Do not infer target pointer size from the
generator host.

The [Unity source catalog survey](../reviews/2026-10-20-unity-layout-catalog.md)
inventories layout attributes at the exact 6000.5.6f1 source tag and measures 23
common Unity candidates plus primitive types in the installed bundled Mono CLI.
It also identifies Unity's SizeOf/AlignOf APIs for target validation. These are
preliminary measurements; the approved membership is recorded below.

#### Official initial Unity target

Approved on 2026-10-21: [664 types](unity-initial-layout-targets.csv), comprising
373 engine entries and 291 optional-package additions. This CSV is the
canonical membership list for the initial catalog. It records full type names,
assembly names, physical instance-field counts and source locations, plus package
versions and declaration conditions for optional-package entries. Engine assembly
names were observed in compiled metadata; package assembly names come from their
source definitions and still require compiled validation.
Empty, single-field, generic, non-public, and ref-like types are excluded.

The baseline is Unity **6000.5.6f1**, UnityCsReference revision
`0e24f535c49ef35981d84b56f30858457d9fee79`. Source field inventories were checked
against the installed Editor assemblies. The [marked-entry review](../reviews/2026-10-21-unity-selected-struct-catalog.md)
and [40-addition review](../reviews/2026-10-21-unity-unmarked-struct-catalog.md)
retain the selection rationale and exclusions.

This approval fixes the initial implementation target; it does not establish
size/alignment constants or support across Unity versions and player targets.
The CSV's assembly column records the observed baseline identity. In particular,
the 66 `Unity.Mathematics` entries were found in `UnityEngine.MathematicsModule`;
package-provided `Unity.Mathematics.dll` identities require separate validation.
Only validated entries may supply known layout information for a supported
profile. Unvalidated layouts remain unknown under the storage rules above.

The [optional-package survey](../reviews/2026-10-21-unity-optional-package-catalog.md)
extends discovery to the Editor's package catalog, including DOTS. All 291
suggestions from the [per-type review](../reviews/2026-10-21-unity-optional-package-selection.md)
are accepted. The [373-entry engine-only snapshot](unity-initial-layout-targets.engine-only.csv)
preserves the earlier selection for audit and reproduction.

#### Approved catalog measurement workflow

A separate maintenance script reads the approved target list, prepares a Unity
probe with the required pinned packages, and generates concrete measurement calls
for available types. Each call records `UnsafeUtility.SizeOf<T>()` and
`UnsafeUtility.AlignOf<T>()`. The measurement tooling runs outside ordinary
source-generator execution.

Preserve results as JSON with Unity/package versions, backend, architecture,
actual assembly/type identity and a field/layout signature. Generate the sorted
`UnityTypeLayoutCatalog.generated.cs` lookup table from those results. The table
contains names and numeric constants, so the analyzer has no Unity dependency.

Editor measurements establish an Editor profile. Run the same probe in Mono and
IL2CPP players for supported architectures to establish player profiles. Matching
results may share catalog entries. Missing types, changed signatures and
unmatched profiles keep unknown layout information; do not infer a player layout
from the generator host or an Editor-only measurement.

This workflow is approved. The initial supported target matrix, how the generator
identifies the applicable profile, and the measurements themselves remain pending.

#### Single-field wrappers

The single-field shortcut applies to ordinary sequential wrappers, including
C#'s default sequential layout, with default packing and no explicit size.
Count physical instance fields, including compiler-generated backing fields;
ignore static and constant fields. The field must itself have supported,
unmanaged layout. Nested eligible wrappers can recurse to a supported leaf;
an unresolved type parameter remains unknown.

Do not apply the shortcut to `LayoutKind.Auto`, explicit layout, nondefault
`Pack` or `Size`, fixed buffers, or inline arrays. Leave these shapes unknown
instead of calculating their custom layouts. A single declared field does not
by itself prove the wrapper has that field's size and alignment:

```cs
[StructLayout(LayoutKind.Sequential, Size = 16)]
struct Padded { public int value; } // Size 16, rather than 4.

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct Packed { public long value; } // Can change alignment without changing size.
```

A focused probe on CoreCLR 10.0.12 and Unity 6000.5.6f1's bundled Mono confirmed
ordinary and nested wrapper sizes, the padded counterexample, a shifted explicit
field, and the packing distinction: a normal long wrapper embedded after a byte
starts at offset 8, while the packed wrapper starts at offset 1. This probe used
managed `sizeof` and field addresses. It did not run in an IL2CPP player; targeted
Mono and IL2CPP validation remains part of implementing the wrapper shortcut.

Recursion supplies layout metadata only. A wrapper and its underlying field
retain distinct type identities for Additive typed-slot sharing. Unsupported
types stay outside the known-layout byte-range region. Unknown-size unmanaged
fields use the offset-zero helpers below when their types are concrete.
Reference-containing structs and unresolved generic value fields follow the
storage rules below.

### Known-size and unknown-size unmanaged storage

Partition unmanaged payload storage into two independent regions:

- All eligible known-size fields go into one explicit-layout value-data helper.
  The selected XLA heuristics assign aligned byte offsets within that helper,
  including permitted partial overlap. Known-size fields do not enter the
  unknown-size slot helpers.
- Concrete unmanaged structs with unknown size use multiple nongeneric
  explicit-layout slot helpers. Every field within a slot starts at
  `[FieldOffset(0)]`; omit the helper's `Size` setting. The runtime computes each
  slot helper's extent, alignment, and padding.

The ordinary outer union contains the single known-size helper, the additional
unknown-size helpers, and its other fields. Outer field placement is assigned
by the runtime. Eligibility for byte packing still requires known alignment;
target-dependent alignment handling remains an open detail.

For example, `A(Foo foo1, Foo foo2, float duration)`, `B(Baz baz, Bar bar)`, and
`C(int count)` can use two unknown-size helpers:

- Slot 1 overlays Foo for `foo1` with Baz for `baz`.
- Slot 2 overlays Foo for `foo2` with Bar for `bar`.
- The known-layout helper overlays float `duration` with int `count`.

The number of helpers must follow valid slot assignment under the existing
stable-location constraint. The maximum unknown-field count in any variant is
a lower bound, not always sufficient. For example,
`A(Foo x, Foo y)`, `B(Foo y, Foo z)`, `C(Foo z, Foo x)` requires three stable
slots although each variant uses only two fields. Each shared-property group
must keep one slot; groups that coexist in a variant cannot share that slot.
Only unknown-size groups contribute to this slot count and assignment. Known-size
groups are handled independently by the single byte-packed helper.

This avoids general size calculations, value boxing, and storage-selection
branches in getters. Unknown sizes do prevent ranking alternative assignments
by their true byte footprint.

Reuse the placement machinery with logical extent 1 and alignment 1 for each
unknown-size shared-property group, in a separate allocation from the known-size
byte region. Logical placement offset selects the helper index; it does not
become a byte offset within that helper. Do not replace the type's unknown size
metadata with a fictitious one-byte size.

Both selected orderings collapse to variant-count descending and stable group
index ascending when all extents equal 1, so run only one pass. Keep the concrete
type identity even though the logical extent is 1. For each property group:

1. Find existing helpers already containing that same concrete type and having
   no variant-membership conflict with this group. Check against all groups in
   each helper, including groups of other types.
2. If one or more are compatible, reuse the same-type field in the helper with
   the lowest stable index. Update its occupied variant set; every occurrence
   of the property retains that location.
3. Otherwise use the unit-slot best-fit placement rule to overlay another type
   or create a helper. Apply the same coexistence checks in either case.

This preference avoids enlarging another helper with a type already represented
in a compatible slot. The assignment remains greedy; it does not guarantee the
minimum number of helpers or the smallest runtime byte footprint.

For `A(Big big1, Small small1)` and `B(Small small2, Big big2)`, reuse the Big
slot for both Big properties and the Small slot for both Small properties.
The earlier [bundled Mono CLI layout probe](../reviews/2026-10-21-additive-overlap-tradeoffs.md)
compared an assignment lacking this optimization with typed storage and measured
264 versus 144 bytes. That example exposes the omitted reuse preference; it is
not an inherent Additive advantage or evidence for retaining a separate mode.

All overlapped types must be unmanaged. A helper that needs an unresolved type
parameter would itself become generic and cannot use explicit layout; those
value fields use ordinary typed slots. Nongeneric helpers can be embedded in an
otherwise generic union when their field types do not depend on its type
parameters. `ValueStorage.Auto` uses this mixed representation in generic unions:
eligible known-size fields enter the packed helper, eligible concrete unknown-size
fields enter offset-zero helpers, and generic-dependent value fields retain typed
slots. Additive remains the policy without explicit offsets.

The [targeted Unity probe](../reviews/2026-10-20-unity-unsized-overlap.md) passed
821 checks in each Mono and IL2CPP release Linux x64 player. It confirms automatic
helper sizing and padding, direct property reads, nongeneric helpers embedded
in a generic outer type, and a closed generic unmanaged field inside a
nongeneric helper. This validates the selected shape, not generator integration.

### Reference-containing structs and generic-dependent values

`ReferenceStorage` controls structs known to contain managed references,
independently of `ValueStorage`:

| Reference policy | Storage for `struct Payload { string text; int count; }` |
|---|---|
| Typed | Ordinary `Payload` field; reuse with the same type across mutually exclusive variants. |
| Shared | Box the payload into a shared `object?` slot; unbox to `Payload` when reading. |
| Auto | Select Shared, including boxing, when its total inline union size is at most half the Typed alternative. If either size is unknown, apply the same threshold to total generated instance-field counts. Otherwise use Typed. |

Shared therefore permits boxing allocations for reference-containing struct
payloads. Unmanaged values keep their value-storage policy; Shared does not
redirect them to boxed object storage. Every property still has one fixed slot,
and fields that coexist in a variant cannot share it.

Generic-dependent value fields such as `T value` with `where T : unmanaged`, or
an unmanaged `Wrapper<T>`, use ordinary typed fields in the outer union. This
preserves their types without boxing or offsets. Reuse typed storage across
variants when types match and groups do not coexist. The runtime determines the
field's size and alignment when the generic type is instantiated.

A generic union may therefore mix typed generic fields with nongeneric
overlapping helpers for eligible concrete fields. A struct already known to
contain references follows `ReferenceStorage` even when it has generic type
arguments; the typed generic fallback applies when the value's classification
or layout depends on unresolved parameters. Known reference types continue to
follow the reference policy normally.

#### Automatic reference storage

Resolve value storage before comparing the complete Typed and Shared inline
union representations. Include the value storage, discriminator, reference and
typed payload slots, common bookkeeping fields, and padding in both sizes.
When both sizes are known, choose Shared when its inline size is at most half
the Typed size; otherwise choose Typed. The threshold includes equality.

The comparison excludes separately allocated boxes, object headers, and any
allocation or GC cost. Auto may therefore introduce boxing for
reference-containing structs when Shared meets the selected threshold.
This rule optimizes inline footprint without predicting payload lifetimes,
construction frequency, or union copy counts. Users can override it with
explicit Typed or Shared storage after profiling.

If either total inline size is unknown, compare the number of generated instance
fields in each outer union after storage reuse and packing. Select Shared when
its count is at most half the Typed count, including equality; otherwise select
Typed. Include the discriminator, known-size helper, each unknown-size helper,
ordinary typed slots, reference slots, and any common bookkeeping fields that
are emitted. Each outer field counts once, regardless of its type's size. Do not
recursively count fields within helpers or payload structs, separately allocated
boxes, public properties, or static fields.

For example, two unchanged fields plus six Typed slots give a total of eight.
If Shared needs two slots, its total is four and Auto selects Shared. With ten
unchanged fields, the same slot reduction gives totals of sixteen and twelve,
so Auto selects Typed. Counts describe the complete generated union, not only
the reference-storage region.

This fallback approximates footprint without inventing byte sizes for unknown
types. Fields can have very different sizes, so halving their count does not
guarantee halving the inline byte size. Always use the byte-size comparison when
both total sizes are known. The exact byte-size calculation remains open.

### Proposed policies

These collect settled policy behavior and remaining proposals; enum declarations
and API defaults are not finalized.

| Enum | Member | Proposed behavior |
|---|---|---|
| `ValueStorage` | `Auto` | Use nongeneric overlapping helpers for eligible concrete unmanaged fields in both nongeneric and generic unions; generic-dependent value fields use ordinary typed slots. |
| `ValueStorage` | `Additive` | Unmanaged values use typed slots shared by type across variants, without unsafe code, offsets or boxing. Reference-containing structs follow `ReferenceStorage`. |
| `ValueStorage` | `OverlapUsingOffsets` | Eligible unmanaged fields use nongeneric explicit-layout helpers; generic-dependent value fields use ordinary typed slots. |
| `ReferenceStorage` | `Auto` | Select Shared, including boxing, when its total inline union size is at most half the Typed alternative. If either size is unknown, compare total generated instance-field counts using the same threshold. Otherwise use Typed. Exclude separately allocated boxes. Exact byte-size calculation remains open. |
| `ReferenceStorage` | `Typed` | Use typed slots for reference types and reference-containing structs; share by type across variants, ignoring reference-nullability annotations. |
| `ReferenceStorage` | `Shared` | Share `object?` slots across reference payloads; box reference-containing structs into those slots. |

Reference sharing must consider simultaneous occupancy, not just the number of
different reference types. Four mutually exclusive reference fields can use one
shared slot; four references used together still require four slots.

### Field offsets: discussion in progress

The current `Layout.Overlap` puts explicit layout directly on the union struct.
Every instance field then requires an offset, including reference storage.

The redesign selects separate, nongeneric explicit-layout value helpers embedded
in a union that uses ordinary field placement: one helper for XLA-packed known-size
fields and additional offset-zero helpers for unknown-size unmanaged structs.
The current generator does not emit this arrangement yet.

Established constraints and observations:

- A helper nested inside a generic C# type is itself generic in CLR metadata,
  even if it does not mention `T`. It cannot bypass the generic explicit-layout
  restriction.
- A genuinely nongeneric helper declared outside all generic containing types
  can hold fixed-type overlapping fields inside an otherwise generic union.
  Fields depending on `T` would remain outside that helper.
- The standalone-helper arrangement succeeded on CoreCLR and Unity's bundled
  Mono; the nested generic arrangement threw `TypeLoadException`. The later
  unknown-size helper probe also validated concrete nongeneric helpers inside
  a generic outer struct in an IL2CPP player.
- Unity 6000.5's `Vector2` has sequential layout with two floats: offsets 0 and 4,
  total size 8. The installed assembly also had managed-array stride 8 on both
  tested runtimes. The current generator does not calculate composite sizes such
  as `Vector2` yet.

Still to decide:

- The primitive layout policy, size/alignment data for the approved Unity
  targets, supported target-alignment profiles, and the handling of
  architecture-dependent fields. General layout calculation for other composite
  types is outside the current scope.

The stable-location decision follows the [discriminator-check investigation](../reviews/2026-10-20-unity-kind-dispatch.md)
and [larger IL2CPP probes](../reviews/2026-10-20-unity-kind-dispatch-complexity.md).
Mono retained repeated getter selection work. IL2CPP eliminated it in several
examples, but larger ordinary getters and field-first patterns retained it.
Storage selection must therefore avoid requiring these optimizations.

### Other open details

- Final enum member names, defaults, and attribute parameters/properties.
- How to calculate the complete inline sizes for `ReferenceStorage.Auto` under
  the selected target profile. The comparison includes the selected value
  representation, discriminator, reference and typed payload slots, common
  bookkeeping fields, and padding; separately allocated boxes are excluded.
- Whether Shared retains concrete slot types when a particular slot only ever
  holds one reference type; this hybrid refinement has not been agreed.
- Migration of the old `Layout` API, including the removed boxed and
  UnsafeOverlap strategies, and the existing global configuration properties.
  Decide how project defaults differ from automatic selection.
- Implementation order and the regression/benchmark coverage for the settled
  policies.

### Evidence

- [Unity cast versus `as` comparison](../reviews/2026-10-20-unity-reference-casts.md)
- [Unity typed storage versus casts](../reviews/2026-10-20-unity-typed-reference-storage.md)
- [Repeated discriminator checks in property patterns](../reviews/2026-10-20-unity-kind-dispatch.md)
- [Larger IL2CPP discriminator-check probes](../reviews/2026-10-20-unity-kind-dispatch-complexity.md)
- [Mono and IL2CPP discriminator-selection timings](../reviews/2026-10-20-unity-kind-dispatch-cost.md)
- [Unknown-size unmanaged overlap in Mono and IL2CPP](../reviews/2026-10-20-unity-unsized-overlap.md)
- [Unity source layout inventory and catalog candidates](../reviews/2026-10-20-unity-layout-catalog.md)
- [XLA packing heuristics, pinned upstream reference](https://github.com/openxla/xla/blob/fe53c79cbd247bb11fa8b05d32f640be8b2748be/xla/service/heap_simulator/heap_simulator.cc)
- [Historical CoreCLR layout benchmarks](../benchmark-results/RESULTS_default.md)
- [.NET nested types and generics](https://learn.microsoft.com/en-us/dotnet/standard/generics/#nested-types-and-generics)
- [Unity 6000.5 Vector2 declaration](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.5/Runtime/Export/Math/Vector2.cs)
- [StructLayout size overrides](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.structlayoutattribute.size)
- [StructLayout packing and alignment](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.structlayoutattribute.pack)
