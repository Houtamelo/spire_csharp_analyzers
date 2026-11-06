# Spire examples

Only the four Spire adaptations are tracked as submodules. The parent repository
records their commits and URLs; project files and edits belong to those repositories.
The duplicate original checkouts have been removed.

No pushes are authorized. The GitHub forks exist, but the local Spire comparison
branches have not been published there yet.

| Submodule | GitHub fork |
|---|---|
| `BossRoomSpire` | [Boss Room](https://github.com/Houtamelo/spire-example-boss-room) |
| `ChopChopSpire` | [Chop Chop](https://github.com/Houtamelo/spire-example-chop-chop) |
| `BurstPathTracerSpire` | [Path tracer](https://github.com/Houtamelo/spire-example-path-tracer) |
| `FirefightersSpire` | [DOTS](https://github.com/Houtamelo/spire-example-dots) |

Each adaptation opens as a Unity project at its repository root and targets Unity
6000.5.6f1. Its existing standalone history is preserved. Compare `without-spire`
with `with-spire` to inspect integration changes without Editor upgrade noise;
`upstream` identifies the original project snapshot. Chop Chop also has
`spire-item-so`, the serialized item-model example. No separate original checkout
is needed to inspect these baselines.

After the recorded commits are published, initialize them with
`git submodule update --init --recursive`. Submodule checkout uses the recorded
commit, normally with a detached HEAD. To edit a project, select or create a
branch inside its repository and commit there. Publish that commit before
updating the parent pointer with `git add examples/NAME`.

## Local-only projects

`UnityShowcase`, `2DPlatformer`, and `Playground` are ignored by the parent
repository and are not submodules. They are excluded from publication. The first
two retain local Git history with no GitHub remote; Playground remains untouched.
A fresh clone of Spire will not contain these local projects.

Package preparation currently requires the local `UnityShowcase` project: its
`sample-files.txt` and authored assets supply the optional UPM showcase sample.
Keep it available when running package or example preparation. This requirement
also applies to packaging in a fresh checkout; the sample has no public checkout
source while the project remains local-only.

## Preparation

Build the local package and validate the four adaptation projects:

```sh
dotnet run -c Release --project tools/Spire.Tool -- examples
dotnet run -c Release --project tools/Spire.Tool -- examples --example FirefightersSpire
```

`SpireExamples.json` records the upstream source revisions and the `without-spire`
comparison refs. Preparation accepts local or `origin` comparison branches and
validates Unity files and Git refs without changing checkouts or local edits.
`Directory.Build.props` keeps Spire's parent build policy out of Unity-generated
IDE projects.

See the [integration report](../docs/reviews/2026-10-27-spire-demo-integration.md)
and [video plans](../docs/video-playlist/README.md) for the recorded checks and
feature walkthroughs. Repository cleanup does not add new Unity runtime validation.
