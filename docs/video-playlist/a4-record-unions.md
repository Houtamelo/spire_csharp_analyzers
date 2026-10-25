# A4. Choosing record unions for runtime outcomes

An optional follow-up for experienced C# viewers: compare two representations of
the same cooking outcome and explain when reference semantics fit the design.

## Example and feature grouping

Reuse the `CraftResult` behavior from [episode 4](04-cooking-outcomes.md).
Keep a working struct version, then build an equivalent record version for a
runtime result passed from gameplay code to UI. This comparison does not replace
the quest's ScriptableObject architecture or its Inspector-authored union fields.

Group an abstract partial record union, nested sealed record variants,
variant construction, type/property patterns, and exhaustive handling. Explain
record value equality and reference sharing only where the example uses them.

## Demonstration sequence

1. Replay the successful and failed cooking interactions using the struct result.
2. Declare the equivalent record union and inspect its generated support.
3. Update consumers to match variant types and extract their fields.
4. Verify the same UI and inventory behavior with both implementations.
5. Compare construction allocation, copying/reference semantics, and the code
   each representation needs.
6. Choose a representation for this use case and explain the concrete tradeoff.

## Preparation and recording evidence

Verify record syntax and any required init compatibility support in the chosen
Unity Editor and player. Test Crafted, RecipeNotOwned, and MissingIngredients;
show a missing-case diagnostic on the record version before repairing it.

Record variants allocate reference objects and have no struct layout selection.
Generated Unity serialization and drawers apply to supported mutable struct
unions, not record unions. Keep this example runtime-only unless a separately
validated JSON persistence segment is deliberately included.

Describe record support specifically: arbitrary non-record class unions are not
supported. See [record unions](../discriminated-unions.md#record-unions).
