# A2. Evaluating closure inliners in ingredient checks

An optional advanced walkthrough: investigate callback allocation and dispatch
in a gameplay operation, then compare generated struct callbacks with the
existing code and a straightforward loop.

The entire closure inliner feature is experimental and may change or be removed.
Explain the `SPIREEXP001` opt-in on compilers that recognize `ExperimentalAttribute`.

## Example and feature grouping

Chop Chop's
[InventorySO](https://github.com/UnityTechnologies/open-project-1/blob/608eac98df29cd97821a6115cd52dfb9027345b1/UOP1_Project/Assets/Scripts/Inventory/ScriptableObjects/InventorySO.cs)
uses capturing predicates in ingredient availability checks. First establish how
often they run and whether their costs matter. This is a candidate investigation,
not an established game bottleneck.

Group `[InlinerStruct]`, `[Inlinable]` delegate parameters, generated generic
overloads, and the matching action/function inliner interfaces. The existing
[showcase algorithms](../../tests/UnityPackageSample/Assets/Scripts/ShowcaseAlgorithms.cs)
illustrate the API but are not the main gameplay demonstration.

## Demonstration sequence

1. Cook with a realistic inventory and trace the ingredient-check operation.
2. Profile the capturing predicates under that workload and explain the measured
   allocation, if present.
3. Compare a direct loop to establish what a simpler implementation achieves.
4. If a reusable callback API is justified, make its containing type partial and
   annotate its delegate parameter. Show the generated struct overload.
5. Use a generated stateless forwarder or an explicitly stateful struct callback
   as appropriate. Do not describe captured state as automatically converted.
6. Run the same correctness checks and compare allocations/timing in Unity.

## Preparation and recording evidence

Spire does not retrofit List.Exists or other framework APIs. Adapting this example
requires an application-owned method whose body invokes the callback. Keep that
extra code visible so viewers can assess the tradeoff.

Verify availability results for present, missing, and insufficient ingredients.
Record the actual overload used and measure Mono and IL2CPP separately if claiming
both. Desktop JIT monomorphization benefits do not establish IL2CPP speedups.
If a direct loop is the better solution here, report it and choose another real
callback use case before making this a release demonstration.

See the public contracts for
[InlinerStruct](../../src/Houtamelo.Spire/InlinerStructAttribute.cs) and
[Inlinable](../../src/Houtamelo.Spire/InlinableAttribute.cs).
