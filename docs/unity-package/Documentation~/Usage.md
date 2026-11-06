# Using unions and utilities

Struct variants are static partial factories. Generated `kind` identifies the
active variant; a variant accessor returns its public payload struct. Use the
canonical predicate and payload deconstruction pattern to match it:

```cs
static string Describe(Result result) => result switch
{
    { IsSuccess: true, success: var (value) } => value.ToString(),
    { IsFailure: true, failure: var (message) } => message,
};
```

Spire checks exhaustiveness and guarded accessor use. The IDE quick fix adds
missing arms in this style. Deconstruction with `var` preserves nulls; declaration patterns
such as `string message` exclude null. Payloads are non-partial structs with
private `_`-prefixed fields, public getter-only properties, constructors, and
typed `Deconstruct` methods. Construct changed values through the union factory.
Use `var (field)` for one payload field and `var (first, second)` for multiple
fields. Fieldless variants need only `IsVariant: true`. There is no union-level
deconstruction or flat field-property API.

Variant accessors also assert the expected kind. When the compiler host provides
Unity's assertion API, they use `UnityEngine.Debug.Assert` (`UNITY_ASSERTIONS`);
other hosts use `System.Diagnostics.Debug.Assert` (`DEBUG`). The check and
message are omitted when the corresponding symbol is absent. Nonserialized
Unity unions use a constant assertion message so their accessors can compile
with Burst.

Payloads live under a sibling types container (`ResultTypes.Variants.Success`).
The container matches the union's accessibility; `Variants` and its payloads are
public within it. Payloads copy the union's generic parameters and constraints,
while the containers remain nongeneric. Generic unions use an arity suffix
(`ResultTypes1.Variants.Success<T>` for `Result<T>`). Accessors use camelCase; an accessor matching any variant factory
gets one `_value` suffix. Other naming collisions use compiler diagnostics.

Concrete unmanaged fields overlap in per-variant runtime-laid-out helpers.
Generic-dependent fields remain typed outside the overlay. No unsafe code,
manual size catalog, or additional Unsafe DLL is required. A union itself can
be generic, but may not be nested in a generic containing type (SPIRE_DU018).

For Burst jobs and unmanaged ECS components, use only Burst-supported unmanaged
payload fields and omit `[Serializable]`. Unity serialization adds a managed
cache reference. If both forms are needed, manually declare a separate
serializable authoring union and convert it to the runtime union.
Prefer exhaustive switch statements in Burst code: a switch expression can
emit an unsupported boxed unmatched-value exception path even when Spire proves
that every declared variant is covered.

| Reference storage | Behavior |
|---|---|
| Auto | Shared when its slot count is at most half Typed's; otherwise Typed. |
| Typed | Reuse compatible typed slots across variants; direct access without boxing. |
| Shared | Reuse object slots; casts on reads, and boxing for structs containing managed references. |
| ReadGlobalCfg | Inherit the project default, falling back to Auto. |

Field names do not affect reuse; fields present together need separate slots.
Auto counts every managed candidate as one slot after same-type reuse. It
excludes unmanaged helpers, discriminator, generic-dependent fields, and cache.

Choose an explicit override with
`[DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]`. Unity does
not import Spire's NuGet MSBuild defaults from generated IDE project files.
Put Unity settings on the attribute.

Record unions retain nested record variants and ordinary reference-pattern
matching. They do not use generated Unity struct serialization. Generated JSON
converters retain the logical variant/field wire format; request
`JsonLibrary.SystemTextJson` or `JsonLibrary.NewtonsoftJson` and supply that
library. Caller options still determine how nested user types serialize.

Other APIs include `SpireLINQ.OfKind`, `SpireEnum<TEnum>`,
`[EnforceInitialization]`, and `[EnforceExhaustiveness]`. `[InlinerStruct]`
generates struct method forwarders; `[Inlinable]` generates overloads accepting
them. Their desktop JIT benefits are not a Unity player performance guarantee.

The [rule reference](Reference/rules/SPIRE009.md) describes union exhaustiveness;
individual SPIRE pages ship beside it. See [Serialization](Serialization.md)
for persistence and Inspector behavior.
