# Rider loses analyzer dependency registrations on reload

Status: reproduced in the installed Rider 2026.3 loader, build
RD-263.7307.258. No package or production-source change made.

The [YouTrack submission draft](2026-11-12-rider-analyzer-dependency-youtrack-report.md)
is self-contained and has an accompanying local reproduction archive.

Rider can report that `Houtamelo.Spire.PatternAnalysis` is missing even when
the DLL is present and was registered with its analyzer loader. Reloading a
changed dependency removes its registered path. A subsequent analyzer reload
into a fresh load context then cannot resolve that dependency.

## Evidence

Existing Functional-project worker logs contain eight warning batches across
four sessions on November 11-12. Each batch reports the same failure during
`reloadAnalyzerReference`, followed by discovery of code fixes, refactorings,
and completion providers. Those three scans account for repeated warnings
within each batch; they are not three independent missing files.

One example is
`~/.cache/JetBrains/Rider2026.3/log/RoslynWorker/Houtamelo.Spire.Functional/backend.2026-11-12_0504.15006.log:99`.
Its stack reaches `AnalyzerReferencesHandler.ReloadAnalyzerReference` through
`ProvidersLoader.CreateExtensions`. The matching backend is
`backend.2026-11-12_0504.14252.log`; the worker records parent PID 14252.
These are local project-reference sessions using rebuildable analyzer outputs,
not evidence of a consumer NuGet archive missing a file.

Current project declarations include the dependency. Both existing local
`6.0.0` package archives, `Houtamelo.Spire` and `Houtamelo.Spire.Analyzers`,
also contain it under `analyzers/dotnet/cs`. Those archives were inspected,
not rebuilt for this investigation; their version is not a proposed release.

## Root cause

Read-only inspection and execution of the installed Rider implementation found:

1. `AnalyzerReferencesTracker.Add` calls `AddDependencyLocation` before creating
   an `AnalyzerFileReference`.
2. `MainAssemblyLoader` delegates user DLL loading to
   `RiderShadowCopyRiderAnalyzerAssemblyLoader`.
3. Its base `RiderAnalyzerAssemblyLoader.LoadFromPathUnchecked` notices when a
   cached file's module version ID (MVID) changes. It invalidates cached assembly
   entries **and removes that path from `myKnownAssemblyPathsBySimpleName`**.
   This happens after the host has registered the dependency path for reload.
4. The changed helper loads directly by path, but its dependency registration
   remains absent. A first analyzer reload can still work if its load context
   already contains the helper.
5. A later analyzer reload creates a fresh context. `RelatedAssemblyResolve`
   consults `GetPaths`, finds no registered helper, and returns null. Enumerating
   `Assembly.DefinedTypes` throws `ReflectionTypeLoadException` with the same
   PatternAnalysis `FileNotFoundException` seen in the logs.

Re-registering the helper path after its reload restores discovery on the next
analyzer reload. The file stays present throughout the reproduction. The two
builds use the normal unchanged assembly identities; their different MVIDs
represent ordinary rebuilds. Increasing package versions is not the required
repair for this lost registration.

## Reproduction and checks

The local fixture is `tmp/rider-patternanalysis-loading/`. It contains an
independently authored reflection probe, captured Debug/Release Spire DLLs,
their hashes/MVIDs in each result, and a replay script. It calls the installed
`MainAssemblyLoader`; it does not copy or simulate Rider's loader code.

The probe ran under Rider's bundled .NET 10.0.14 runtime. It uses private temp
and runtime directories, leaving live Rider processes and their shadow-copy
directories alone. No UI or IDE-setting change was needed.

| Scenario | Verified result |
| --- | --- |
| Cold load, helper registered | Discovery succeeds. |
| Helper not registered, then registered | The exact missing-helper error appears, then discovery recovers. |
| Helper rebuilt/reloaded, analyzer rebuilt twice | Helper registry goes from one path to zero; discovery eventually fails despite the DLL being present. |
| Same reload sequence, then helper re-registered | Registry returns to one path; discovery succeeds again. |

All four scenarios verified their expected success/failure transitions in
separate processes. Each writes `runs/<scenario>-<id>/results.json` and exits
nonzero if an expected transition is absent. The probe built with zero
warnings/errors and was formatted with `dotnet format whitespace --include`.

Replay from the repository root:

```bash
bash tmp/rider-patternanalysis-loading/run-repro.sh \
  /home/houtamelo/.local/share/JetBrains/Toolbox/apps/rider/lib/ReSharperHost
```

`tmp/rider-patternanalysis-loading/verification.log` records the final replay.
The fixture is a local ignored artifact, not part of the normal test suite.
Its checks reproduce provider discovery; they do not prove which diagnostics
the live editor retained or whether a particular suppressor callback failed.

## Repair boundary

The direct fix belongs in Rider: invalidating a loaded assembly must preserve
or restore its registered dependency location for subsequent load contexts.
The reproduction supports an upstream report with a precise failing operation;
no external report was sent.

A Spire-side mitigation would be to compile the PatternAnalysis implementation
into the analyzer assembly, removing this private assembly-resolution boundary.
That affects package, Unity-delivery, and test-assembly layout and should be
handled as a separate packaging change with its own acceptance checks.
The current investigation found no missing package entry to repair, and no
reason to change exhaustiveness rules or suppress warnings globally.

This confirms a separate loader defect. Its applicability to the user's
[stale editor warning](2026-11-12-rider-stale-exhaustiveness-suppression.md)
after installing a new NuGet version remains unproven: changing a package's
versioned directory is a different event from overwriting local build outputs.
