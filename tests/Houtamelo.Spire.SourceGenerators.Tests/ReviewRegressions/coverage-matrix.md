# Review regression coverage

| Area | Input | Contract |
| --- | --- | --- |
| Accessor ownership | Variant `A(int value)` alongside `B(int a)` | The generated `a` accessor belongs only to `A`; reading it under a `B` guard reports SPIRE013 while the `A` guard remains valid. |
| JSON literal escaping | Attribute discriminator, variant name, and field name containing quotes, backslashes, newlines, tabs, C0 controls, U+0085, U+2028, and U+2029 | System.Text.Json and Newtonsoft.Json generated converters and schema compile; both serializers preserve the names and round-trip the payload. |
| JSON global configuration | Same escaped discriminator inherited from `Spire_DU_DefaultJsonDiscriminator`, including U+0085/U+2028/U+2029 | Global configuration receives the same C# literal escaping as an explicit attribute and round-trips through both converters. |
| Record wildcard refactoring | Record DU switch expression with `_` arm and an existing local | Expansion emits valid record type/property patterns for every variant, preserves the wildcard body, avoids local collisions, and compiles through the generator. |

The code-refactoring provider currently exposes wildcard expansion for switch
expressions; statement-switch coverage remains with the existing AddMissingArms
tests until the provider exposes a statement refactoring entry point.
