# 6. Evolving authored content safely

Demonstrate variant renaming and reordering against saved quest content, making
the persistence behavior visible through gameplay and Git diffs.

## Example and feature grouping

Use the converted quest assets from [episode 3](03-inspector-authoring.md).
Rename the union's GiveItem variant to DeliverItem without changing what it does.
The original enum-based demo data must already have been migrated into the union
format; this episode addresses changes within that format.

Group `[Variant(FormerlySerializedAs = "GiveItem")]`, variant reordering,
current-name and former-name payloads, and stable saves of unchanged data.
Explain the workflow before the generated implementation.

## Demonstration sequence

1. Save a playable quest using GiveItem, then capture its values and YAML baseline.
2. Save/reload the unchanged asset and inspect the diff.
3. Rename the variant with its former-name alias. Update source consumers and
   reload the existing asset without recreating its values.
4. Play the quest and verify the item delivery and reward behavior.
5. Save the migrated asset and inspect its expected name-related changes.
6. Reorder the variant declarations, reload, and repeat the gameplay check.
7. Save the now-current data again and verify that an unchanged save is stable.

## Preparation and recording evidence

Keep separate before-rename, after-rename, and reordered snapshots. Verify item
reference identity, quantities, active variants, and full unchanged YAML rather
than only the displayed fields. Expect a migration diff on the first renamed
save; distinguish that from recurring changes on unchanged saves.

One former name is supported per variant. Resave affected content before another
rename replaces the alias. This mechanism does not migrate parameter names/types,
removed variants, union or assembly identities, or a change to managed-reference
field encoding. Use the terms current-name and former-name payload.

Show missing-data fallback only as a short troubleshooting segment if it serves
the example; it defaults to the first variant with a warning, not recovered data.
See [serialization](../unity-package/Documentation~/Serialization.md) and
[upgrading](../unity-package/Documentation~/Upgrading.md).
