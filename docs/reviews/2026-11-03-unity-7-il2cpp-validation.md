# Unity 7 IL2CPP validation

Spire 4.7.6 passes all **100 original behavioral tests** and **60 native runtime
and serialization checks** on Unity `7000.0.0a12` (`8751f7c83ed7`), Linux x86_64,
with High managed stripping. The full, unfiltered Unity Test Framework run passes 107
cases: the 100 behavioral tests plus seven bridge contracts, with zero failures,
inconclusive cases, or skips.

An earlier run stalled in `Il2Cpp_ProcessStartReportsUnsupportedRuntime`, a
subprocess capability test that did not exercise Spire features. That test and
its legacy exception-expectation helper were removed. The subsequent unfiltered
run passes all 107 remaining cases. The original timeout remains recorded in the
[probe investigation](../issues/2026-11-03-unity-7-process-capability-probe.md).

| Check | Result |
|---|---|
| Original behavioral Fact identities and assertions | 100/100 pass |
| All remaining NUnit bridge contracts | 7/7 pass |
| Native serialization preparation in the CoreCLR Editor | 118 checks pass |
| Native IL2CPP runtime/serialization player | 60 checks pass, exit 0 |
| High stripping | Both player configurations use High |
| Compiler-host assemblies in stripped player inputs | None found |
| Unrelated subprocess capability contract | Removed; unfiltered rerun passes |

Behavioral coverage includes union construction, payload access, deconstruction,
implicit conversions, generic/reference payloads, record unions, generated
Newtonsoft.Json and System.Text.Json round trips and error handling, inliners,
enum conversions, and public utilities. Native checks cover persisted assets,
managed-reference aliasing and cycles, Unity object identity, generic unions,
hydration, former-name migration, and invalid-payload fallback. The player log
confirms that the persisted invalid-payload asset was loaded.

## Evidence

The [verified manifest](../../tmp/unity7-il2cpp-validation-20261103/manifest.json)
records all expected test identities, copied-source and DLL hashes, backend
evidence, player assembly inventories, and artifact hashes. The collector checks
every expected Fact name, every remaining bridge contract, every case result,
and successful Editor exit.

- [Unfiltered NUnit results](../../tmp/unity7-il2cpp-validation-20261103/behavioral/unity7-il2cpp-behavioral-unfiltered.xml): 107 passed, exit 0.
- [Behavioral runtime identity](../../tmp/unity7-il2cpp-validation-20261103/behavioral/il2cpp-native-backend.json): `ENABLE_IL2CPP`, `LinuxPlayer`, Unity `7000.0.0a12`.
- [Native player results](../../tmp/unity7-il2cpp-validation-20261103/native/unity7-il2cpp-player.json): 60 checks, no failures, exit 0.
- [Native preparation](../../tmp/unity7-il2cpp-validation-20261103/native/unity-union-serialization-player-preparation-results.json): 118 checks, no failures.

This Unity 7 IL2CPP player reports `DefaultDomain` and uses
`System.Private.CoreLib, Version=10.0.0.0`. The older harness gate requiring the
literal domain name `IL2CPP Root Domain` therefore does not apply. A temporary
runtime initializer records the compiled backend and actual player platform;
the accepted run also builds and links native IL2CPP output. The separate native
player independently reports its compiled IL2CPP backend.

## Fixture adaptations and scope

All preparation and compatibility adaptations are confined to disposable
projects under `tmp/unity7-il2cpp-validation-20261103`. The original 100 behavioral
tests and native runtime fixture sources remain byte-identical. The tracked
bridge changed only to delete the subprocess probe, its helper, and unused
imports; retained assertions are unchanged. All four package DLLs match the preserved 4.7.6 hashes used in
the [serialization crash comparison](2026-11-03-unity-7-serialization-recheck.md).
No production implementation was changed for this run.

The projects use Unity 7's bundled SDK `10.0.303` and Test Framework `1.9.0`.
The behavioral fixture uses its normal NuGet Newtonsoft.Json dependency because
Unity 7 no longer provides the old Editor DLL path. The native builder runs its
IL2CPP branch with its original strict-build and High-stripping checks. Test
assemblies and JSON test dependencies retain their existing linker roots.

The restart left truncated derived Bee artifacts, including zero-byte cached
assemblies. Those caches were preserved separately and the rerun used a private
Bee cache. Unity's bundled linker also required `libxml2.so.2`; a compatible
library was extracted into a task-local directory and supplied through
`LD_LIBRARY_PATH`. Host packages and system libraries were not changed.

These results establish this Linux IL2CPP configuration. Rendered Unity 7
Inspector behavior, CoreCLR players, Windows, other architectures, and a Unity
6.x serialization-crash fix require their own verification.
