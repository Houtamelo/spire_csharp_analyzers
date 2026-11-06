# Windows Visual Studio acceptance

Run from a signed-in Windows desktop with a licensed Unity 6000.5.6f1 Editor,
Windows Mono/IL2CPP support, .NET 10 and 8.0.318 SDKs, and Visual Studio Community.
The IDE needs the Unity, managed desktop, and Visual Studio extension development
workloads. IL2CPP additionally needs the C++ build tools and Windows SDK.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\VisualStudioIntegrationTestDriver\Run-Windows.ps1
```

Use `-Editor` and `-VisualStudio` for non-default installations. `-Project` selects
an owned disposable fixture; it must not be a customer project or the tracked
fixture. Paths under the repository's `examples/` directory are rejected,
including with `-Prepared`. `-NoBuild` reuses already-built package, CLI, and driver outputs.
`-Prepared` requires a passing `unity-asmdefs --visual-studio <devenv.exe>` report
and an unfixed fixture. A normal run restores that fixture automatically.

The runner selects Unity's Visual Studio project generator and checks the real
compiler/analyzer references in all eight assemblies. It installs the private
driver into the `SpireValidation` experimental profile, refreshes only that
profile's composition cache, and launches the generated solution. It preserves
the regular Visual Studio profile. Close an existing `SpireValidation` instance
before running. First launch may require dismissing optional IDE setup screens.

The driver requires all of the following inside the actual IDE:

- Eight live `SPIRE002` locations and the fixture's `SPIRE016` diagnostic.
- The real **Replace with SpireEnum<T>.From(value)** suggested action.
- The exact saved source edit, with `SPIRE016` disappearing while `SPIRE002` remains.
- Go To Definition resolving the source-generated `Kind` enum.

Unity then recompiles the edited fixture and runs valid/invalid conversion
probes. This covers one representative IDE action; the existing provider tests
cover the other transformations.

Evidence is written under `<project>/VisualStudioDriver`, including
`ide-results.json`, before/after source hashes, live diagnostic rows, and the IDE
activity log. Unity writes `visual-studio-recompile-results.json` beside it.
An absent report, unavailable action, incorrect edit, or failed Unity probe
fails the command. Unavailable unrelated IDE action providers are recorded;
they never satisfy the required Spire action.
