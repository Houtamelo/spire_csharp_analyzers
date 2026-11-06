# Linux-specific development-tool fixtures on Windows

Resolved on November 5. The final Windows .NET development-tool run passed
150 cases, and the complete Unity Editor Mono harness passed 2,671 checks.
Twelve Linux-only cases are explicitly platform-inapplicable; eight new Windows
process cases and the portable shared child-cleanup case execute on Windows.
The independent SDK and Unity manifests agree on every applicable identity and
every exclusion. See the [verification report](../reviews/2026-11-05-windows-platform-tests.md).

## Original failure

The Windows Unity 6000.5.6f1 Editor Mono run executed all 2,644 discovered cases:
2,631 passed and 13 failed. Exact .NET/Unity case identities matched, and all
discovered cases and original behavioral facts executed. The command correctly
returned failure; these cases have not been silently excluded or marked passing.

The post-AHCI repeat executed 2,645 cases: 2,631 passed and 14 failed. The
additional failure was a packaging test's outdated assertion that Rider was
the only verified IDE. After updating that assertion for the verified Visual
Studio workflow, the exact package-export case passed in both Windows .NET and
Unity Editor Mono. At that stage the 13 platform-specific failures below
remained; the complete suite had not been rerun after the focused correction.

The 13 failures belong to development-tool process/display fixtures:

| Fixture | Cases | Windows failure |
|---|---:|---|
| `ProcessRunnerCorrectnessTests` | 8 | Explicitly requires Linux process descendants/process groups |
| `VirtualDisplayTests` | 4 | Launches `Xvfb` and inspects an X11 server |
| `UnityHarnessRunnerTests.RunUnity_TimeoutKillsTheRealChildProcessBeforeRethrowing` | 1 | Launches `/bin/sh` and `sleep` |

The resolution retains Linux process-group/X11 coverage and adds real Windows
process-tree/pipe tests. Platform selection is explicit in independent xUnit
discovery and the Unity bridge, with canonical reasons and exact exclusion
identity checks. Ordinary skipped facts still fail validation. The new Windows
Mono cases also exposed a synchronous reader-disposal block; timeout cleanup
now includes reader closure in its bounded wait.

Package analyzer, generator, pattern-analysis, global-configuration, behavioral,
and Unity compiler-probe cases in this run passed. Player runtime, native
serialization, rendered Inspector, and IDE results are separate acceptance gates.

The local evidence is under
`~/.local/share/spire-windows/virtualbox/results/secondary/full-editor-harness/`.
The repeat and focused correction are under `results/sata-validation/` and
`results/sata-validation/packaging-readme/`.
The guest source is an owned working-tree snapshot, not a clean release commit.
