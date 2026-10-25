# Retired layout snapshot migration

The old `struct_additive`, `struct_overlap`, `struct_boxed_fields`,
`struct_boxed_tuple`, and `struct_unsafe_overlap` snapshot trees describe
removed storage strategies and are no longer discovered by the snapshot
runners. Their meaningful behavior is covered by the current
`Behavioral/VariantPayloadCoreTests`, `GroupedUnionTypes`, `VariantPayloadConsumers`,
JSON round-trip, and Unity variant-payload suites. The current generated struct
shape is asserted by `GroupedUnionTypes`: payloads are under
`{UnionName}Types.Variants`, while runtime storage is under `{UnionName}Types.Fields`.

Record snapshots remain active. JSON record snapshots also remain active; the
former struct/custom/fieldless JSON cases are covered by the current behavioral
JSON tests, including custom discriminators, field names, generic values, and
fieldless variants.
