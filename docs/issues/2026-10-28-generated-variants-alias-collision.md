# Generated `Variants` alias conflicts with another global union

Generated using aliases are emitted inside the union's named namespace, so they
can shadow unrelated global types such as `Variants`. Global-namespace unions
and collisions within the union's own namespace retain ordinary compiler errors.
These rare conflicts are left to the user by design.

The following global-namespace case still produces CS0576:

```csharp
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Shape
{
    [Variant] public static partial Shape Empty();
}

[DiscriminatedUnion]
public partial struct Variants
{
    [Variant] public static partial Variants Value(int value);
}
```

`Shape` generates `using Variants = global::ShapeTypes.Variants;`, which conflicts
with the global `Variants` type. Its payload accessor and conversion operator
reference that alias. Moving `Shape` into a named namespace avoids this conflict.

`GeneratedAliasEmitter.ReservedNames` keeps its existing checks for the current
union, generic parameters, containing types, and variants. The approved change
only moves aliases into a named namespace; it adds no compilation-wide collision
detection or alias renaming.
