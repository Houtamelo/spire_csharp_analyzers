# Release trailer: gameplay, authoring, and compiler feedback

Create a short overview from verified walkthrough footage. Its purpose is to
make Spire's workflow benefit immediately visible and lead viewers into the
playlist. Choose the duration after selecting the strongest shots.

## Feature grouping and shot order

| Sequence | Footage | What it communicates |
|---|---|---|
| 1 | Play the completed quest or cooking interaction. | The examples belong to a working game. |
| 2 | Briefly show the variant declarations and a typed handler. | Define alternative data shapes with a small public API. |
| 3 | Switch the objective variant and edit its fields in Unity. | Inspector authoring comes from the union declaration. |
| 4 | Add a variant, show the missing-handler diagnostic, apply a Rider fix. | New gameplay choices reveal incomplete consumers. |
| 5 | Play the repaired behavior. | The fix produces a visible result. |
| 6 | Rename a variant and reload the saved quest, if the sequence is clear. | Authored content can survive the demonstrated rename workflow. |
| 7 | Show the release identity and links to the store and full walkthroughs. | Provide a clear next step. |

Use shots from [quest objectives](01-quest-objectives.md),
[adding gameplay safely](02-adding-gameplay-safely.md),
[Inspector authoring](03-inspector-authoring.md), and
[saved-content evolution](06-evolving-saved-content.md). Cooking feedback is an
alternative gameplay opening if it communicates the result more clearly.

## Preparation and recording evidence

Record the real game, generated UI, diagnostics, and applied fixes. Prepare a
coherent before/after state so the cut does not imply that the quick fix itself
implements the gameplay logic. Keep snippets readable at the final video size.

Use the support matrix and release metadata approved for the actual submission.
Only include performance or player-compatibility claims backed by measurements
for the version/platform shown. Keep layout internals and backend explanations
in the full walkthroughs unless they improve the trailer's central demonstration.

Present the modified Unity demos as examples used to demonstrate Spire. Do not
imply Unity endorsement. Add final store and playlist URLs when those exist;
their absence is a production dependency, not an instruction to publish now.
