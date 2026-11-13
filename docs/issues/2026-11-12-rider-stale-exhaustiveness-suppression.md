# Rider retains exhaustiveness warnings after startup or package updates

Status: investigated; Rider invalidation is the leading explanation. The exact
trigger has not been reproduced. No production changes made.

The user reports stale CS8509 in both the editor and Problems, with
solution-wide analysis enabled. Package updates reliably expose the problem;
opening a solution sometimes does. Editing source, restarting Roslyn analyzers,
or reopening Rider clears it. The behavior predates the current Rider
2026.3 build RD-263.7307.258, verified from the installed product metadata.

## Findings

Spire's [CS8509Suppressor](../../src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/CS8509Suppressor.cs)
examines the diagnostics and semantic model supplied by its current callback.
It stores no analysis results between callbacks.
[ExhaustivenessChecker.Check](../../src/Houtamelo.Spire.PatternAnalysis/ExhaustivenessChecker.cs)
creates a fresh domain resolver and type-hierarchy resolver for each check.
The latter's instance cache therefore does not survive successive checks.
There is no identified Spire cache to invalidate here.

Rider produces native compiler-style highlightings and imports suppression
results from its Roslyn worker. JetBrains introduced general suppressor support
in [Rider 2025.2](https://www.jetbrains.com/rider/whatsnew/2025-2/#roslyn-based-suppressors).
Read-only inspection of the installed binaries confirms this path:

| Installed Rider component | Relevant behavior |
| --- | --- |
| `DocumentDiagnosticsService` | Requests Roslyn diagnostics, filters them, and caches the result for an open document. |
| `GuardedDiagnosticsService.IsNotCompilerDiagnostic` | Keeps suppressed compiler diagnostics; filters out unsuppressed compiler diagnostics. |
| `RoslynDaemonProcess.CreateRoslynHighlighting` | Converts `IsSuppressed` diagnostics to `SuppressedRoslynHighlighting`. |
| `RoslynDaemonInvalidator` | Invalidates daemon analysis when diagnostic updates arrive or the Roslyn worker becomes ready. |
| `SWEADiagnosticsService.GetDiagnosticsImpl` | Tries the open-document cache before analyzing the project. |
| `OpenedAnalyzedDocumentCache.TryGetDiagnostics` | Reuses cached diagnostics when document-state checksum and analyzer instances match. This check does not include the project's semantic state or generated output. |

The open-document cache is a concrete invalidation suspect. References or
generated code can change while a consumer document and its analyzer instances
remain unchanged. The solution-wide path can then reuse results from an older
semantic state. Editing the document changes its checksum; restarting the
worker discards its caches. These mechanisms fit the reported recovery actions.
However, this is a static-inspection hypothesis: no failing editor session was
captured, and the cache path has not been proven to cause the editor underline.
The separate project-analysis cache does include project state and a diagnostic
checksum; the identified concern is the earlier open-document-cache shortcut.

Binary evidence comes from `lib/ReSharperHost/JetBrains.Roslyn.Worker.exe` and
`JetBrains.Roslyn.Host.dll` under the installed Rider directory. Inspection used
the existing `ilspycmd` with `DOTNET_ROLL_FORWARD=Major`; no IDE restart,
configuration change, or host-display interaction was performed.

## Separate loading failure

Follow-up: the [dependency-reload investigation](2026-11-12-rider-analyzer-dependency-reload.md)
reproduced a Rider loader defect that removes registered dependency paths on
reload. This establishes the failure mechanism independently of the stale
editor-warning hypothesis below; the exact historical editor impact remains
unverified.

Four existing worker logs for `Houtamelo.Spire.Functional` contain repeated
`ProvidersLoader` warnings that `Houtamelo.Spire.PatternAnalysis, Version=1.0.0.0`
could not be loaded. For example:

`~/.cache/JetBrains/Rider2026.3/log/RoslynWorker/Houtamelo.Spire.Functional/backend.2026-11-12_0504.15006.log:99`

The stack is provider discovery through `AnalyzerFileReference.GetAssembly()`
and enumeration of `DefinedTypes`. This proves a loading failure during
discovery; it does not prove the exhaustiveness suppressor was absent or threw.
The later `backend.2026-11-12_1203.308912.log` contains no such warning.

Current package project declarations include PatternAnalysis alongside the
analyzer DLL, current build outputs contain it, and the Functional project
explicitly references it as an analyzer asset. A missing declaration was not
found. A package update or startup loading race remains possible, but is not
established by these logs. Do not conflate this with the cache hypothesis or
claim it explains every historical occurrence.

## What Spire can do

The [DiagnosticSuppressor API](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/DiagnosticAnalyzer/DiagnosticSuppressor.cs)
lets Spire suppress diagnostics supplied by the host. It provides no supported
operation for invalidating Rider's editor analysis or requesting another pass.
Changing suppression criteria would not fix a host that skips a fresh pass or
fails to apply its results. No suppressor-only fix is justified by this evidence.

The next useful experiment is a minimal package consumer with an exhaustive
`[EnforceExhaustiveness]` hierarchy and a generated-union control. Observe cold
startup and a package update without editing source. Compare solution-wide
analysis enabled/disabled, and capture Roslyn/daemon traces before clearing the
warning. A private instrumented suppressor can distinguish: callback not invoked,
CS8509 not supplied, checker indeterminate, or suppression reported but ignored.
Keep a genuinely incomplete switch as a control.

If dependency loading is independently reproduced, test delivering the
PatternAnalysis implementation inside the analyzer assembly. That could remove
one loader boundary, but is an unvalidated packaging change and would not itself
repair Rider's diagnostic invalidation. Preserve the existing suppression
semantics while investigating.

An upstream report should include the exact Rider build, package versions,
solution-wide-analysis setting, unchanged source before/after restarting
analyzers, clean compiler output, and paired worker/daemon traces. Name the
open-document-cache shortcut as a suspect, not a confirmed cause. No upstream
message was sent.

The previously investigated [invalid-arm case](2026-11-11-rider-cs8509-invalid-arm.md)
is distinct: Roslyn can omit CS8509 entirely when an arm has an invocation error.

Validation attempt: `dev-tools.dotnet_test` for the source-generator project,
filter `FullyQualifiedName~Exhaustiveness`, stopped before test execution with
NETSDK1005. Its existing `obj/project.assets.json` contains only `netstandard2.1`
from the Unity test-host restore, while the normal test target is `net10.0`.
No test result or live-editor reproduction is claimed; restore assets were not
repaired as part of this investigation.
