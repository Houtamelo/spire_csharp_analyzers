# RFC: Record-struct variants for struct unions

Status: implemented and validated on 2026-11-13. This RFC records the
decisions from the design discussion, including the alternatives that were
subsequently rejected. Diagnostic IDs/messages and the exact deterministic
cross-file ordering rule are left to the implementor within the constraints below.

Allow struct discriminated unions to mix existing method-declared variants with
user-declared record-struct variants. Both forms retain the current
field-based union storage, payload reconstruction, and equality behavior.

The authored record replaces the generated public `Types.Variants.Variant`
payload only. Retain the existing internal serialization payload wrappers and
serialization workflow. No separate serialization mirror representation has
been selected; preserve the existing wrapper design.

## Declaration forms

The agreed API allows exactly two forms for `[Variant]` declarations on a struct union:

| Topic | Method declaration | Record-struct declaration |
|---|---|---|
| Syntax | `[Variant] public static partial Union Variant(fields..);` | `[Variant] public record struct Variant(fields..);` |
| `partial` | Required on the method | Optional on the record struct |
| Factory implementation | Generate the declared partial method's implementation | Generate no variant factory |
| Canonical payload type | Generate `{UnionName}Types.Variants.Variant` | Use the declared `Union.Variant` |
| Duplicate generated payload | Existing generated payload is retained | Generate no payload counterpart under `Types.Variants` |
| Construction | `Union.Variant(fields..)` | `new Union.Variant(fields..)` |
| Union storage | Extract and store variant field values | Extract and store variant field values |
| Payload access | Reconstruct the generated struct | Reconstruct the declared record struct |

