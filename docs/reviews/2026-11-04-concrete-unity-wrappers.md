# Concrete Unity serialization wrappers

The [November 5 legacy-Editor recheck](2026-11-05-unity-2022-2023-current-validation.md)
passes all 2,689 current Editor cases on Unity 2022 and 2023 and both Unity 2023
native players. Unity 2022's native generic serialization boundary remains.

Non-generic unions now persist correctly in Unity `2022.3.62f3`. The same
wrapper generation path is used on every Unity version. Generic unions keep
existing newer-Editor support but remain unsupported by Unity 2022's native
serializer. The package version and published minimum remain unchanged.

This follows the [original Unity 2022 failure](../issues/2026-11-04-unity-2022-generic-managed-reference-payloads.md)
and [LTS validation](2026-11-04-unity-lts-followup.md). All results below are Linux x64.

## Representation and migration

Each canonical and former-name variant receives an internal sealed class such as:

```cs
[Serializable, Preserve]
internal sealed class MoveSerializedPayload
    : SpireSerializedPayload<CommandTypes.Variants.Move>
{
    [Preserve]
    public MoveSerializedPayload() {}
}
```

These classes live under the existing `CommandTypes.SerializedPayloads` container.
They inherit the single public `value` field. The shared generic base keeps its
existing name and fields and is now unsealed, allowing old assets to deserialize.
No extra union instance field or factory-time cache allocation is introduced.

Preparation and current-state checks require the canonical concrete subclass;
restoration and field reuse still accept the generic base. This upgrades old
caches, including unchanged caches nested in arrays/lists, while reusing
unchanged current caches. The drawer identifies variants through the inherited
`value` field's type rather than the wrapper's own generic arguments.
Existing parser restrictions, including `SPIRE_DU018` for generic containing
types, are unchanged. There is no Unity-version branch or new generic rejection.

An actual asset was written using the previous package in Unity `6000.5.6f1`,
archived, opened with the new package, and saved without changing logical values.
It changed from the old generic typename to the canonical concrete typename.
Fresh-process reads passed in both `6000.5.6f1` and `2022.3.62f3`, including
no-op-save YAML stability. Existing assets must undergo this resave in a newer
supported Editor before moving them to Unity 2022; that Editor cannot load the
old generic managed-reference objects itself.

## Native Editor results

| Gate | 2022.3.62f3 | 2023.2.22f1 | 6000.5.6f1 |
| --- | --- | --- | --- |
| New non-generic fixture Write | 67 passed | 67 passed | 67 passed |
| New fixture fresh-process Read | 44 passed | 44 passed | 44 passed |
| Broader fixture Write | Same-process 126 pass; generic payload rejected on disk | 126 passed | 126 passed |
| Broader fresh-process Read | Expected failure: generic fields did not restore | 87 passed | 87 passed |
| Broader Recovery / Accessors | Not accepted on 2022 | 59 / 9 passed | 59 / 9 passed |
| Migrated legacy asset, fresh-process verification | 12 passed | Not run | 12 passed |

The new fixture tests singles, fieldless variants, strings, arrays/lists, nested
non-generic unions, Unity asset references, shared/cyclic managed graphs, inherited
SerializedProperty paths, former-name canonicalization, and stable YAML/IDs.
Real generated drawer fallback is invoked with actual canonical and former
managed-reference values and an invalid discriminator. These are headless
SerializedObject checks; no Inspector window or native input is involved.
The broader modern-Editor fixture retains all its generic cases.

Unity 2022 still logs rejection of
`SerializedGenericTypes1.SerializedPayloads.ValueSerializedPayload<System.Int32>`.
Its saved generic cache is null, and the fresh-process Read fails at check 19:
`Generic union fields did not restore.` The passing same-process Write is not
evidence of generic persistence. The dedicated non-generic acceptance fixture
passes independently without removing those failing generic assertions.

## Managed regression results

All 577 distinct checks in the selected partitions pass. The partition totals
below overlap by test name; `managed-acceptance-proof.json` deduplicates them.

| Partition | Passed |
| --- | --- |
| Unity generator/metadata, including nine new wrapper tests | 60 |
| Snapshot selection | 102 |
| Generator compile/emit/load behavioral tests | 218 |
| Readable factories/grouped types/private payloads/field collisions/kind | 107 |
| Compatibility selection, including all Q09 exact-output checks | 4 |
| Separate compiled behavioral project | 100 |

