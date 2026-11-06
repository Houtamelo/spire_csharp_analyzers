# Windows Unity version matrix

All applicable Editor, native persistence, rendered Inspector, Mono/IL2CPP player,
and Visual Studio Community gates pass on Windows x64 for `2022.3.62f3`,
`2023.2.22f1`, and `6000.5.6f1`. Unity 2022 retains its documented restriction:
generic unions cannot use native Unity serialization. Generic runtime checks
remain active. The package version and declared minimum are unchanged.

Validation starts from commit `dde1e03ad8137e0291726e1e078fdaac77e67825` and
includes the eight source-file changes described below. It validates package
`4.7.6`. All three Windows Editors and Windows IL2CPP modules are installed.

## Results

| Gate | Unity 2022.3.62f3 | Unity 2023.2.22f1 | Unity 6000.5.6f1 |
| --- | ---: | ---: | ---: |
| Complete, unfiltered Editor suite | 2,692 passed | 2,692 passed | 2,692 passed |
| Applicable portable case identities | 2,570 matched | 2,570 matched | 2,570 matched |
| Original runtime Facts compiled by Unity | 100 passed | 100 passed | 100 passed |
| Compiler probes | 5 passed | 5 passed | 5 passed |
| Native Write / fresh Read / Recovery / accessors | 125 / 86 / 59 / 9 | 128 / 89 / 59 / 9 | 128 / 89 / 59 / 9 |
| Concrete-wrapper Write / fresh Read | 79 / 44 | 79 / 44 | 79 / 44 |
| General native integration | 11 passed | 11 passed | 11 passed |
| Rendered Inspector cases / assertions | 17 / 192 | 18 / 204 | 18 / 204 |
| Release Windows Mono player | 59 checks | 60 checks | 60 checks |
| Release Windows IL2CPP player | 59 checks | 60 checks | 60 checks |
| Behavioral IL2CPP suite | 108 passed | 108 passed | 108 passed |
| Visual Studio Community acceptance | Passed | Passed | Passed |

All accepted processes exit 0. Every Editor case passes with zero NUnit skips;
12 Linux-only cases are explicitly excluded by independent platform discovery.
Exact case identities, execution names, original Fact names, and exclusion
identities/reasons match. The 2,692 cases comprise 2,570 portable cases, 100
original Facts, 17 bridge contracts, and 5 compiler probes. The standard CLI
harness command selects fewer assemblies; these final Editor runs deliberately
omit the assembly filter to include all compiler probes.

The full Windows .NET solution passes **2,707 tests** across eight projects,
with the same 12 explicit Linux exclusions. Focused Windows process checks pass
12/12. Actual IL2CPP reports contain the IL2CPP runtime marker and execute every
original behavioral Fact.

Rendered tests use Direct3D 11 and native Windows input. All applicable controls
are verified; there are no failed or unverified cases. Only
`ClosedGenericKindControl` is not applicable on Unity 2022. Its corresponding
generic native persistence assertions remain explicitly inapplicable; the
other generic runtime cases still run.

Both production player backends build with High stripping and run their original
assertions on actual Windows executables. Compiler-only Spire DLLs are absent
from all six player outputs. Visual Studio checks exercise live diagnostics,
one representative Quick Action, generated-symbol navigation, diagnostic refresh,
and recompilation in the corresponding Unity Editor.

Unity 6000 customer acceptance also passes a fresh package import and all
16 Showcase tests. This customer/sample gate uses the sample's Unity 6000/URP
requirements; no older-Editor sample acceptance is claimed.

## Corrections and retained failures

- The Git fixture passed an absolute submodule destination to Git for Windows,
  producing an invalid `.git/modules/C:/...` path. It now uses a destination
  relative to its disposable repository. All 17 fixture tests pass on Linux and
  Windows without changing their assertions.
- Unity logs can still have an open writer when the Editor process exits.
  Integration/customer workflows now use a reader that shares writes and deletion.
  A Windows red run of the previous reader passes five cases and fails the real
  open-writer case. After an explicit rebuild, all six filesystem regression
  cases pass; the combined tooling regression run passes 23/23. The complete
  Linux DevTools project passes 161 tests with eight Windows exclusions.
- Three native fixture files called `GetEntityId` unconditionally and failed
  compilation on older Editors. They now retain `GetEntityId` under
  `UNITY_6000_4_OR_NEWER` and use `GetInstanceID` otherwise. Reference equality
  and native-ID equality assertions remain intact. This follows Unity's
  [identity API migration](https://docs.unity.com/en-us/engine/6000.5/manual/scripting/fundamental-unity-types/instanceid-to-entityid-migration)
  and the older [GetInstanceID API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Object.GetInstanceID.html).
  Corrected native tests and actual players pass on all three versions.

Initial rendered attempts correctly refuse input when a validation-player
firewall notification covers the target, or the guest's Logitech overlay takes
foreground focus. Scoped notifications are closed; the overlay is temporarily
closed for the final 2022 run and restored afterward. Final reruns keep the real
input guards and every assertion. Owned project-version metadata is aligned
with the requested Editor to avoid the version-mismatch startup dialog.

One initial .NET process-output deadline case fails during concurrent Editor
installation. The unchanged case passes in the final complete solution, focused
12-case run, and all three complete Editor runs. No timeout is increased.

Repeated native-project preparation also exposes a separate file-sharing error
before a player build starts. Accepted production builds use fresh disposable
projects; native persistence phases use distinct process logs. All original
methods and assertions execute. The unresolved orchestration issue and its
evidence are recorded in the [file-lifetime report](../issues/2026-11-06-windows-validation-file-lifetime.md).

## Provenance and environment

The guest uses Windows 10 Enterprise `19045.6456`, VirtualBox/Guest Additions
`7.2.22`, VBoxSVGA 3D, 8 vCPUs, and 12 GiB RAM. Visual Studio Community is
`2026 18.11.3`; the Windows validation SDK is .NET `10.0.403`, with SDK
`8.0.318` available for Unity's player tooling. There is no GPU passthrough.

The guest receives an archived commit snapshot with `examples/` excluded.
All eight patched source hashes match the host and guest; all 12 prepared native
fixture copies match the corrected source. The four product DLL hashes match
across 24 prepared package copies, including Editor, native, player, and IDE
projects. The live repository's examples are not validation inputs.

Windows records one AHCI reset during the run. Package checks complete
successfully afterward, but VM storage stability is not established; see the
[storage observation](../issues/2026-11-06-windows-vm-ahci-reset.md).

Host evidence is under `tmp/windows-matrix-20261106/`: `acceptance-proof.json`
records strict acceptance and provenance checks; `acceptance-summary.json`
contains selected outcomes and every retained attempt; `audit.py` and `prove.py`
reproduce those checks. Source archives and both patch manifests remain there.

Raw Windows evidence is under
`~/.local/share/spire-windows/virtualbox/results/windows-matrix-20261106/`.
Within `final/`, version directories contain complete Editor, behavioral,
customer, and IDE results. `players-clean/`, `render-repeat/`,
`render-2022-final/`, and `native-repeat/` retain the accepted independent runs.
`final-provenance.json` records source/package hashes, fixture copies, restored
overlay state, Editor versions, and system events. Earlier failed attempts,
including artifact-collection and incremental-build mistakes, remain archived.
