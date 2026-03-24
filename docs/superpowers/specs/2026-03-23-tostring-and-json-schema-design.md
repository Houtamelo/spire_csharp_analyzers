# ToString and JSON Schema Generation for Discriminated Unions

## Overview

Two additions to the discriminated union source generator:

1. **ToString** — human-readable positional format for all union types
2. **JSON Schema** — `oneOf` + `discriminator` schema, tiered implementation based on available runtime

---

## Feature 1: ToString

### Format

```
VariantName(field1Value, field2Value)
```

- Fieldless variants: `VariantName()`
- Single field: `VariantName(5.5)`
- Multiple fields: `VariantName(3.0, hello)`
- Field values use their own `ToString()` — no quoting, no special formatting
- Field order: **original declaration order** from user source, not internal storage order (which may differ due to deduplication in Additive/Overlap strategies)
- Generic unions: no type name prefix — `Some(42)`, not `Option<int>.Some(42)`

### Strategy behavior

| Strategy | Implementation |
|----------|---------------|
| Struct (all 5) | `override string ToString()` — switch on `kind`, format each variant |
| Record | `override string ToString()` in a separate partial declaration per variant — overrides the default record `TypeName { Prop = val }` format |

### Always generated

Not opt-in. Every union gets `ToString()`.

### Default struct values

Not handled specially. `default(Shape)` is prevented by `[MustBeInit]` analyzers (SPIRE001, SPIRE003, SPIRE004). If a user bypasses the analyzers, `ToString()` prints the first variant with zero/null field values.

### Emitter file

`ToStringEmitter.cs` in `src/Spire.SourceGenerators/Emit/`.

---

## Feature 2: JSON Schema

### Exposure

```csharp
public static Lazy<string> JsonSchema { get; } = new(BuildJsonSchema);
```

Emitted on the union's base type (the partial struct or abstract partial record).

### Generation condition

Generated only when `Json != JsonLibrary.None` on the `[DiscriminatedUnion]` attribute.

### Schema format

Standard JSON Schema `oneOf` + `discriminator` envelope:

```json
{
  "oneOf": [
    {
      "type": "object",
      "properties": {
        "kind": { "const": "Circle" },
        "Radius": { "type": "number" }
      },
      "required": ["kind", "Radius"]
    },
    {
      "type": "object",
      "properties": {
        "kind": { "const": "Square" },
        "Side": { "type": "number" }
      },
      "required": ["kind", "Side"]
    }
  ],
  "discriminator": {
    "propertyName": "kind"
  }
}
```

### Tiered implementation

```csharp
public static Lazy<string> JsonSchema { get; } = new(BuildJsonSchema);

private static string BuildJsonSchema()
{
#if NET9_0_OR_GREATER
    // Use System.Text.Json.Schema.JsonSchemaExporter for accurate field type schemas
#else
    // Return compile-time generated string literal with basic type mapping
    return @"{ ... }";
#endif
}
```

**Tier 1 — STJ + .NET 9+ (`#if NET9_0_OR_GREATER`):**
- Uses `JsonSchemaExporter` at runtime for accurate field type schemas
- Per-variant: construct object schema with discriminator `const` + field schemas from `JsonSchemaExporter`
- Wrap in `oneOf` + `discriminator` envelope

**Tier 2 — STJ below .NET 9, or NSJ only (fallback):**
- Compile-time string literal emitted by the source generator
- Basic type mapping:
  - `int`, `long`, `short`, `byte`, `sbyte`, `ushort`, `uint`, `ulong` → `"integer"`
  - `float`, `double`, `decimal` → `"number"`
  - `string`, `char` → `"string"`
  - `bool` → `"boolean"`
  - `T[]`, `List<T>`, `IList<T>`, `IEnumerable<T>` → `"array"`
  - Everything else → `{}` (any)

**NSJ note:** `Newtonsoft.Json.Schema` is a separate commercial (AGPL) package — not used. NSJ-configured unions always use Tier 2 (compile-time schema).

### Property naming

- Field names respect `[JsonName]` overrides
- Discriminator values respect `[JsonName]` on variants
- Discriminator property name uses the configured `JsonDiscriminator` (default: `"kind"`)
- When both STJ and NSJ are enabled: use STJ path for schema generation

### Emitter file

`JsonSchemaEmitter.cs` in `src/Spire.SourceGenerators/Emit/`.

---

## Out of scope

- TypeScript type generation from unions
- Custom ToString format strings
- Schema generation for non-union types
- `Newtonsoft.Json.Schema` dependency (commercial license)