The actual generated container naming for generic and nested unions retains the
[existing conventions](../discriminated-unions.md#struct-unions).

`[Variant]` on an ordinary `class`, ordinary `struct`, or record class is rejected.
Both `record Variant(...)` and explicit `record class Variant(...)` are forbidden
as annotated variants in this API. Unannotated nested types do not become variants.

Record-struct variants must have public declared accessibility and declare no
type parameters of their own. Non-public and independently generic record
variants produce errors. Using the containing union's type parameters remains
subject to the existing union eligibility rules. Existing accessibility rules for
method-declared variants are unchanged. The implementor selects diagnostic IDs
and messages following the repository's diagnostic conventions.

An absent positional parameter list is allowed and defines a fieldless variant:

```csharp
[Variant] public record struct None;
```

An empty positional parameter list is also fieldless. Only positional parameters
define union fields; any additional authored instance members do not enter the
union schema automatically.

These restrictions concern variant declarations within struct unions. Existing
record-union inheritance and discovery are outside this change. Existing union
eligibility and storage restrictions continue to apply. Reference-type fields
inside an allowed variant remain supported under the existing field rules.

### Mixed example

```csharp
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Customization
{
    [Variant]
    public record struct Slider(SliderKind kind, int value);

    [Variant]
    public static partial Customization Color(ColorKind kind, Rgb value);
}
```

Construction at a call site:

```csharp
Customization slider = new Customization.Slider(sliderKind, 42);
Customization color = Customization.Color(colorKind, rgb);
Customization colorFromPayload =
    new CustomizationTypes.Variants.Color(colorKind, rgb);
```

`SliderKind`, `ColorKind`, and `Rgb` are application types in this example.

Generate a `Color` factory implementation and
`CustomizationTypes.Variants.Color`. Use the authored `Customization.Slider`
directly as Slider's canonical payload type. Generate neither a `Slider` factory
nor a `CustomizationTypes.Variants.Slider` type. Other sibling container content,
including runtime storage and any required serialization support, remains
available as needed.

The nested `Slider` type also occupies the name that a same-named union factory
would otherwise use. The constructor is the record variant's construction API.

Public canonical payloads implicitly convert into the union. For a public
record-struct variant, this conversion reads its positional member values and
initializes the union's storage. Existing accessibility policy for method-based
conversions is preserved. Non-public record variants are rejected.

## Storage and payload access

The record struct's positional parameters define its variant field schema:
names, types, and order. Method parameters continue to define the schema for
method-declared variants. Additional record members are not automatically added
to the schema.

Preserve the existing conversion and access behavior for both forms:

1. Conversion reads the payload's variant field values into union storage.
2. The union stores its tag and logical field values using the existing layout
   and `ReferenceStorage` rules.
3. The guarded variant accessor reconstructs a payload from those stored values.
   For Slider, it returns `new Customization.Slider(storedKind, storedValue)`.

The declared record struct is the public payload type, rather than an instance
that the union retains intact. Extra record instance state outside the positional
schema is not retained through conversion. Copying field values remains shallow:
reference-valued fields retain their references under the existing storage rules.

Authored replacements of compiler-synthesized positional data members are
allowed. Report a descriptive warning explaining that union conversion extracts
positional member values and payload access reconstructs the record; custom
initialization or getter behavior can therefore change the observed values on
reconstruction. For example, a positional `value` property initialized as
`value + 1` exposes 11 after `new Counter(10)`, then 12 after reconstruction from
the stored 11. The implementor chooses the warning ID and wording.

Ordinary helper methods and additional computed properties remain allowed and do
not trigger this warning merely by being present. They do not add union fields.
Properties emitted by Spire for serialization do not count as authored
replacements and must not trigger the reconstruction warning.

Record-struct accessors expose the authored record type, and method accessors
expose their generated payload type. Preserve the existing `kind`, `IsVariant`,
accessor naming, kind guards, and pattern-matching behavior. Positional record
structs with fields supply their compiler-generated `Deconstruct`; method
payloads retain the generator-provided `Deconstruct`. A fieldless record variant
needs only its kind predicate for matching and does not require `Deconstruct`.
No union-level deconstruction API is added.

## JSON names

Retain the existing `[JsonName]` placements for union serialization:

- On the variant method or record-struct type: override the variant's JSON name.
- On a method parameter or record positional parameter: override that field's
  JSON name.

```csharp
[Variant, JsonName("slider")]
public record struct Slider(
    [JsonName("slider_kind")] SliderKind kind,
    [JsonName("slider_value")] int value);
```

No property or backing-field targets are added to `[JsonName]`. Record annotations
do not introduce additional precedence rules between parameter, property, and
field targets. Existing `[JsonName]` support on the union type is unchanged.

## Unity managed-reference annotations

Record-struct variants support both managed-reference annotation forms:

```csharp
[Variant]
public record struct Node(
    [UnitySerializeReference] NodeBase? value);

[Variant]
public record struct OtherNode(
    [field: UnityEngine.SerializeReference] NodeBase? value);
```

Recognize Spire's `[UnitySerializeReference]` on a positional parameter or Unity's
`[SerializeReference]` on its corresponding backing field. Either marks the same
logical variant field for managed-reference serialization. If both are present,
treat them as one request; no precedence rule is needed.

Use that flag when emitting the corresponding serialized field and any
former-name serialization support. Runtime union storage and equality are
unchanged. The annotations affect Unity serialization only, under its existing
eligibility rules. Additional non-positional fields do not become union fields
because they carry `[SerializeReference]`.

Method-declared variants retain their existing parameter marker. Spire's marker
stays parameter-only; Unity's attribute retains its existing field target.

## Former variant names

Record-struct variants retain the existing `VariantAttribute.FormerlySerializedAs`
syntax and semantics:

```csharp
[Variant(FormerlySerializedAs = "OldSlider")]
public record struct Slider(SliderKind kind, int value);
```

Support one previous variant name for restoring Unity serialization data. It
adds no runtime variant, factory, implicit conversion, or typed equality
overload. Existing former-name validation and serialization-only compatibility
behavior continue to apply within the existing serialization workflow.

## Serialization authoring

The existing internal per-variant serialization class derives from
`SpireSerializedPayload<CanonicalPayload>`. That shared wrapper declares
`[SerializeField] public T value`, so the public variant payload also supplies
the value serialized inside the internal wrapper.

For method variants the generator annotates the public payload with
`[Serializable]` and its backing fields with `[SerializeField]` or
`[SerializeReference]` when Unity serialization is enabled. Replacing that public
payload with an authored record preserves those wrappers. Handle the authored
record's serialization attributes through the two cases below. Apply Unity field
generation and warnings when the existing Unity serialization mode is enabled;
ordinary non-serialized unions gain no Unity serialization requirements.

### Partial record variants

For a partial record variant, emit a partial declaration that supplies
`[Serializable]` if it is absent. For each positional member that the user has not
explicitly declared, generate the corresponding auto-property, initialize it from
the positional parameter, and annotate its backing field for serialization:

```csharp
// User declaration
public partial record struct Foo(int bar);

// Generated declaration
[global::System.Serializable]
public partial record struct Foo
{
    [field: global::UnityEngine.SerializeField]
    public int bar { get; set; } = bar;
}
```

The user verified this partial-record technique during the design discussion.
Generating the property suppresses the compiler's corresponding synthesized
property while preserving its positional value and public API.

Use `[field: SerializeReference]` instead when either approved managed-reference
annotation requests it. Preserve relevant existing property/backing-field
annotations when materializing a previously synthesized member. Avoid duplicate
`[Serializable]` and serialization-field annotations.

If the user already declared a positional property, keep that property. Check
its backing field for `[SerializeField]` or `[SerializeReference]`. If it lacks
serialization attributes, emit a descriptive warning and offer a code fix to add
the appropriate backing-field annotation to the authored property. An existing
managed-reference annotation counts; the fix must not blindly add `[SerializeField]`
to a managed-reference field. Preserve the property's initializer and body.

This serialization warning is separate from the authored-member reconstruction
warning. A custom positional property can require either or both warnings.

### Non-partial record variants

Non-partial variants remain supported. Check their existing type and positional
backing-field annotations. Warn only when necessary serialization attributes are
missing; a fully annotated record needs no missing-attributes warning.

The diagnostic should explain the missing requirements and the available
solutions. Suggested content, with exact wording and diagnostic IDs left to the
implementor:

> Variant `{VariantName}` needs serialization attributes. For union `{UnionName}`
> to be properly serializable, all variants must be serializable too.
>
> Mark the record struct with `[Serializable]` and its positional data members
> with `[field: SerializeField]`, or `[field: SerializeReference]` where appropriate.
>
> Making `{VariantName}` partial lets Spire generate the necessary attributes for
> positional members you have not explicitly declared.
>
> If the union was not intended to be serializable, remove `[Serializable]` from it.

Provide these code fixes as applicable:

- Add `[Serializable]` to the authored record if it is missing.
- Add missing serialization annotations for all positional properties. For
  synthesized properties, add the field-targeted attribute to the positional
  parameter. For explicitly declared auto-properties, annotate the property.
  Preserve existing annotations and use `[SerializeReference]` when requested.
- Mark the record struct `partial` so the generator can supply the type attribute
  and any positional properties the user has not explicitly declared. Authored
  properties still follow the warning/fix rules in the partial case.

Only positional data members are required for the union schema; helper and extra
computed properties do not need serialization annotations for this feature.
Fieldless variants require the type attribute and no positional field annotations.
Code fixes must preserve authored code and only offer field-targeted annotations
where a serializable backing field exists or is synthesized.

## Equality contract

Preserve the [implemented struct-union equality contract](../discriminated-unions.md#equality).
For ordinary non-ref struct unions, the canonical payload type for each variant
determines the typed equality signature:

- `Union : IEquatable<Union>` and `Union : IEquatable<Payload>` for each canonical
  variant, including canonical method variants with non-public factories.
- Public readonly typed `Equals` overloads, plus readonly overrides of
  `Equals(object?)` and `GetHashCode()`.
- `==` and `!=` for `(Union, Union)`, `(Union, Payload)`, and `(Payload, Union)`,
  declared on the union.
- Equality compares the variant tag and active logical fields using
  `EqualityComparer<TField>.Default`. Hashing uses the same tag and fields.
- Serialization payloads/caches, inactive storage, and extra union instance fields
  stay excluded. Extra record state outside its positional schema is also excluded
  from union equality and hashing.
- `Equals(object?)` accepts only the same union type. Boxed payloads are not
  accepted through that overload.
- Serialization-only former-name payload aliases gain no typed equality surface.
- No nested `Hasher`, reverse `Payload : IEquatable<Union>`, or payload-side union
  equality members/operators are generated.

For a record-struct variant, union equality compares its positional member values;
it does not delegate to the record struct's own `Equals` or `GetHashCode`. The
record's own compiler-generated or authored equality remains its own behavior.
Existing record-union equality and ref-struct union APIs remain unchanged.

Rejecting `union == default` and `union != default` remains a separate
[deferred analyzer change](../../plans/2026-11-13-reject-default-union-comparisons.md).
That plan covers either operand order and typed union defaults. Current SPIRE003
still exempts equality comparisons. This RFC adds no default-comparison rule and
does not treat that future rejection as already implemented. Untyped `default`
can remain ambiguous with multiple payload overloads.

## Compatibility and implementation scope

Method declarations and their generated API retain their current behavior.
Record structs require C# 10; method-only consumer code retains its existing
C# 9 compatibility. The new syntax does not raise the language requirement for
consumers that keep using methods.

The implementation needs to distinguish each variant's declaration form,
canonical payload type, field schema, and construction path. Update discovery
and `[Variant]` targeting to recognize the allowed record structs, then use those
distinctions throughout emission and analysis:

- Emit partial factories and generated payload types only for method variants.
- Build conversions and reconstructed accessors for either canonical type without
  depending on a record variant having a same-named factory.
- Resolve typed equality, payload references, field guards, exhaustiveness,
  deconstruction patterns, and missing-arm fixes for either declaration form.
- Adapt JSON and Unity serialization paths to the same field schema and
  reconstruction model. Add the partial-record serialization declarations,
  missing-attribute warnings, and their source-editing code fixes described above.

No new storage mode, whole-payload retention mode, deep cloning, or automatic
equality generation for authored payloads is introduced.

## Variant ordering

Discover method and record-struct variants as one sequence in declaration order.
Do not group methods before records or sort variants by name. Generated `Kind`
entries and other ordered variant output follow that unified sequence.

Replacing a method declaration with a record declaration at the same position
preserves its position in that sequence. Inserting or moving a variant can change
numeric `Kind` values, as with existing method variants. This RFC introduces no
explicit tag IDs or automatic numeric-tag migration.

Across partial declarations, use compilation syntax-tree order and source order
inside each part, preserving the existing method-only ordering behavior. Changing
the compiler's input order can change numeric kind values. Integration tests
cover both input orders.

## Accessor naming and collisions

Preserve camelCase accessor naming, including existing acronym handling. If the
accessor name matches a variant factory or a nested record-variant type, append
one `_value` suffix. Both declaration forms participate in that collision check.

Remaining collisions use ordinary C# compiler errors. Do not introduce further
automatic renaming, repeated suffixes, or separate collision rules for records.
Existing former-name validation and serialization behavior remain unchanged.

## Rejected alternatives

| Alternative | Decision |
|---|---|
| Allow record classes | Initially considered, then explicitly rejected; only record structs are allowed as type-declared variants |
| Allow ordinary classes or structs | Rejected to avoid a separate constructor/member contract and the associated policy surface |
| Allow non-public or independently generic record variants | Rejected with an error; record variants must be public and declare no type parameters of their own |
| Require a positional parameter list | Rejected; an absent or empty list defines a fieldless variant |
| Reject authored replacements of positional data members | Rejected; allow them and report a descriptive reconstruction warning |
| Store authored payloads intact | Rejected; preserve field extraction and reconstruction |
| Require authored record structs to be partial | Rejected; partial records receive automatic serialization support, and non-partial records use authored annotations with warnings/fixes when incomplete |
| Replace the existing serialization wrappers with a separate mirror representation | Rejected; preserve the wrappers and make their canonical record payload serializable |
| Generate a factory or duplicate payload for a record variant | Rejected; construction and payload identity use the authored nested type |
| Compare entire payloads or physical union storage for equality | Rejected; compare the tag and active logical fields |
| Generate a nested equality comparer/Hasher | Dropped in favor of the union's equality interfaces and overrides |
| Group methods before record variants | Rejected; both forms participate in one declaration-ordered sequence |
| Add further automatic name-collision handling | Rejected; retain camelCase accessors with one `_value` suffix, then ordinary compiler diagnostics |

## Acceptance coverage

Implementation must use real generator/compilation integration tests and runtime
behavioral tests, with snapshots where generated API shape matters:

- Method-only, record-struct-only, and mixed unions compile with the expected
  payload types, factories, and public implicit conversions. A record payload
  without `partial` compiles. No duplicate record payload or factory is emitted.
- Fieldless records with absent or empty positional parameter lists compile,
  convert, reconstruct, match by kind, and participate in equality. Non-public
  and independently generic record variants produce errors. Record variants in
  supported generic unions may use the containing union's type parameters.
- The exact mixed example above works, including payload members named `kind`.
  Unannotated nested types are ignored. Forbidden annotated declaration forms
  produce diagnostics, and existing invalid method declarations remain rejected.
- Interleaved method/record variants retain declaration order in `Kind` and other
  ordered output. Changing a variant's declaration form at the same position
  preserves its ordinal. Existing method-only order and the documented
  deterministic cross-partial ordering rule have integration coverage.
- CamelCase accessors check both factory and record-type names for collisions,
  including lowercase record names. Apply one `_value` suffix and leave any
  remaining collision to the compiler. Existing acronym handling is preserved.
- Conversion copies field values; subsequent payload mutation does not replace
  stored scalar values. Access reconstructs the expected payload. Additional
  non-positional state does not become union state or affect union equality.
- Valid authored replacements of positional data members compile with a
  descriptive warning, and behavioral tests demonstrate their reconstruction
  semantics. Ordinary helper methods and additional computed properties do not
  trigger that warning merely by being present.
- Both union/payload operand orders, typed and object equality, hashing, and
  collection use preserve the existing contract. Cover matching/different tags
  and fields, null/reference fields, and serialization/cache independence.
- Guards, exhaustive matching, positional patterns, and missing-arm fixes work
  with both canonical payload locations. Exercise supported generic, nested,
  readonly, and ref-union hosts within their existing restrictions.
- Supported JSON and Unity paths preserve their established behavior for both
  forms and mixed unions; invalid serialized declarations receive diagnostics.
  Runtime Unity claims require the corresponding Editor/player validation.
- Partial record variants receive the missing type attribute and generated
  positional properties with the proper backing-field attributes. Preserve
  positional construction, deconstruction, equality, existing annotations, and
  the existing internal wrappers. Generated properties do not trigger the
  authored-member warning.
- Authored positional properties lacking serialization annotations receive a
  warning and an applicable annotation fix without replacement or initializer
  changes. Already annotated properties, including managed-reference fields,
  do not receive that missing-attributes warning.
- Non-partial records cover missing type attributes, missing positional field
  annotations, both missing, and fully annotated controls. Verify all three code
  fixes through real before/after compilation and generator runs, including
  managed-reference fields, fieldless variants, and mixed authored/synthesized
  positional properties. Cover non-serialized unions as controls.
- `[JsonName]` on record types and positional parameters controls variant and
  field names respectively, matching method-variant behavior. The attribute's
  supported targets remain unchanged.
- Record positional fields support the parameter `[UnitySerializeReference]`
  marker, backing-field `[SerializeReference]`, or both together with identical
  serialization behavior. Cover nulls, derived types, and shared references under
  the existing Unity rules. Annotated non-positional fields stay outside the
  union schema. Existing method-variant annotations continue to work.
- Record variants accept one `FormerlySerializedAs` name, restore matching Unity
  data under the existing validation rules, and gain no extra runtime variant,
  factory, conversion, or equality overload for that old name.
- Method-only output compiles under the existing C# 9 profile; the new record
  syntax compiles under C# 10. The separate default-comparison policy is unchanged.

## Implementation validation

The implementation includes generation, equality, JSON and Unity serialization,
coupled analysis, warnings, and code fixes. Invalid record declarations report
SPIRE_DU019; missing serialization attributes report SPIRE028; authored positional
member replacements report SPIRE029.

- `dotnet test tests/Spire.CoreCLR.slnx --no-restore`: 2,988 tests passed,
  including all 1,480 source-generator tests. These cover C# 9 method-only
  compatibility, C# 10 records, generated API snapshots, runtime reconstruction,
  both JSON libraries, guard/exhaustiveness analysis, and actual code-fix edits
  followed by regeneration and compilation.
- The [native record-variant fixture](../../tests/UnityVariantPayloadCore/Assets/RecordVariantTests/README.md)
  passed 73 write checks and 67 fresh-process read checks in Linux Unity Editor
  6000.5.6f1. It validates real asset persistence, serialized property paths,
  managed references, and former-name migrations, including legacy method-field
  keys. This run does not extend player or other Unity-version validation.
- Changed compiled C# files were formatted; changed-file whitespace and local
  Markdown links were checked.
