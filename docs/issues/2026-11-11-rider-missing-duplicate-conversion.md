# Missing duplicate conversion diagnostic in Rider

The user reported that Rider shows no error for two identical implicit
`Result<T>` to `Result` conversion operators whose bodies contain exhaustive
variant switches. The compiler diagnostic is CS0557.

Spire's exhaustiveness suppressor declares only CS8509 and CS8524. Roslyn
filters the supplied diagnostics to those IDs, excludes diagnostics whose
default severity is Error, and rejects suppression descriptor ID mismatches.
See the [Roslyn suppression context](https://source.dot.net/Microsoft.CodeAnalysis/DiagnosticAnalyzer/DiagnosticAnalysisContext.cs.html)
and [suppression validation](https://source.dot.net/Microsoft.CodeAnalysis/DiagnosticAnalyzer/Suppression.cs.html).

The isolated reproduction is `tmp/cs8509-duplicates/Fixture.sln`. With the
current Spire generator and suppressor loaded, the installed .NET 11 preview
SDK reports exactly one error, CS0557 at `Cases.cs:30`, and no warnings.

Read-only inspection of installed Rider 2026.3 (263.7307.258) also found that
`OverlapResolverBase` matches suppressed Roslyn highlightings against compiler
IDs and source ranges. No broad suppression of unrelated errors was found.
This does not establish why the user's editor omitted the duplicate error.

InspectCode 2026.2.3.1 reports duplicate-member errors at both declarations in
that fixture (`tmp/cs8509-duplicates/inspections.sarif`). Its version differs
from installed Rider, so this does not reproduce the editor omission.

The user then reported manual payload conversion operators conflicting with
Spire-generated operators. Building the actual Functional project reports
four CS0557 errors, all located in generated `.g.cs` files: two for `Result`
and two for `Result<T>`. The compiler still rejects the conflicts.

During the initial investigation Rider's global settings contained a `*.g.cs` inspection exclusion at
`~/.config/JetBrains/Rider2026.3/resharper-host/GlobalSettingsStorage.DotSettings:23`
(`CodeInspection/ExcludedFiles/FileMasksToSkip`, value True). The user has since
removed that exclusion and marked the mask as generated code. Unlike marking
code as generated, an inspection exclusion can hide compiler inspections too;
see [Rider's inspection configuration](https://www.jetbrains.com/help/rider/Code_Analysis__Configuring_Warnings.html).
This is a candidate explanation for invisible generated-member conflicts,
not a verified cause of the earlier hand-written duplicate omission. The
Functional folder also has an explicit ForceIncluded entry for its nongeneric
generated Result file, so effective settings and the working project's
settings still needed comparison at that point. The agent changed no IDE settings.

`SuppressorDiagnosticIsolationTests.cs` adds five real-generator regression
tests. They compare diagnostics with and without the suppressor, preserve
CS0557, CS0111 and CS0168, and verify that the four manual/generated conversion
conflicts are reported in generated syntax trees.
The full source-generator exhaustiveness filter passes 170 tests.

The Unity comparison project is
`/home/houtamelo/Documents/projects/axel/futa_pandemic/Unity_Futapandemic`.
Rider's cached generated Result files for both projects are under the same
`/tmp/JetBrainsPerUserTemp-1000-1/SourceGeneratedDocuments/` scheme, with the
same generator and filename. Their conversion operators are identical; the
nongeneric files differ only in Debug.Assert calls. Unity does not bypass
the filename mask through a different generated-file directory convention.

Two isolated SDK fixtures under `tmp/cs8509-generated-conflicts/` compare the
locally built analyzer DLLs against the Unity package's DLLs. Both target
netstandard2.0/C# 10 and use identical source. Both compilers report two CS0557
errors in generated code; InspectCode reports both conflicts on the handwritten
declarations in each fixture. Binary provenance alone does not reproduce the
live project's omission. The remaining difference in live editor analysis is
unresolved; no production suppressor change is justified by these results.