The new tests were first run against the old implementation and failed before
implementation. They cover wrapper symbols/constructors/attributes, cache reuse
and replacement, legacy and former wrappers, invalid-payload fallback, nested
migration, escaped/private/namespaced declarations, generic constraints, and
version-independent generation. Only intentional Unity Q09 output bytes and
their hashes were refreshed; the manifest records the approved change.

The initial unpartitioned full generator run was stopped by the RSS watchdog.
The November 5 retry below completes that project. C# formatting and scoped
`git diff --check` also pass.

## Initial player resource limit

The initial Mono and IL2CPP High-stripping builds on `6000.5.6f1` both stopped in Unity's
CIL linker with `System.OutOfMemoryException`. The subprocesses inherited the
512 MiB managed-heap cap. This attempt produced no player-runtime result.

Those initial heavy operations ran sequentially on CPU affinity `22,23`, quota 200%, with
two Unity job workers, a 3 GiB cgroup maximum, swap disabled, and an independent
2 GiB aggregate RSS watchdog. The failed player build peaked just below the
watchdog threshold.

## November 5 revalidation with 12 GiB

The user authorized a 12 GiB total allowance. The retry removes the blanket
per-process managed-heap cap and sets `MemoryMax=12G`, `MemoryHigh=10G`, swap
disabled, and a 12 GiB aggregate RSS watchdog. CPU affinity `22,23`, quota 200%,
two Unity job workers, and sequential heavy operations remain in force.

The same frozen package and unchanged test binary were used. Both Linux x64
player builds on `6000.5.6f1` succeed with High stripping and zero build errors.
Both executables were then launched independently and their reports verified:

| Gate | Result | Sampled peak aggregate RSS |
| --- | --- | --- |
| Mono + IL2CPP builds, sequential | Both passed | 2.83 GiB |
| Actual Mono player | 60 checks passed, exit 0 | 53 MiB |
| Actual IL2CPP player | 60 checks passed, exit 0 | 80 MiB |
| Complete .NET source-generator test project, unfiltered | 1,051 passed, zero failed/skipped | 2.70 GiB |

The player reports identify their actual backend, `6000.5.6f1`, 64-bit execution,
and non-development builds. They cover runtime unions and persisted serialized
payloads, including generic reconstruction, aliases, nested collections, managed
graphs, Unity object identity, and direct invalid-payload recovery. A malformed
asset may be canonicalized during build, so its native malformed-load callback
is not claimed solely from the passing runtime report.

The full generator project runs from the unchanged Release binary with
`--no-build --no-restore` and no test filter. TRX confirms all 1,051 cases
executed and passed. No retry was stopped by the memory watchdog.
Rendered Inspector interaction and legacy-version Player acceptance remain
separate gates; the package minimum is unchanged.

Retry evidence: `tmp/unity-concrete-wrappers-12g-20261105/` contains
`acceptance-summary.json`, `build-report.json`, `player-results.json`, both
`*-player-report.json` files, the full-project TRX, resource records, and the
12 GiB launcher. `previous-512m-build-report.json` preserves the original failure.

## Artifacts

Evidence root: `tmp/unity-concrete-wrappers-20261104/`.

- `new-package-sha256.json`: the four DLL hashes used by every native project;
  they match the final Release source-build DLLs.
- `{2022.3.62f3,2023.2.22f1,6000.5.6f1}/native/`: actual assets, baseline YAML,
  Unity logs, phase reports, and runner records.
- `legacy-before-upgrade/`: old-package asset/YAML archive and SHA-256 inventory.
- `6000.5.6f1/legacy-migration/`: old write, upgrade, and fresh verification reports.
- `managed-acceptance-proof.json` and `*-results/*.trx`: distinct managed outcomes.
- `q09-actual/`: captured Unity output bytes before baseline refresh.
- `*.resources.json`: commands, exit codes, CPU/memory limits, measured RSS, and
  watchdog outcomes, including the stopped full run and failed player build.

The tested analyzer SHA-256 is
`3b9f7e2fbf523646dde39234d9287eb6ee43878088ab1fd8db0362ab564eeec1`.
