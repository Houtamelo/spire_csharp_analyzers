# Spire demonstration integration

October 28: the Boss Room connection-feedback union was rejected because it
added a mapping layer over already separate UI callbacks. Its replacement is the
[enemy AI hierarchy conversion](2026-10-28-boss-room-ai-union.md). The old Boss
Room feedback results below describe the earlier implementation; consult the
new review for current AI evidence.

The four sibling projects contain actual Spire gameplay integrations. Their
original checkouts remain unchanged. Run `dotnet run -c Release --project
tools/Spire.Tool -- examples` from the repository root to validate the independent
example Git repositories and build Spire. Project files and Git checkouts are preserved.
All siblings target Unity 6000.5.6f1 on Linux x64.

## Examples and evidence

| Project | Integration | Native checks | Play Mode |
|---|---|---|---|
| [BurstPathTracerSpire](../../examples/BurstPathTracerSpire/README.md) | Lambert, Metal, and Dielectric materials inside the actual Burst renderer. | 3/3 groups pass: unmanaged storage, confirmed native execution, original-renderer pixel comparison. | Pass: actual scene, renderer, camera, and frames. |
| [FirefightersSpire](../../examples/FirefightersSpire/README.md) | State-specific destinations in the actual DOTS bot component and systems. | 3/3 groups pass, including real World transitions and native Burst execution. | Pass: completed Step 4 creates bot entities. |
| [BossRoomSpire](../../examples/BossRoomSpire/README.md) | Exhaustive connection events mapped to typed popup and reconnect feedback. | 14/14 checks pass; 10/10 original loopback host/client tests pass with the final parser. | Pass: Startup creates its live ConnectionManager. |
| [ChopChopSpire](../../examples/ChopChopSpire/README.md) | Serializable quest actions and explicit cooking outcomes consumed by the game. | 5/5 groups pass: factories, migrated asset reload, actual quest handling, cooking success/failure, Polybrush ownership. | Pass: Initialization loads the persistent managers and main menu. |

The path tracer is Aras Pranckevičius's independent ToyPathTracer, rather than an
official Unity demo. Firefighters comes from Unity's official Entities 101 sample.
The new playlist outlines are [Burst materials](../video-playlist/07-burst-materials.md)
and [DOTS states](../video-playlist/08-dots-firefighters.md).

Native execution is checked with BurstDiscard markers, so managed fallback does
not count as a pass. The path tracer compares 8x8 pixels against the retained
original renderer with a 0.001 tolerance. This proves compatibility and output
agreement for the selected workload; it is not a measured speedup.

Firefighters exercises movement, fetching/carrying, filling/passing, dousing,
heat clearing, and team effects in real ECS Worlds. Boss Room preserves the
application status numbers, messages, and JSON wire representation. Its original
network tests exercise real local hosts/clients, reconnection, shutdown, and
cancellation. Cloud services and older-NGO interoperability are outside this run.

Chop Chop tests use real project item, quest, and channel assets. They check
missing/present items, exact collection quantities, delivery events, replacement
item preservation, repeated cooking ingredients, exact shortage payloads, and
failed-cook inventory atomicity. Migrated quest assets are committed in the example repository.

## Generator changes

Inner unmanaged storage helpers now use the compiler's default sequential layout;
the explicit offset-zero overlay remains. Scalar, composite, generic, size, and
round-trip regressions are covered. No unsafe-code setting or Unsafe DLL is added.

Native Burst testing also found unsupported System.Diagnostics assertion code.
When Unity's usable assertion API is present, generated accessors use it.
Nonserialized Unity unions use constant assertion messages, avoiding an
unsupported managed formatting path. Other hosts retain the existing .NET API.

C# switch expressions can emit a boxed SwitchExpressionException fallback even
when Spire proves named variants exhaustive. The Burst examples use exhaustive
switch statements. Adding a variant still exposes missing consumers.

A Serializable union has a managed serialization cache. Users needing Inspector
authoring and unmanaged Burst/ECS data must manually declare two unions and
convert or bake between them. This limitation is documented in the Unity guide
and the packaged customer guide; Spire does not generate that conversion.

## Project upgrades and preparation

Each sibling now has its own Git history: `upstream` contains the exact original
project tree, `without-spire` separates Unity compatibility changes, and
`with-spire` contains the integration and validation scripts. Compare the latter
two branches to review the Spire conversion. The repositories are local, with no
published remotes; the parent repository ignores them. `SpireExample.json`
retains provenance, including the recovered Chop Chop prefabs. No overlay
manifests or file allowlists remain. Pinned original submodules remain unchanged.
Package lock files retain the resolved dependency versions.

