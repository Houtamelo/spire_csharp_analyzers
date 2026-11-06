# Upgrading safely

Preserve your project in version control before replacing the package. Keep the
DLL `.meta` files: they control analyzer loading and runtime plugin inclusion.
Use Package Manager to select the replacement package's `package.json`, allow
Unity to recompile, then check the Console and Rider's generated members.

Inspect representative saved scenes, prefabs, and ScriptableObjects. Load and
save them without edits and review the YAML diff. Exercise each stored variant,
managed-reference sharing/nulls, and your target player backend. Keep your own
assemblies and union type identities unchanged during a package-only upgrade.

The concrete serialization-wrapper update reads the previous
`SpireSerializedPayload<T>` representation and writes per-variant subclasses.
When moving existing assets to Unity 2022, install the update and resave those
assets in a newer supported Editor first. Unity 2022 rejects the old generic
managed-reference objects; this migration does not make generic unions
serializable in Unity 2022.

Import the showcase once. Reimporting samples from another package version can
create a second copy with the same authored GUIDs and assembly names. Remove the
previous unmodified sample copy first, or retain your customized copy and compare
the new sample outside the project. Keep customized assets separately from the
resettable demo preset and scene.

Package updates and application schema migrations are separate. The supported
variant rename mechanism is one `Variant.FormerlySerializedAs` name. It does
not migrate field names/types, inline versus managed-reference changes, union
types, namespaces, or assemblies. Do not discard an existing alias while assets
still contain that name. See [Serialization](Serialization.md).

If a load fails after an upgrade, restore the prior package and assets first;
do not save invalid defaulted data over the original assets. Compare the first
error and isolate the smallest asset/declaration pair before migrating it.
