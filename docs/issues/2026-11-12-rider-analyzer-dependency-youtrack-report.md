# Analyzer DLL reload removes dependency registration, causing FileNotFoundException during provider discovery

Reloading an analyzer dependency can remove its registered resolution path.
Subsequent analyzer discovery then fails with `FileNotFoundException`, although
the dependency DLL is still present. The attached probe reproduces this using
the installed Rider loader and recovers when the dependency is re-registered.

## Environment

- Rider 2026.3, build **RD-263.7307.258**.
- openSUSE Tumbleweed 20261102, Linux x86_64.
- Probe uses Rider's bundled .NET runtime **10.0.14** and SDK **10.0.403**.
- Analyzer: `Houtamelo.Spire.Analyzers`; private dependency:
  `Houtamelo.Spire.PatternAnalysis`.
- The attachment provides two builds with identical assembly identities but
  different module version IDs (MVIDs). It checks both conditions before running.

## Reproduction

Extract `rider-analyzer-dependency-reload.zip`, then run from its extracted
`rider-analyzer-reload-repro` directory:

```bash
bash run-repro.sh /path/to/Rider/lib/ReSharperHost
```

The script targets the affected Linux x64 installation layout. It builds a
small console probe with no NuGet package dependencies and uses the installed
`JetBrains.Roslyn.Worker.exe` through reflection. No IDE launch or Spire source
checkout is needed. The probe uses Rider's actual `MainAssemblyLoader`, not a
reimplementation, with private temporary/shadow-copy directories.

The failing scenario performs these operations at fixed input paths:

1. Register analyzer A and dependency A with `AddDependencyLocation`. Load the
   analyzer and enumerate `Assembly.DefinedTypes`: succeeds.
2. Overwrite the dependency with build B. Register its path again and load it.
3. Observe that the dependency's registered path count changes from **1 to 0**.
4. Overwrite the analyzer with build B, register it, load it, and enumerate its
   types: succeeds because this context already contains the dependency.
5. Overwrite the analyzer with build A again, register it, load it, and enumerate
   its types: fails in the fresh load context.
6. Re-register the dependency, reload analyzer B, and enumerate its types:
   succeeds again.

The dependency file remains present throughout; the probe checks this before
each discovery attempt.

## Expected result

A dependency location registered with the loader remains available after its
assembly is reloaded, so subsequent analyzer load contexts can resolve it.

## Actual result

The helper's registered path disappears after reload. Later type discovery
throws `ReflectionTypeLoadException` with this loader exception:

```text
Could not load file or assembly 'Houtamelo.Spire.PatternAnalysis, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'. The system cannot find the file specified.
```

The probe verifies four scenarios in separate processes:

| Scenario | Result |
| --- | --- |
| Cold load with dependency registered | Discovery succeeds. |
| Dependency registered after an initial failure | Discovery recovers. |
| Dependency reload followed by repeated analyzer reloads | Registration disappears; discovery fails. |
| Same sequence followed by dependency re-registration | Registration returns; discovery recovers. |

Exit code 0 means all expected transitions, including the reproduced failure,
were observed. Results include assembly identities, MVIDs, SHA-256 hashes,
load-context IDs, dependency paths, and discovery outcomes.

## Investigation

In this build, `AnalyzerReferencesTracker.Add` registers a dependency location
before creating the `AnalyzerFileReference`. During reload,
`RiderAnalyzerAssemblyLoader.LoadFromPathUnchecked` detects a changed MVID and
invalidates its assembly cache. It also removes the file's path from
`myKnownAssemblyPathsBySimpleName` after registration has occurred.

Loading the changed dependency directly succeeds, but its resolution path
remains absent. Later, `RiderDefaultAnalyzerAssemblyLoader.RelatedAssemblyResolve`
calls `GetPaths` in a fresh context and finds no candidate. Re-registering the
dependency after reload restores loading.

Please preserve or restore dependency locations when invalidating cached
assemblies, or otherwise ensure subsequent contexts can resolve these files.

The corresponding live Rider warnings occur during `reloadAnalyzerReference`,
followed by `ProvidersLoader.CreateExtensions` while scanning code fixes,
refactorings, and completion providers. A short original worker-log excerpt is
attached. It is explicitly an excerpt, not a full IDE log bundle.

The deterministic reproduction verifies loader/provider-discovery behavior.
It does not automate the IDE UI or establish whether this also causes stale
compiler-warning suppression in the editor.

## Attachment contents

- Probe source, project, and replay script.
- Two captured Spire builds and their MIT license; no Rider binaries or PDBs.
- Observed console output and JSON results from all four scenarios.
- A worker-log excerpt and environment/binary fingerprints.
- `SHA256SUMS` for the supplied files.

Personal filesystem prefixes in the supplied text evidence are replaced with
`<REPRO_DIR>`, `<RIDER_HOST>`, or `<USER_HOME>`.
