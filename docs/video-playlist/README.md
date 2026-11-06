# Spire Unity release video playlist

Planning outlines for a mixed audience, starting with union fundamentals through
working gameplay. These describe what to demonstrate, not narration. Video length
follows the example; no fixed duration is imposed.

Use one recurring game for continuity and a second game where it offers a better
fit. The six core episodes form the initial proposal. Advanced episodes are
optional follow-ups; their numbering does not commit them to the release schedule.
Assemble the trailer after recording the walkthroughs.

| Order | Outline | Example and purpose |
|---|---|---|
| 1 | [Quest objectives](01-quest-objectives.md) | Chop Chop: learn unions by refactoring existing quest choices. |
| 2 | [Adding gameplay safely](02-adding-gameplay-safely.md) | Extend those objectives; let diagnostics identify incomplete handlers. |
| 3 | [Inspector authoring](03-inspector-authoring.md) | Author and persist quest data through generated Unity drawers. |
| 4 | [Meaningful failure feedback](04-cooking-outcomes.md) | Chop Chop: turn cooking outcomes into useful player feedback. |
| 5 | [Protecting existing code](05-existing-code-safety.md) | Boss Room: enum exhaustiveness, checked conversions, and initialization. |
| 6 | [Evolving saved content](06-evolving-saved-content.md) | Rename and reorder variants without losing authored quest data. |
| 7 | [Burst material variants](07-burst-materials.md) | CPU path tracer: typed surface parameters in a native rendering job. |
| 8 | [DOTS bot states](08-dots-firefighters.md) | Firefighters: typed state destinations in an ECS simulation. |
| A1 | [Layouts and allocations](a1-layouts-and-allocations.md) | Profile a real workload before choosing storage and matching syntax. |
| A2 | [Closure inliners](a2-closure-inliners.md) | Evaluate callbacks in Chop Chop's ingredient checks. |
| A3 | [JSON save data](a3-json-save-data.md) | Persist gameplay data through generated JSON converters. |
| A4 | [Record unions](a4-record-unions.md) | Compare struct and record representations of cooking outcomes. |
| Trailer | [Release overview](release-trailer.md) | Extract the clearest gameplay, Inspector, and compiler demonstrations. |

## Example projects

The initial source review was October 13, 2026. October 27 work adds Spire sibling
projects and Burst/DOTS examples; the [integration report](../reviews/2026-10-27-spire-demo-integration.md)
records the completed checks and remaining filming preparation. Existing game
behavior remains the baseline for the refactors.

| Project | Local Unity project path | Inspected upstream revision | Original Editor |
|---|---|---|---|
| [Chop Chop](https://github.com/UnityTechnologies/open-project-1) | `examples/ChopChopSpire` | `608eac98df29cd97821a6115cd52dfb9027345b1` | 2020.3.17f1 |
| [Boss Room](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop) | `examples/BossRoomSpire` | `1299ba4fc97d83b634e7d32a86c438ba668d7f9c` | 6000.0.52f1 |
| [DOTS Samples](https://github.com/Unity-Technologies/EntityComponentSystemSamples) | `examples/FirefightersSpire` | `6786a741ee1f118ed14cecfa02beae8e926937b0` | 6000.2.10f1 |
| [ToyPathTracer](https://github.com/aras-p/ToyPathTracer) | `examples/BurstPathTracerSpire` | `ff20bfe777f38632277b84831a1ac62a3eca5c32` | 2021.3.16f1 |

Only the Spire adaptations are kept locally. Their `upstream` and `without-spire`
refs preserve comparison baselines; the separate original checkouts were removed.
The paths above use Unity 6000.5.6f1; the final column records the upstream Editor.

[UnityShowcase](../../examples/UnityShowcase/README.md) remains the existing Spire
example and a reference for generated APIs. It is not a substitute for validating
the selected demo refactors. See [demo checkout instructions](../../examples/README.md).

## Recording approach

Start each walkthrough with the gameplay or workflow the viewer will improve.
Show the relevant original code, make one focused change, and finish with the
working result. Keep shared infrastructure intact unless the episode needs a
specific change. Briefly introduce new syntax before relying on it.

Show intentional failures as well as the repaired behavior. Record the actual
diagnostic and quick fix, and verify the result in Unity. Keep before/after code
snapshots reproducible. Separate Editor, Mono player, and IL2CPP evidence whenever
presenting compatibility or performance results.

Before filming, choose the release Editor from the [support guide](../unity.md),
confirm each demo opens there, and run real integration checks for the changed
gameplay and persistence paths. The downloaded projects are source baselines;
their presence does not establish Spire integration or compatibility.
