# Unity payload stability

Historical design and validation. The owner-token implementation below is
superseded by the [one-reference redesign](2026-10-10-unity-serialization-footprint.md).
Its recorded test results describe the October 9 implementation.

Approved implementation scope, 2026-10-08. The existing active per-variant
`SerializeReference` payload remains the representation for all five layouts.
The runtime layout and public factory/property behavior remain intact.

- `Variant.FormerlySerializedAs` accepts one previous variant name. Generate the
  old-name payload class with the current typed fields. Restore current and old
  payload classes through the current factory; prepare the current payload class
  on the next serialization. The alias affects Unity serialization only.
- Infer the variant from the concrete payload type. Exclude the runtime `_kind`
  field from serialization. Valid assets written by the current implementation
  remain readable because their payload types already identify the variants.
- Null, missing, or unrecognized payloads warn:
  `invalid union serialization data, defaulting to {FirstVariantName}` and
  construct the first variant with default field values. Inspector refresh must
  not repeatedly report the same invalid data. The public generated
  `__SpireFromSerialized(object)` helper performs pure reconstruction/fallback;
  the warning belongs to the native `OnAfterDeserialize` path.
- Prepare nested snapshots into the previous destination payload and owner
  together. Preserve independent runtime copies and sibling slots; do not disable
  ownership checks. The mismatch is confirmed by native probes in
  `docs/unity-managed-reference-stability.md`.
- Retain and reuse caches in Editor, including Play Mode. In player builds,
  release payload and ownership references after runtime reconstruction. Repeated
  explicit nested hydration must preserve already reconstructed values. A new
  native deserialization of invalid data must still use the fallback.
- Keep runtime `Instantiate` serialization operational. Its source can retain
  a reusable payload; deserialized clones use the player cleanup policy.

`SPIRE_DU017` is an error for malformed or ambiguous Unity variant aliases.
Null/empty aliases mean unset, and an alias equal to its own variant name is a
no-op. Reject aliases that identify another current variant or another alias,
and ambiguous sanitized generated payload names. `SPIRE_DU015` continues to cover
conflicts with user-declared generated members. Field serializability remains
Unity's responsibility; do not restore `SPIRE_DU014`.

Verification must cover semantic metadata and compilation, real persisted Unity
assets, former-name-to-current-name migration, stable nested YAML IDs on unchanged saves,
copy and collection isolation, native warnings, Editor drawers, and cleanup and
cloning in standalone Mono and IL2CPP players. GC tests must not retain probe
objects accidentally. Update public documentation and the installable package
after these checks. No version bump, commit, or publication was requested.

## Validation

Completed 2026-10-09 on Unity 6000.5.6f1, Linux x64. Editor and player launches
used Xvfb.

| Environment | Result |
|---|---|
| .NET 10 solution tests | 2,085 passed; no failures or skips |
| Full Unity Editor Mono harness | 2,109 passed; exact expected case identities and all original behavioral cases executed |
| Native Editor integration | 96 passed; includes all-layout persisted alias migration, canonical YAML/RID stability, rendered drawers, Undo/Redo and lifecycle checks |
| Standalone Mono player | Exit 0; values, aliases, nested/generic data, clones, cache clearing and source-cache reuse verified |
| Standalone IL2CPP player, High stripping | Exit 0; same checks, including opaque former-name payloads that cannot canonicalize during scene construction |

The player lifetime probe captures a real generated payload, invokes its valid
`OnAfterDeserialize` callback, returns before GC, and verifies restored values,
cleared payload/owner fields, and weak-reference collection. Native scene loads
and `Instantiate` separately verify cleared caches and correct runtime values.
This does not measure Unity's internal native deserialization reference registry.

Two validation boundaries remain explicit:

- Unity expands fresh null inline helper values into initialized objects. YAML
  stability uses a saved/reloaded baseline; no replacement helper constructors
  are invoked by Spire to imitate that normalization.
- Repeated fixture edits exposed stale Unity binary player artifacts. Final
  Mono and IL2CPP runs used fresh disposable projects; the fixture now requests
  `BuildOptions.CleanBuildCache` when building players.

Results are retained under `tmp/il2cpp-validation/` as
`payload-stability-editor-final.json`, `payload-stability-mono-clean-player.json`
and `payload-stability-il2cpp-clean-player.json`. The full Unity harness report is
`tmp/unity-compiler-tests/harness-results.xml`; .NET results are in each project's
`TestResults/payload-stability-suite.trx`.

The installable package is rebuilt at
`tmp/unity-package/com.houtamelo.spire`. An independent source review found no
remaining serious correctness regression. Work remains uncommitted on
`unity-6000`.
