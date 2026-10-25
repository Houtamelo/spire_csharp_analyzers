# Private payload coverage

| Case | Contract |
|---|---|
| `PayloadFieldsArePrivateAndGettersArePublic` | Payload fields use private `_`-prefixed storage, while original names are public getter-only properties. |
| `PayloadTypesAreNonPartialIncludingFormerAliasesAndNestedTypes` | Canonical, alias, and nested payload structs are not declared `partial`. |
| `ConstructorAndDeconstructUsePrivateBackingStorage` | Constructors and typed `Deconstruct` preserve values through private fields. |
| `PayloadMutationIsRejectedByTheGeneratedReadOnlyProperties` | Generated payload properties cannot be assigned by consumers. |
| `GenericAndNestedPayloadsExposePrivateBackingFields` | Generic and nested payloads preserve private storage and getter-only access. |
| `UnityPayloadBackingFieldsCarrySerializationAttributes` | Unity serialization attributes attach to the private backing fields, including managed references. |
