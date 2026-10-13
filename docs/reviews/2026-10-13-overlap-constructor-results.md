# Overlap constructor initialization

Ordinary `Layout.Overlap` factories now initialize readonly storage in generated
private constructors instead of writing through `Unsafe.AsRef`. Field names,
offsets, readonly modifiers, public factory/property signatures, and deconstruct
behavior are preserved. No storage fields or marker types were added.

Constructors share ordered CLR-equivalent parameter signatures. A discriminator
switch routes arguments when those variants write to different physical fields.
Reference nullability, tuple element names, and `dynamic` do not create duplicate
overloads. Closed containing generic types remain distinct. Boxed fields retain
typed constructor parameters and box when assigned to their existing storage.

The Unity-serialized Overlap path retains its mutable construction and payload
schema. `UnsafeOverlap` retains its separate Unsafe dependency. This change does
not select a new supported Unity baseline or alter the showcase prerequisites.

## .NET validation

All 2,304 Release tests pass:

| Project | Passed |
| --- | ---: |
| Source generators | 918 |
| Analyzers | 826 |
| PatternAnalysis | 339 |
| Behavioral tests | 129 |
| DevTools | 92 |

Nine focused behavioral tests compile, emit, load, and execute generated unions.
They cover field routing, defaults, copies, nulls, reference identity, nullable
generic and tuple annotations, dynamic/object, boxed structs, long tuples versus
explicit `ValueTuple`, closed containing types, readonly reflection, and modern
init/deconstruct support. A negative compiler control rejects writes to readonly
fields outside constructors.

The C# 9 cases use the real .NET Core 3.1 reference pack. `Unsafe` is absent from
their compilation and generated assembly references. `IsExternalInit` is absent
from the framework references; the Spire runtime's internal PolySharp definition
is inaccessible to the consumer. Generated nullable warnings are rejected.
Inside Editor Mono these behavioral tests instead use the actual runtime
references, excluding assemblies that expose the helper API. The core BCL's
private helper types remain inaccessible. This avoids loading .NET 3.1 tuple
type forwarders into Mono while retaining the .NET compilation proof and the
native Unity 6.3 acceptance below.

Eight ordinary Overlap snapshots were updated and two added. The full suite also
passes the existing JSON, Unity-serialized Overlap, UnsafeOverlap, and other-layout
controls. Reports are under `tmp/overlap-constructors/dotnet`.

The [coverage matrix](2026-10-13-overlap-constructor-coverage.md) records the
construction contract. The tracked native fixtures are
`tests/UnityAsmdefIntegration/Assets/RuntimeAuto/RuntimeAuto.cs`,
`RuntimeAuto/OverlapConstructorPlayerProbe.cs`, and
`Editor/OverlapConstructorPlayerBuild.cs`.

## Native Unity validation

Unity 6000.3.26f1 on Linux x64 passes the native asmdef fixture under Xvfb.
All eight assembly probes execute successfully; baseline, intentional SPIRE001
failure, repair, missing-core failure, and repair phases pass. The runtime-auto
probe now constructs a readonly ordinary Overlap union with same-signature field
routing, primitive fields, references, a boxed struct, and a fieldless variant.
Neither `Assets` nor `Packages` contains an added Unsafe DLL or `link.xml`.

The dedicated Linux IL2CPP player builds successfully with zero errors and High
managed stripping, then executes the same construction assertions successfully.
The build report verifies the actual backend and stripping setting; the runtime
report independently confirms `IL2CPP`, `pass`, and no errors. No linker
preservation file or runtime Unsafe DLL was added. Unity's LLVM 15 linker uses
the already documented scoped `libxml2.so.2` installation for this Linux host.

Evidence under `tmp/overlap-constructors`:

- `lts-asmdefs/asmdef-results.json`
- `lts-asmdefs/Assets/GeneratedOverlapPlayer/overlap-constructor-player-build-report.json`
- `il2cpp-player-results.json`
- `lts-asmdefs/unity-overlap-il2cpp.log` and `il2cpp-player.log`

The first Editor import exposed a direct runtime-assembly reference in the new
Editor test builder. The builder now resolves the probe through loaded assemblies,
preserving the original asmdef reference graph; the corrected native run passes.

The full Unity 6000.5.6f1 Editor Mono harness passes 2,328/2,328 with zero skips.
All 2,189 portable xUnit identities match the .NET discovery manifest and execute;
all 129 original behavioral cases and ten bridge contracts also pass. Evidence is
`newer-harness/harness-results.xml` and `newer-harness-cli-final.log`. The focused
Editor run independently passes all nine new constructor tests.

The rebuilt consumer bundle is under `tmp/unity-package/com.houtamelo.spire`.
Its analyzer DLL matches the DLL exercised by the native 6.3 fixtures.
