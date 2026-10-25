# A3. Persisting gameplay data with generated JSON converters

An optional walkthrough for player save files or replay data. Demonstrate an
actual disk roundtrip and distinguish it from Unity scene/prefab serialization.

## Example and feature grouping

Use quest progress or cooking-history data from the earlier episodes. Inspect
Chop Chop's save system before selecting a boundary; its inventory manager
already triggers disk saves after operations. This outline proposes an extension,
not a claim that its existing schema is a union or needs replacement.

Group `JsonLibrary.NewtonsoftJson` or `JsonLibrary.SystemTextJson`, generated
converters, discriminator configuration, and `[JsonName]` for variant and parameter
names. Show one library throughout, with the other as a brief supported option.

## Demonstration sequence

1. Show the gameplay state to preserve and the application's current save flow.
2. Define a save representation with stable item/quest identifiers rather than
   expecting Unity object references to reconstruct themselves from JSON.
3. Enable the chosen converter and inspect a real serialized value.
4. Write a save, restart/load, and verify the reconstructed gameplay state.
5. Show how explicit JSON names can preserve a chosen external name while the
   corresponding C# variant is renamed.
6. Feed malformed or unrecognized data to the actual load boundary and show the
   application's verified failure behavior.

## Preparation and recording evidence

Provide the chosen JSON library in the Unity project and check its player build.
Test the actual converter's failure behavior before planning its footage; do not
promise automatic recovery or a specific exception without verifying it.

Exercise each serialized variant and its fields, then restore real item/quest
references through the application's identifier lookup. Show a versioned save
snapshot if migration is part of the example. Unity's variant former-name alias
does not automatically define a JSON migration policy.

This is separate from generated Unity asset callbacks and does not provide
automatic Netcode support. See [JSON serialization](../discriminated-unions.md#json-serialization)
and [Unity usage](../unity-package/Documentation~/Usage.md).
