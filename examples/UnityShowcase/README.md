# Spire Unity showcase

Openable Unity 6000.5.6f1 URP project with a saved scene, a saved ScriptableObject
preset, editable union drawers, runtime examples, and Unity Test Framework tests.
Assets, their metadata, package configuration, and project settings belong in Git.
Unity caches, machine-specific settings, generated IDE files, and builds are ignored.

The packager also delivers `Assets` as the optional **Unity Showcase (URP)** UPM
sample. Customer imports use its authored GUIDs and resolve the imported asset
location. See [the distributable sample instructions](../../docs/unity-package/Showcase.md).
`sample-files.txt` lists required authored files; update it when adding/removing
sample assets. Export validates that inventory before writing any destination.

## Open and run

Build the Unity package from the repository root:

```sh
dotnet run -c Release --project tools/Spire.Tool -- package
```

Add `examples/UnityShowcase` to Unity Hub and open it with Unity 6000.5.6f1.
Its manifest references the locally built package under `tmp/unity-package`.
Keep the project in this repository; build the package again after changing Spire.
The project pins SDK 8 for Unity/Rider tooling instead of inheriting the
repository's SDK selection. Run the SDK 10 package-building command above from
the repository root.

Open `Assets/Scenes/Showcase.unity`, select
`Assets/Examples/ShowcasePreset.asset`, and change its variants and active fields
in the Inspector. Enter Play Mode to execute the selected command or step through
the command queue. Runtime state resets through the demo's reset button; preset
edits are ordinary Unity asset edits and can be saved and reviewed in Git.

**Tools → Spire → Create or Reset Showcase Examples** restores the supplied preset
and scene. This intentionally replaces their example data, so save any custom
presets or scenes separately.

The project uses URP 17.5.0. `Assets/ShowcaseURP.asset` is assigned in Graphics
and every quality level, with `Assets/URP_RendererData.asset` as its renderer.
The scene camera has URP camera data, and the project uses Linear color space.
**Tools → Spire → Configure Showcase URP** reapplies that configuration.

## Features

| Example | Feature |
|---|---|
| `GameplayCommand` | Additive union, fieldless/scalar/reference variants, list authoring, generated Inspector drawer, exhaustive property patterns |
| `Shape` and `ShowcaseAlgorithms.Area` | Guarded field extraction and compiler-checked exhaustive matching |
| `OperationResult<int>` | Generic union using BoxedFields, success/failure variants |
| `BoxedTupleStats`, `UnsafeStats`, `OverlapShape` | Remaining storage layouts using the same serialization/Inspector workflow |
| `GameplayCommand.ApplyEffect` | `[UnitySerializeReference]`, concrete subclasses, null values, and shared references within the preset |
| `GameplayCommand.Speak` | One former variant name through `[Variant(FormerlySerializedAs = "Say")]` |
| `CountMoves` | `SpireLINQ.OfKind` |
| `ActorMode` and `ReadMode` | Initialization/exhaustiveness enforcement and checked integer-to-enum conversion |
| `DoubleScore`, `AddDoubledScore`, `ReduceScores` | Generated struct forwarders and `[Inlinable]` overloads |
| `Assets/CompilerExamples/Diagnostics.cs.txt` | Intentionally invalid snippets to try in Rider without breaking the example project |

Queue entries four and five share a `DamageEffect`; editing its amount affects
both entries in this one host asset. Entry six has a null effect. The native union
drawer selects union variants; constructing arbitrary managed-reference subclasses
is demonstrated in C# by `ResetExamples` and the HealingEffect runtime button.

`UnsafeOverlap` requires the project's enabled unsafe-code setting. `Overlap`
retains its restriction against generic unions. Unity owns field-type eligibility.
Alias support covers variant names, not arbitrary field/type/assembly migrations.
See [the Unity guide](../../docs/unity.md) for support and serialization limits.

## Tests

Open **Window → General → Test Runner**, choose **EditMode**, and run
`Houtamelo.Spire.Showcase.Tests`. Tests use the real generated code, real Unity
asset save/unload/reload, and the supplied scene. Temporary test assets are removed
after each persistence test; they do not overwrite the authored preset.

For a batch run from the repository root, with `SPIRE_UNITY_EDITOR` set to the
Editor executable:

```sh
"$SPIRE_UNITY_EDITOR" -batchmode \
  -projectPath "$PWD/examples/UnityShowcase" \
  -runTests -testPlatform EditMode \
  -assemblyNames Houtamelo.Spire.Showcase.Tests \
  -testResults "$PWD/tmp/unity-showcase-tests.xml" \
  -logFile "$PWD/tmp/unity-showcase-tests.log"
```

Use an available display or Xvfb on Linux for the Play Mode rendering smoke test.
Unity exits when the test run finishes. The project consumes the current local
package; building it requires the SDK, while running its Unity tests uses the Editor.

Validation on October 11, 2026, with Unity 6000.5.6f1, Linux, under Xvfb passed
all 13 tests after the URP migration, including a real camera render, Play Mode
execution, native persistence for all layouts, reference sharing/nulls, variant
rename migration, and stable unchanged saves. The test run leaves the authored
scene and preset byte-identical. The review repaired Unity value-type snapshot
comparison and made the batch rendering probe explicit; see the
[commercial review](../../docs/reviews/2026-10-11-commercial-readiness.md).
