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
| A1 | [Layouts and allocations](a1-layouts-and-allocations.md) | Profile a real workload before choosing storage and matching syntax. |
| A2 | [Closure inliners](a2-closure-inliners.md) | Evaluate callbacks in Chop Chop's ingredient checks. |
| A3 | [JSON save data](a3-json-save-data.md) | Persist gameplay data through generated JSON converters. |
| A4 | [Record unions](a4-record-unions.md) | Compare struct and record representations of cooking outcomes. |
| Trailer | [Release overview](release-trailer.md) | Extract the clearest gameplay, Inspector, and compiler demonstrations. |

## Example projects

The original demo source was inspected on October 13, 2026. Example selection is
provisional: neither project has been upgraded, integrated with Spire, or validated
for filming. Existing game behavior is the baseline for each proposed refactor.

| Project | Local Unity project path | Inspected upstream revision | Original Editor |
|---|---|---|---|
| [Chop Chop](https://github.com/UnityTechnologies/open-project-1) | `examples/ChopChop/UOP1_Project` | `608eac98df29cd97821a6115cd52dfb9027345b1` | 2020.3.17f1 |
| [Boss Room](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop) | `examples/BossRoom` | `1299ba4fc97d83b634e7d32a86c438ba668d7f9c` | 6000.0.52f1 |

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
