# Readonly record variants need Unity serialization coverage

Spire supports `[Variant] public readonly record struct Value(int amount);`
for runtime union conversion and payload reconstruction. The current Unity
serialization implementation appears unable to persist its positional data.
This finding comes from source inspection and Unity's documented rules; a
native Unity round trip has not been run for this shape.

Affected declaration:

```cs
[Serializable, DiscriminatedUnion]
public partial struct Example
{
    [Variant] public readonly partial record struct Value(int amount);
}
```

The [record declaration emitter](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Emit/VariantPayloadEmitter.cs)
preserves `readonly` and generates positional properties with `get; init;`,
annotating their backing fields with `[SerializeField]` or
`[SerializeReference]`. Those backing fields are readonly. Unity's
[field serialization rules](https://docs.unity.com/en-us/engine/6000.5/manual/scripting/compilation-and-code-reload/script-serialization/rules)
exclude readonly fields; adding serialization attributes does not remove that
restriction.

The [serialized payload type reference](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Emit/GeneratedAliasEmitter.cs)
wraps the authored record itself in `SpireSerializedPayload<T>`, so the internal
wrapper does not provide separate mutable fields for the positional data.
Non-partial readonly record variants have the same underlying restriction even
when users supply the attributes themselves.

The existing [runtime regression](../../tests/Houtamelo.Spire.SourceGenerators.Tests/Behavioral/RecordVariantCoreTests.cs)
`ReadonlyRecordPayloadIsReconstructedFromStoredFields` covers conversion and
reconstruction. It does not establish Unity serialization support. The current
Unity record-variant tests do not cover a readonly variant.

Accepted mitigation: [SPIRE030](../rules/SPIRE030.md) warns for readonly variants
with positional data in Unity serialization mode and offers to remove
`readonly`. It replaces the missing-attributes warning for those variants;
fieldless variants remain exempt. Runtime generation and serialization storage
are unchanged. See the [implementation plan](../../plans/2026-11-15-readonly-record-variant-warning.md).

Any future serialization-storage redesign needs a saved-asset round trip in a
fresh Unity process, covering partial and non-partial readonly variants, value
and managed-reference fields, and fieldless variants. Analyzer/code-fix tests
do not establish native serialization support for readonly payloads.
