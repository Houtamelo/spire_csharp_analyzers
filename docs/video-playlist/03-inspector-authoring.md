# 3. Authoring union data in Unity

Show a designer configuring playable quests through generated drawers, with
ordinary Unity asset editing and persistence.

## Example and feature grouping

Use the quest assets from episodes 1 and 2. Chop Chop's
[StepSO](https://github.com/UnityTechnologies/open-project-1/blob/608eac98df29cd97821a6115cd52dfb9027345b1/UOP1_Project/Assets/Scripts/Quests/ScriptableObjects/StepSO.cs)
also has a reward flag, item, and quantity. A nested reward union with None and
Item choices is a proposed second authoring example; migrate its original data
explicitly while retaining quest behavior.

Group `[Serializable]`, generated variant selection and active fields, nested
unions, Unity object references, Undo/Redo, and asset save/reload. Demonstrate a
list or prefab only where the selected game actually uses it.

## Demonstration sequence

1. Open an existing quest asset and show how its objective is configured.
2. Explain the union declaration and the attribute that enables serialization.
3. Select Dialogue, CheckItem, GiveItem, and CollectItems; show the fields for
   the active variant and assign real item assets.
4. Configure a nested reward and use Undo/Redo on the variant and field edits.
5. Save, reload, and play the configured quest. Verify the reward is delivered.
6. If the game needs nullable or polymorphic managed helper data, demonstrate
   `[UnitySerializeReference]` on that parameter using a working gameplay case.

## Preparation and recording evidence

Test edits through the native drawer, then save/unload/reload the real quest asset
and execute it. Include None reward and an item reward with a meaningful quantity.
Check prefab overrides or multi-object editing before including either in footage.

An ItemSO asset is a Unity object reference and does not need
`[UnitySerializeReference]`. That attribute is for managed helper data whose null,
runtime subtype, or shared identity matters. Do not invent a helper solely to
show the attribute. Spire's variant picker does not create arbitrary helper
subclasses; construct them in C# or use a separate authoring tool.

Keep the payload implementation behind the workflow. Link the
[serialization guide](../unity-package/Documentation~/Serialization.md) for
details. [Episode 6](06-evolving-saved-content.md) covers schema evolution.
