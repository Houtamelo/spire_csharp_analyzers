# SPIRE_DU017: Invalid Unity variant serialization alias

An error prevents ambiguous or malformed `Variant.FormerlySerializedAs` values
from generating incompatible payload classes for a Unity-serializable union.

```cs
[Serializable]
[DiscriminatedUnion]
public partial struct Result
{
    [Variant(FormerlySerializedAs = "Ok")]
    public static partial Result Success(int value);
}
```

Use one previous variant name, written as the symbol name without an `@` escape.
Null, empty, and the variant's own current name are harmless. An alias cannot
identify another current variant, duplicate another alias, or collide with another
generated payload name. Malformed names are also rejected.

Aliases apply only to Unity serialization. Ordinary non-serializable unions do
not generate legacy payloads or report this diagnostic. Collisions with
user-declared generated members continue to use `SPIRE_DU015`.

Rename the alias or remove the conflicting declaration. Do not suppress this
error: generation cannot choose a safe interpretation of ambiguous saved data.
