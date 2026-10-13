# Using unions and utilities

Struct variants are static partial factories. Generated `kind` identifies the
active variant, and generated properties expose its fields. Guard field access
with the variant before reading it:

```cs
static string Describe(Result result) => result switch
{
    { kind: Result.Kind.Success, value: var value } => value.ToString(),
    { kind: Result.Kind.Failure, message: var message } => message,
};
```

Spire checks exhaustive matching and guarded field access. Use Rider's quick
fix to add missing arms. Property patterns with `var` preserve null values;
typed declaration patterns such as `string message` exclude null. Tuple-style
patterns can involve `ITuple` and boxing, so prefer property patterns when
allocations matter.

Compatible same-name fields share a property. Incompatible types get
`camelCaseVariantName_fieldName` properties; for example, Number's `value` and
Text's `value` become `number_value` and `text_value`. Pattern locals can remain
`number_value: var value`. Reference nullability differences alone do not make
fields incompatible. This naming behavior applies to struct unions.

| Layout | Storage and constraints |
|---|---|
| Additive | Typed fields; compatible same-name fields share slots. Supports generics. Default for Unity-serializable structs. |
| Overlap | Explicit offsets with compatible same-name fields sharing offsets. No Unsafe helper dependency. Cannot be generic or nested in a generic type. |
| BoxedFields | Object slots per resolved field name. Value fields box individually. Supports generics. |
| BoxedTuple | One object slot containing the active value or boxed tuple. Supports generics. |
| UnsafeOverlap | Raw unmanaged buffer plus separate managed/generic slots. Supports generics; requires unsafe code in the owning asmdef. |

Choose explicitly with `[DiscriminatedUnion(Layout.Additive)]`. `Auto` uses
Additive for Unity-serializable unions; other structs use Overlap when
non-generic and BoxedFields when generic. Unity does not import Spire's MSBuild
defaults from generated IDE project files: put Unity settings on the attribute.

Record unions use nested record variants and ordinary reference-pattern matching.
They do not use struct storage layouts or generated Unity struct serialization.
Generated JSON converters are separate from Unity asset serialization; request
`JsonLibrary.SystemTextJson` or `JsonLibrary.NewtonsoftJson` and provide the
corresponding library in your project.

Other APIs include `SpireLINQ.OfKind` for filtering, `SpireEnum<TEnum>` for checked
integer conversions, `[EnforceInitialization]`, and `[EnforceExhaustiveness]`.
`[InlinerStruct]` generates struct method forwarders; `[Inlinable]` generates
overloads that accept those structs. Their desktop JIT performance benefits are
not a measured Unity player performance guarantee.

The [rule reference](Reference/rules/SPIRE009.md) describes union exhaustiveness;
individual SPIRE rule pages are shipped beside it. DLL tooltips describe the
runtime public API. See [Serialization](Serialization.md) for Inspector and
migration limits.
