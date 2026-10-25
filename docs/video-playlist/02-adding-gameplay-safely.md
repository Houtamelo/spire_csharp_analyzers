# 2. Adding a gameplay feature without missing a handler

Demonstrate the development benefit of a closed set of variants: adding a quest
objective reveals the code that must change before the new behavior ships.

## Example and feature grouping

Continue the `StepAction` refactor from [episode 1](01-quest-objectives.md).
Chop Chop's original
[QuestManagerSO](https://github.com/UnityTechnologies/open-project-1/blob/608eac98df29cd97821a6115cd52dfb9027345b1/UOP1_Project/Assets/Scripts/Quests/QuestManagerSO.cs)
already checks item-based objectives. A proposed CollectItems objective adds a
required quantity, using the existing inventory count operation. This extension
must be implemented and exercised; it is not an existing Spire demo feature.

Group union exhaustiveness (SPIRE009), wrong-variant and unguarded field access
(SPIRE013/014), missing-arm quick fixes, and wildcard expansion. Property-pattern
captures use `field: var field`, retaining null values and the property's type.

## Demonstration sequence

1. Show the existing quest and its quantity limitation.
2. Add CollectItems with an item and required quantity. Leave the current handler
   unchanged long enough to show the missing-variant diagnostic.
3. Apply Rider's missing-arm fix. Explain that it creates a handler skeleton;
   the developer supplies the actual gameplay logic.
4. Implement the inventory count check and play both insufficient and sufficient
   quantity cases.
5. Briefly attempt an unguarded variant-field read, then fix it by narrowing the
   active kind and extracting its fields.
6. Demonstrate wildcard expansion in a separate relevant consumer. Explain that
   a catch-all can intentionally hide future additions from exhaustiveness checks.

## Preparation and recording evidence

Prepare a quest with a reachable item source and clear progress feedback. Reuse
the original quest handler; add another consumer only if gameplay genuinely needs
it, such as a visible objective description.

Record the real diagnostic, the applied fix, and successful Unity recompilation.
Check threshold behavior, wrong item, and quest completion without duplicate
rewards. Avoid implying that exhaustiveness checking validates quantity rules,
handles every state transition, or proves the whole game correct.

This episode provides the strongest compiler footage for the
[release trailer](release-trailer.md). Continue with
[Inspector authoring](03-inspector-authoring.md).
