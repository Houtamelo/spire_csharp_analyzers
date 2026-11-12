# Rider CS8509 with an invalid switch arm

Status: accepted IDE limitation. No production change or warning-disable
workaround is being adopted.

An exhaustive generated `Result<T>` switch can show Rider's CS8509 inspection
while an arm calls `Func<Exception, TMap>` without its required argument.
Roslyn 4.10 and the installed .NET 11 preview compiler report only CS7036 for
that method. Replacing the call with `default`, or capturing the Err payload and
passing it, lets Roslyn emit CS8509, which Spire suppresses normally.

Spire's `CS8509Suppressor.ReportSuppressions` only receives existing diagnostics.
Read-only inspection of installed Rider 2026.3 (263.7307.258) shows that its
`DocumentDiagnosticsService` forwards suppressed Roslyn compiler diagnostics;
`RoslynDaemonProcess` converts them to suppressed editor highlightings. When
Roslyn emits no CS8509, that path has no suppression result to forward. This
explains a likely integration gap; the exact editor path remains unverified.
[Rider has supported Roslyn suppressors since 2025.2](https://www.jetbrains.com/rider/whatsnew/2025-2/#roslyn-based-suppressors).

The callback must still receive its argument:

```csharp
{ IsErr: true, err: var (error) } => f_else(error),
```

Evidence:

- `InvalidArmSuppressorIntegrationTests.cs` runs the real generator, analyzer,
  and suppressor. Five tests cover the invalid invocation, `default`, valid
  call, and scoped-pragmas with invocation errors and genuinely missing cases.
  The full source-generator exhaustiveness filter passed 165 tests.
- `tmp/cs8509-rider/fixture/` contains isolated complete, invalid, pragma-scoped,
  missing-variant, and unrelated-switch controls.
- InspectCode 2026.2.3.1 reproduced native CS8509 on the unscoped invalid method.
  The scoped method kept its missing-argument error without CS8509. A missing
  Err variant inside the scope retained SPIRE009; outside the scope it retained
  both SPIRE009 and CS8509. An unrelated incomplete integer switch retained
  CS8509.
- `tmp/cs8509-rider/inspections-with-missing-control.sarif` records the final CLI
  results. InspectCode's version differs from installed Rider 2026.3, so this
  is not an editor pass.

Scoped pragmas were tested during investigation, but the user declined that
workaround. Their tests remain as evidence of diagnostic isolation.

An upstream Rider reproduction should compare the native inspection and Roslyn
diagnostics at the same switch, with and without the invocation error. There
is no reason to broaden Spire's suppression criteria: its checker already
proves complete variant coverage for this pattern.
