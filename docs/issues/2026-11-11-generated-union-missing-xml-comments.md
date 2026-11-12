# Missing XML comments on generated union members

Resolved for `Houtamelo.Spire.Functional` on 2026-11-12 by adding
`<NoWarn>$(NoWarn);CS1591</NoWarn>` to its project file. XML documentation
generation remains enabled. This suppresses CS1591 throughout that project;
generated API documentation remains a separate generator concern.

Public members emitted by the discriminated union generator produce CS1591
when the consumer enables `GenerateDocumentationFile`. Documenting the authored
union and its variant factories does not document generated members such as
`Kind`, `kind`, `IsOk`, `IsErr`, payload properties, and variant types.

Observed while documenting `Result.cs` and `ResultImpl.cs` in
`Houtamelo.Spire.Functional`. That pass documented all 57 authored public
declarations and removed both file-level CS1591 pragmas.

At the time, the normal package build also stopped at four CS0557 errors from
handwritten conversion operators that duplicated generated operators. To reach
documentation validation, an isolated project at
`/tmp/spire-result-doc-validation/` copied the two files and removed only those
four conflicting authored conversions. It used the real Spire generator and
the same netstandard2.0 dependencies.

With warnings treated as errors, that isolated build reports 40 CS1591 errors,
all in generated union files. Allowing CS1591 as a warning in the temporary
project permits XML output; its SARIF report confirms that no unsuppressed
diagnostics originate in the authored files. No production warning suppression
or generator change was added during that investigation. The existing
exhaustiveness integration suite passed all 172 tests.

After the project-level suppression, the normal Functional build passes with
zero warnings and zero errors. `GenerateDocumentationFile` and
`TreatWarningsAsErrors` both still evaluate to `true`.
