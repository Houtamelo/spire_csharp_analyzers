# 4. Giving players meaningful failure feedback

Use a cooking interaction to show why an outcome can carry more useful
information than a boolean or a method that returns nothing.

## Example and feature grouping

Chop Chop's
[InventoryManager.CookRecipeEventRaised](https://github.com/UnityTechnologies/open-project-1/blob/608eac98df29cd97821a6115cd52dfb9027345b1/UOP1_Project/Assets/Scripts/Inventory/InventoryManager.cs)
checks recipe ownership and ingredient availability before removing ingredients
and adding a dish. Its method returns no outcome. The inventory's
[hasIngredients](https://github.com/UnityTechnologies/open-project-1/blob/608eac98df29cd97821a6115cd52dfb9027345b1/UOP1_Project/Assets/Scripts/Inventory/ScriptableObjects/InventorySO.cs)
provides only an availability boolean.

Propose a `CraftResult` with Crafted, RecipeNotOwned, and MissingIngredients
variants. Carry the produced dish or concrete missing-item quantities as relevant.
Keep the existing event channels and persistence boundary where practical.

Group variant-specific outcome data, exhaustive UI handling, and guarded field
extraction. Introduce a generic result abstraction only if another actual operation
benefits from sharing it. `SpireLINQ.OfKind` is a small optional addition if an
existing outcome history needs filtering.

## Demonstration sequence

1. Attempt cooking without the recipe, with insufficient ingredients, and with
   everything required. Show the feedback the current implementation provides.
2. Separate the cooking operation's result from the UI's presentation of it.
3. Declare the result variants and return the appropriate outcome from each path.
4. Handle each variant in the UI, including which ingredients and quantities
   are missing. Show the compiler identifying an omitted outcome.
5. Repeat the three interactions and verify the feedback and inventory changes.

## Preparation and recording evidence

Use actual recipes, ingredient sources, and inventory UI. If failure feedback is
already provided elsewhere in the demo, document it before choosing the refactor;
the inspected method alone does not establish that the whole game lacks feedback.

Check that failure consumes nothing, success consumes only the intended
ingredients, and the produced dish is added once. Missing-quantity calculation
and transaction correctness are application code, not generated guarantees.
Prepare the save/reload check if persistence is shown.

Continue with [existing-code safety](05-existing-code-safety.md). Reuse these
outcomes for the optional [record comparison](a4-record-unions.md).
