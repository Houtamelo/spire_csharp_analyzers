# 1. Representing quest objectives with unions

Teach discriminated unions by preserving an existing quest's behavior while
making its objective data explicit. This is the first full walkthrough for
viewers who know C# but have not used unions.

## Example and feature grouping

Chop Chop's [StepSO](https://github.com/UnityTechnologies/open-project-1/blob/608eac98df29cd97821a6115cd52dfb9027345b1/UOP1_Project/Assets/Scripts/Quests/ScriptableObjects/StepSO.cs)
stores a `StepType` with Dialogue, GiveItem, and CheckItem choices, alongside an
item field and common dialogue/actor data. Its
[QuestManagerSO](https://github.com/UnityTechnologies/open-project-1/blob/608eac98df29cd97821a6115cd52dfb9027345b1/UOP1_Project/Assets/Scripts/Quests/QuestManagerSO.cs)
switches on that enum to check the inventory and advance the quest.

Propose a struct `StepAction` union for the three choices. Keep shared dialogue,
actor, and quest progress data on the existing host. Preserve GiveItem's current
meaning: deliver an inventory item to the quest, rather than reward the player.

Group `[DiscriminatedUnion]`, `[Variant]`, generated factories, `kind`, typed
properties, and exhaustive property patterns. Start with `Layout.Additive`.
Introduce serialization only enough to keep the example playable; episode 3
explains the authoring workflow.

## Demonstration sequence

1. Play the existing dialogue, item-check, and item-delivery quest steps.
2. Inspect the enum, the associated fields, and their consumer. Explain why each
   choice needs different data and why shared data stays outside the union.
3. Declare the three variants and inspect the generated API in Rider.
4. Replace the enum-based handler with property patterns using `var` captures.
5. Construct objectives through their factories. Compare the required arguments
   with the former enum-plus-fields representation.
6. Replay the same quest, including the case where the required item is absent.

## Preparation and recording evidence

Upgrade feasibility must be established: the inspected demo uses Unity
2020.3.17f1. Keep a working original snapshot and migrate the selected quest
assets explicitly; a variant alias does not convert the old enum-based schema.

Verify all three original behaviors, including inventory removal only when the
delivery succeeds. Show that Dialogue no longer exposes an unrelated item field.
Spire encodes the variant's fields; application validation still decides whether
an assigned item reference is usable.

Keep layout internals and performance comparisons for the advanced episode.
Continue with [adding a gameplay feature](02-adding-gameplay-safely.md).
