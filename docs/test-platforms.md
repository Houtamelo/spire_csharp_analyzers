# Platform-specific test coverage

## CoreCLR and Unity suites

Run the portable CoreCLR/NuGet suite from the repository root:

```sh
bash tools/test-coreclr.sh
```

`tests/Spire.CoreCLR.slnx` retains the original analyzer, generator,
PatternAnalysis, runtime behavioral, Functional, and MSBuild-configuration projects. It also
tests installed local NuGet packages through complete and failing consumer
builds. This suite needs the .NET 10 SDK, Bash, Git, and `unzip`, with no Unity
installation. CI runs it
on pushes and pull requests, and the NuGet publishing workflow requires it.
See [NuGet publishing](nuget-release.md) for Trusted Publishing setup and retries
that preserve an existing release tag.

The Functional tests exercise the actual result, option, and collection APIs,
including error propagation and exception stack traces. Packed consumers install
only `Houtamelo.Spire.Functional` and verify its transitive Spire dependency,
generated union APIs, and compiler diagnostics.

Tests that load installed Unity reference assemblies carry
`[Trait("Environment", "Unity")]`; mixed tests keep their portable controls
separate. The CoreCLR entry point excludes only that trait. To verify that no
Unity installation is consulted, set `SPIRE_UNITY_EDITOR` to a nonexistent path.

Run the Unity-reference generator suite with an installed Editor:

```sh
SPIRE_UNITY_EDITOR=/path/to/Editor/Unity bash tools/test-unity-metadata.sh
```

These tests compile generated callbacks and drawers against real Unity
assemblies. They run in CoreCLR and do not replace the native Unity suites:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-harness
dotnet run -c Release --project tools/Spire.Tool -- unity-union-core
dotnet run -c Release --project tools/Spire.Tool -- unity-behavioral --backend IL2CPP
```

The harness runs the shared compiler/analyzer and behavioral tests inside
Editor Mono and verifies exact discovery/execution identities. The native
union and player fixtures cover persistence, Inspector behavior, and runtime
backends; see [Unity validation](unity.md). Keep both CoreCLR and native Unity
gates when changing shared runtime or generation behavior. Benchmark correctness
remains in the full solution and requires the .NET 11 preview compiler for its
native C# union comparison types.

The [suite isolation verification](reviews/2026-11-07-coreclr-unity-suite-isolation.md)
records the current environment regressions, package consumers, and host results.

Guard-flow tests include generated consumers that execute bounded inputs and
compare each payload read with its actual discriminator. They run in both the
portable suite and the Editor Mono harness. See the
[model invariants and validation boundaries](guard-flow.md).
C# 11 ref-field regressions instead compile and run installed NuGet consumers
in CoreCLR: Unity's target runtime does not support ref fields. Portable
ref-return, pointer, and alias regressions remain in the shared suite.

Measure compiler-analysis scaling separately from xUnit timing:

```sh
dotnet run -c Release --project tools/Spire.GuardFlow.Performance -- \
  --output tmp/guard-flow-performance.json
```

The [runner](../tools/Spire.GuardFlow.Performance/README.md) records raw timings,
allocation deltas, diagnostic identities, and cancellation observations. The
CoreCLR workflow runs its shorter `--quick` profile and retains the JSON with
GitHub's [artifact action](https://github.com/actions/upload-artifact).

## Operating systems

Use `LinuxFact` or `WindowsFact` in `DevTools.Tests` when a test depends on that
operating system's facilities. Ordinary `Fact` and `Theory` cases remain required
on every host. A platform attribute declares a requirement; it must not be used
to hide a product failure on a supported platform.

Linux process-group tests retain their `setsid`/signal and inherited-pipe
coverage. Xvfb tests retain real X11 server setup, callback, cancellation, and
cleanup checks. Windows process tests use real child processes, readiness/PID
files, and redirected streams to cover deadlines and tree cleanup. Windows
orphan-pipe tests deliberately use non-owned descendants and clean up their
published PIDs; they do not claim Linux process-group semantics on Windows.
Timeout cleanup includes closing redirected readers in its bounded wait because
Windows Mono can block closure behind an inherited pipe read. A non-owned
descendant may keep background cleanup pending until it closes the pipe.

## Discovery contract

The normal xUnit runner reports an incompatible platform fact as skipped, with
a canonical reason such as `Requires Linux; current platform is Windows.`
The independent xUnit exporter retains those case identities in
`platformExclusions`; the Unity bridge independently selects applicable cases
and records the same exclusions.

Both discovery manifests contain:

- `schemaVersion`: `2`.
- `platform`: the actual runtime platform.
- `cases`: every applicable case, with its stable identity and display name.
- `platformExclusions`: each incompatible case's identity, required platform,
  and reason.

The Unity harness requires exact agreement on applicable identities and on
each exclusion's identity, platform, and reason. Excluded and executed identities
must be disjoint, and every applicable discovered case must execute. Ordinary
`Fact(Skip = ...)`, altered platform skip reasons, missing cases, and unexpected
ignored NUnit tests still fail validation.

Keep test inputs under `tests/` and owned temporary outputs under `tmp/`.
The interactive `examples/` tree is not a source of test data. See the
[fixture isolation verification](reviews/2026-11-05-test-fixture-isolation.md).

The [Windows verification](reviews/2026-11-05-windows-platform-tests.md) records
150 passing Windows .NET development-tool cases, 2,671 passing Editor Mono
checks, and the matching 12 Linux exclusions. Linux retains 2,704 passing cases
and explicitly excludes the eight Windows process cases.
