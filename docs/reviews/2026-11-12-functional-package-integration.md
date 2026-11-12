# Functional package integration

`Houtamelo.Spire.Functional` follows the shared four-package release version,
prepared as `6.0.0` after the initial `5.0.0` integration checks below. It is built and tested by both solutions, packed by the local
release dry run and publishing workflow, and bundled with the Unity distribution.

The NuGet package targets .NET Standard 2.0 and contains its DLL, XML API docs,
and README. Its only declared package dependency is `Houtamelo.Spire`.
JetBrains.Annotations and PolySharp remain build-only dependencies; PE metadata
checks confirm no JetBrains or Unity assembly reference in the Functional DLL.
The Unity bundle contains two runtime DLLs, three compiler DLLs, both runtime XML
files, and an offline Functional guide. Runtime import settings and deterministic
GUIDs are preserved in UPM and `.unitypackage` staging.

Validation on Linux, 2026-11-12:

- .NET SDK `10.0.401`: the complete portable suite passed 2,762 tests, including
  Functional behavior and installed NuGet consumer tests.
- Portable DevTools: 194 passed, eight declared Windows-only cases excluded.
- Real workflow-step fixtures packed all four current projects and all three
  projects from the older release layout. Version mismatch and missing-version
  fixtures failed before publication; failing portable tests prevented packing.
- Functional-only NuGet consumers compiled and ran. Incomplete result matching
  failed with SPIRE009; assigning inspection's `Void` to `Result` failed with
  CS0029.
- Unity `6000.5.6f1`, Editor Mono: a fresh archive export/import passed all six
  acceptance groups, including source compilation and execution using Functional
  results/options, plugin settings, generated drawers, and native JSON persistence.
  The negative consumer failed with SPIRE014.
- Archive inspection checked all five DLLs against the actual Release outputs,
  their stable GUIDs and plugin roles, both XML files, and the Functional guide.

Local artifacts are under `tmp/functional-integration/`: `nupkgs/`, `upm/`,
`Spire-5.0.0.unitypackage`, `artifact-report.json`, and the fresh import's
`unitypackage-verify.json`. Player builds and other Unity versions were not
retested for Functional in this change.

These integration checks preceded publication; they triggered no remote workflow
or package publication. Before the first public
Functional release, the NuGet Trusted Publishing policy must include its package
ID and permit new-package publication; the account-side policy was not inspected
or modified. See [NuGet release setup](../nuget-release.md).
