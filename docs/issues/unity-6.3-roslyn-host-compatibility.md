# Unity 6.3 LTS compiler compatibility

The initial Roslyn host mismatch is resolved on `unity-6000`. The compiler
extensions now build against Roslyn 4.3.1 and reference compiler assemblies at
version 4.3.0.0. Unity 6000.3.26f1 loads the generator and analyzers.

Full Unity 6.3 product support is still under evaluation. `UnsafeOverlap` needs
a runtime Unsafe dependency that the older Editor does not expose to consumer
compilations. The shipping manifest and showcase prerequisites remain unchanged.
Ordinary `Overlap` now initializes readonly storage inside generated constructors,
removing its previous `Unsafe.AsRef` dependency. The Unity serialization path for
`Overlap` continues to assign mutable storage directly.
The constructor change passes native 6.3 Editor Mono and a Linux IL2CPP player
with High stripping, without an added Unsafe DLL or consumer `link.xml`. See
[the implementation and validation report](../reviews/2026-10-13-overlap-constructor-results.md).

## Original failure

On October 12, 2026, the exact Release bundle was installed into a disposable
native Inspector fixture using the official Linux Editor and IL2CPP module from
[the 6000.3.26f1 release](https://unity.com/releases/editor/whats-new/6000.3.26f1).
The Editor ran under Xvfb. Compilation reported:

```text
warning CS9057: The analyzer assembly '.../Houtamelo.Spire.Analyzers.dll'
references version '4.10.0.0' of the compiler, which is newer than the
currently running version '4.3.0.0'.
```

The generator therefore did not emit implementations, producing downstream
CS8795 errors for the fixture's partial variant factories. The Editor exited
with code 1 before executing the acceptance method. No native test pass or
IL2CPP compatibility claim can be inferred from this attempt.

Reproduce from the repository root:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-integration \
  --editor /path/to/6000.3.26f1/Editor/Unity \
  --project tmp/unity-63-compatibility --graphics
```

The evidence is in `tmp/customer-delivery/lts-inspector-cli.log` and
`tmp/customer-delivery/lts-inspector/unity-integration-1.log`. The isolated
Editor installation is under `tmp/customer-delivery/unity-6000.3.26f1`.

## Compatibility implementation

Only two newer enum names blocked the downgrade: `LanguageVersion.CSharp12`
and `RefKind.RefReadOnlyParameter`. A small shared helper recognizes their
stable public values, preserving C# 12 InlineArray selection and ref-readonly
parameter behavior when the same extensions run on newer compiler hosts.
CodeFixes and PatternAnalysis needed no source changes.

The values are defined in Roslyn's public
[LanguageVersion enum](https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/LanguageVersion.cs)
and [RefKind enum](https://source.dot.net/Microsoft.CodeAnalysis/Symbols/RefKind.cs.html).

The initial Roslyn downgrade regression run passed 2,293 .NET tests with the older extensions
running in its Roslyn 4.10 test host. Added integration cases cover ref-readonly
diagnostics and generated host signatures; a package test inspects the exported
DLL references and rejects a future compiler baseline above 4.3.

The real Unity 6.3 Inspector fixture passes all 120 assertions after adding
`System.Runtime.CompilerServices.Unsafe.dll` from the existing test dependencies
to the disposable project. This additional DLL is not in the shipped package.
Changing only the project's API compatibility level to .NET Framework did not
resolve the missing Unsafe API. Native acceptance must therefore be described
with this prerequisite until the runtime dependency policy is decided.

The requirement is a runtime helper API, not support for the C# `unsafe` keyword.
`UnsafeOverlap` emits `Unsafe.As`, `Unsafe.Add`, `Unsafe.ReadUnaligned`, and
`Unsafe.WriteUnaligned` to access its unmanaged byte buffer. The separately
supplied DLL is approximately 19 KiB and comes from Unsafe package 6.1.2. Unity
6000.5.6f1 already references its own version from the BCL targeting pack. Any
shipping dependency solution needs to account for that newer-Editor definition.

Evidence is under `tmp/roslyn43`: `native-inspector-results.json` records the
native pass; `lts-inspector/unity-unsafe-probe.log` records the conditional run;
the initial unchanged-fixture and Framework-profile compilation failures are
preserved in that project's archived logs. The evaluation also checks native
player execution and newer-host compatibility separately.

The complete Unity 6.3 Linux IL2CPP serialization player passes with High
stripping, no consumer `link.xml`, and the same separate Unsafe 6.1.2 dependency.
The report confirms all baseline, stability, and managed-reference assertions,
including the actual IL2CPP backend. The first native link failed because the
installed LLVM 15 linker could not load `libxml2.so.2`. Extracting the official
openSUSE Leap 16 package into `tmp/roslyn43/linker-dependencies` and scoping
`LD_LIBRARY_PATH` to that directory resolved it without modifying system files
or changing Unity's compiler/linker. The build and runtime reports are
`lts-inspector/unity-player-build-report.json` and `il2cpp-player-results.json`.

Native asmdef acceptance passes on both 6000.3.26f1 and 6000.5.6f1: all eight
assemblies load the extensions, the intentional SPIRE001 error and omitted-core
reference are rejected, both repairs compile, and generated APIs are verified.
Unity 6.3's two-pass Bee compilation prints some warnings that are omitted from
later `assemblyCompilationFinished` callbacks. Repair phases now require zero
errors and a completion callback for every assembly; the baseline still requires
all eight SPIRE002 warnings. No stale diagnostic replay or warning suppression
is used.

The initial newer-Editor Mono harness run passed 2,317/2,317 on 6000.5.6f1 with exact
identity coverage for all 2,178 portable xUnit cases. It continues to use Roslyn
4.10 for the compiler test harness while loading the 4.3-built extensions. These
results do not imply the C# 12 harness sources can compile in Unity 6.3.

Rider 2026.2.3.1 also accepts the older extensions. InspectCode reports SPIRE002
in all eight fixture assemblies and the expected SPIRE016 cast warning. The
actual Rider quick fix is discovered and applied, and Unity 6.3 recompiles its
result successfully. Reports are in `lts-asmdefs/jetbrains-inspections.sarif`,
`RiderDriver/rider-quick-fix.json`, and `jetbrains-integration-results.json`.
The initial isolated Rider startup imported settings and restarted before the
driver could run; rerunning only the quick-fix stage with that setup complete
passed. No compiler-host DLLs were added to the consumer package.

## Maintenance assessment

Keep the single Roslyn 4.3.1 dependency set for the Unity branch. Current
functionality needs only the two compatibility checks; no emitter or analyzer
features were removed. Future Roslyn API additions must either have a small
compatible implementation or justify increasing the baseline. Keep the actual
exported-reference guard and run the native old-host fixtures alongside the
newer-host regression suite. IDE and Editor host compatibility remain separate
checks. The downgrade is retained on the Unity branch without changing the
Unity support policy.

Compiling against the older Roslyn API does not downgrade the consumer's C#
compiler or add newer language features to Unity 6.3. The full compiler test
harness retains Roslyn 4.10 and its C# 12 fixture requirements. Compiler-host DLLs
remain excluded from customer packages. The separate non-Unity product on `main`
is outside this work.

The manifest remains pinned to the verified 6000.5.6f1 environment. Unity marks
6.5 end of life, and the separately reported 6.6 callback crash still limits
native serialization there. A maintained native-serialization baseline remains
unresolved; see [the commercial review](../reviews/2026-10-11-commercial-readiness.md).

The showcase's URP 17.5.0 and Core RP 17.5.0 package metadata require Unity
6000.5. Choosing and validating older sample dependencies remains necessary
before advertising 6.3 installation support. The Roslyn downgrade itself is
feasible and does not require a second set of compiler-extension sources.