The C# example command validates the catalog, real Git roots and comparison refs,
and required Unity files before building the local package. It never copies or
checks out project files. Integration tests use real Git directory repositories
and linked worktrees, covering dirty/detached checkouts and failure preservation.
The new `without-spire` branch snapshots have not been run separately in Unity;
the existing native results below describe the `with-spire` project contents.

| Repository | `without-spire` | `with-spire` |
|---|---|---|
| BossRoomSpire | `c26e3ea` | `09bb671` |
| BurstPathTracerSpire | `912369b` | `9126987` |
| ChopChopSpire | `fc416a6` | `24beb20` |
| FirefightersSpire | `0ed14c5` | `96349e1` |

All four repositories are clean on `with-spire`. Each `upstream` tree hash equals
its pinned original project subtree hash. Their object databases are independent
of the original repositories; Boss Room's copied LFS objects pass `git lfs fsck`.
Checksums confirm that conversion preserved every former integration-manifest
file byte for byte. No Unity instance was launched for the Git conversion.

Chop Chop upgrades URP, Input System, Addressables, localization, rigging, and
Editor dependencies from Unity 2020.3. Cinemachine stays on 2.x. Embedded Polybrush
1.2.1 retains its license and notices; three Editor files adapt mesh ownership to
Unity's EntityId API. Retained whiteboxing assets require its runtime components.
Unused Recorder tooling is removed. An orphaned depth-normals renderer subasset
and its unconsumed URP 10 sample are excluded. Unity's actual material upgrader
has been applied and saved, and the input wrapper regenerated with Input System
1.20.1. The saved graphics/material settings and quest assets are tracked.

The original MainMenu also references four deleted scene-chunk prefabs. The
sibling restores their exact GUIDs and contents from upstream commit
`20095ca4cd35b8d565ae003322231c4afc1e1dfa`. All nested mesh/material dependencies
resolve in the current baseline. Unity's repair of stale prefab-instance records
is saved in the sibling menu scene; the original scene remains unchanged.

Unity 6000.5 Linux's Crunch encoder aborted on multiple original textures in
crn_threading_pthreads. A sibling-only import hook disables Crunch for that Editor
and OS, retaining GPU compression and the original texture sources/metadata.
This is a demo import workaround, not a Spire runtime dependency.

Boss Room upgrades NGO, VContainer, Cinemachine, Input System, multiplayer
services/tools, URP, and tutorial packages. New NGO transport failures can contain
plain text; the original client states assumed every reason was application JSON.
The sibling now preserves valid original status JSON and handles unstructured
failures through the existing connection/retry behavior. Parsing rejects missing,
malformed, and unknown statuses using SpireEnum.TryFrom.

Firefighters omits an unused old Visual Scripting dependency whose template uses
removed Editor APIs. Other Entities 101 scenes remain available.

## Validation scope and reproduction

The .NET source-generator suite passes **819/819**, behavioral tests **93/93**,
and DevTools tests now pass **128/128**, including **16** real-Git workflow cases.
The fixture hashing helper also compiles for the portable `netstandard2.1` Unity
test host. The current native example checkouts pass the production Git/project
validator. Fresh tooling evidence is
`tmp/spire-demo-integration/tests/tooling-git-examples-final.trx`.

Every project exposes `SpireExamples.SpireExampleValidation.Run` and
`SpireExamples.SpireExamplePlayModeSmoke.Run` for Unity's `-executeMethod`.
They write SpireExampleResults.json and SpirePlayModeResults.json respectively,
then exit with status zero only on success. Smoke runs preserve domain reloads,
wait for Editor initialization, prewarm Search, and reject errors and assertions.

Runs use Xvfb; scene smoke checks additionally use software OpenGL. Automated
instances isolate PulseAudio, PipeWire, and ALSA from host devices. Audio is
disabled where possible; mixer-dependent checks use null backends instead.
Temporary project audio settings are restored afterward. No user-wide audio
settings are modified.

Logs, native JSON/XML reports, and .NET TRX reports are under
`tmp/spire-demo-integration/`. Key evidence: burst-validation-normal-cache.log,
burst-playmode-3.log, dots-validation-5.log, dots-playmode-3.log,
boss-validation-final-2.log, boss-network-clean.xml,
chop-validation-12.log, chop-playmode-final.log, and
tests/source-generators-final.trx. The final networking wrapper and Unity process
both exit successfully.

These checks cover the selected Editor, native Burst paths, and Linux headless
startup. They do not establish demo player builds, other platforms, a complete
quest playthrough, cloud multiplayer, or performance improvements. Firefighters
shutdown emits a [three-allocation Persistent leak warning](../issues/2026-10-27-firefighters-shutdown-allocation-warnings.md);
its origin is not yet isolated, and no attribution to Spire is established.
Manual recording/playthrough
review remains necessary before filming the final walkthroughs.
