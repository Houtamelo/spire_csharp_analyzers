# Spire examples and upstream demo baselines

[UnityShowcase](UnityShowcase/README.md) is the existing Spire-integrated example.
Chop Chop and Boss Room are upstream demo checkouts for the
[release video plans](../docs/video-playlist/README.md). They are kept as pinned
Git submodules so their source, assets, metadata, and upstream notices remain
together without copying their history into Spire's main repository.

| Demo checkout | Open this directory in Unity | Baseline Editor |
|---|---|---|
| `ChopChop` | `ChopChop/UOP1_Project` | 2020.3.17f1 |
| `BossRoom` | `BossRoom` | 6000.0.52f1 |

The baseline checkouts have not been upgraded or integrated with Spire. Open them
with their original Editor or establish an upgrade separately before recording.
The playlist index records the inspected revisions and upstream links.

To obtain the registered revisions after cloning Spire:

```sh
git submodule update --init --recursive --depth 1
git -C examples/ChopChop lfs pull
git -C examples/BossRoom lfs pull
```

Run those commands from the Spire repository root with Git LFS installed. Fetch
LFS content before opening either project so imported assets are real files,
not pointer placeholders. Avoid updating to upstream heads implicitly; the
recording plans refer to the pinned source revisions.

Future demo refactors must retain a reproducible original baseline. Changes
inside a submodule belong to that checkout and are not captured by a commit of
the outer repository alone.
