# Windows Unity and Visual Studio validation environment

Historical planning record from November 2, 2026. Subsequent execution and
results are recorded in the [Windows validation report](../docs/reviews/2026-11-04-windows-unity-validation.md)
and [platform follow-up](../docs/reviews/2026-11-05-windows-platform-tests.md).
Statements about planned or untested work below describe the planning date.

Updated November 2, 2026. The `spire-windows` VM is installed and has booted the existing Windows installation to its sign-in screen with working NAT networking. It uses direct read/write disk access authorized by the user. The user will install/update Unity, Visual Studio, and SDKs and perform account sign-ins. The test-harness extensions and Unity/IDE acceptance gates below remain planned work, not completed validation.

## Installed VM and handoff

Open **Spire Windows** from the Linux application menu. Its console was opened on the host desktop for the handoff. Closing the viewer leaves the VM running; shut Windows down from its own power menu before accessing its disk on Linux.

| Item | Installed configuration |
|---|---|
| VM | `spire-windows`, system libvirt (`qemu:///system`), autostart disabled |
| CPU / memory | 8 vCPUs, 12 GiB RAM, KVM acceleration |
| Boot | Microsoft-signed UEFI firmware, independent persistent NVRAM, emulated TPM 2.0 |
| Disk | Existing Windows disk attached read/write through an emulated NVMe controller |
| Display | QXL + local SPICE console; Windows sign-in screen verified at 1280 x 800; Unity rendered controls remain untested |
| Network | `default` NAT, Intel e1000e adapter; guest obtained `192.168.122.218` during verification |
| Controls / definition | `~/.local/share/spire-windows/manage.sh` and `domain.xml` |
| Application menu entry | `~/.local/share/applications/spire-windows.desktop` |

The launcher verifies VM/disk identity, uses a launch lock, and unmounts host volumes through UDisks before starting a stopped VM. `/mnt/windows` was successfully unmounted on the host. Its existing fstab entry is `noauto`, so boot does not mount it automatically. Do not mount Windows volumes while the VM is running.

Verified: XML/schema and libvirt definition, actual KVM boot through the original Windows EFI layout, virtual-device setup, sign-in screen, DHCP lease, connected SPICE client, idempotent launcher start, and rejection of a deliberately mismatched VM identity against live libvirt without changing the running VM. An ACPI shutdown request did not power the guest off; use the Windows power menu. A second cold-boot cycle has not been claimed or validated. No forced power-off was used.

For the first tool installation, use Unity `6000.5.6f1` with Windows IL2CPP support for fixture parity; full Visual Studio Community with Unity, C++, and extension-development workloads; .NET 10 and an SDK satisfying the existing `8.0.318`/`latestPatch` fixture pin. Existing MSVC/Windows SDK installations may already satisfy the native compiler requirement. Confirm the actual Windows version and installed tool versions after signing in. Tool installation and account management belong to the user for this handoff.

## Environment decision

Start with a local QEMU/KVM + libvirt VM booting the existing Windows disk directly, with guest writes persisting to that physical disk. Keep one interactive guest desktop for Unity Inspector and Visual Studio tests. Run the compiler/runtime checks headlessly where supported. Reuse the existing Linux/Rider validation and Unity fixtures. An early boot and graphical feasibility check determines whether this VM is suitable.

| Approach | Fit | Decision |
|---|---|---|
| Persistent local Windows VM | Reuses the existing installation and available tools; guest-side changes persist directly on the Windows disk | First candidate; cold boot, virtual hardware, and graphical checks must pass before adopting it |
| Existing or dedicated native Windows PC | Provides native desktop graphics and the same guest-side test commands | Preferred fallback if VM graphics are inadequate; reuse existing hardware if available |
| GitHub-hosted Windows with Unity installed | Fresh machines, hosted logs and artifacts; repeated installation/activation and desktop setup | Later option after the Unity/IDE harness works on a persistent environment |

Observed host: openSUSE Tumbleweed, Ryzen 9 9900X, 24 logical CPUs, about 30 GiB RAM, one visible Radeon RX 9070-series GPU, and approximately 266 GiB available on the project filesystem during preflight. Authorized host execution confirmed KVM access, libvirt 12.6.0, QEMU 11's Q35 machine, and the writable Windows host mount before unmounting it. Sandbox filesystem restrictions and mount views do not establish the host's disk permissions or mount mode. Basic VM readiness has been proved; Unity/IDE graphics and tool readiness remain separate gates.

| Component | Initial proposal |
|---|---|
| Guest | Existing Windows installation first; identify its edition, version, build, and activation after boot. A fresh Windows 11 x64 installation remains a fallback |
| Firmware | UEFI with its own persistent NVRAM and, where required, a new persistent TPM 2.0 state |
| CPU / RAM | 8 vCPUs, 12 GiB fixed RAM; one validation job at a time |
| Storage | Direct read/write attachment of the existing approximately 931-GiB Windows disk, including its EFI and recovery partitions, identified by a stable device path |
| Capacity | Check Windows free space before Unity/VS installations and builds; preserve host disk/RAM headroom for ongoing Linux work |
| Network | NAT; host-to-guest SSH and a loopback-bound graphical console |
| Desktop | Dedicated test account; 1920 x 1080, 100% scaling, fixed keyboard layout for initial UI tests |
| Lifecycle | Start on demand after exclusive disk handoff; retain VM firmware/TPM state, provisioning/version records, per-run workspaces, and exported results |

Unity, Visual Studio, guest updates, and guest build outputs consume space on the existing Windows filesystem, which most recently reported approximately 215 GiB free. Host storage is needed for VM configuration, firmware/TPM state, installers where staged, and exported artifacts. The direct-disk design does not provide automatic qcow2 disk snapshots or whole-installation rollback; reproduce the tool configuration from provisioning records and reset disposable test projects between runs.

### Reuse the dual-boot installation

Read-only inspection identifies `/mnt/windows` as `/dev/nvme1n1p3` at this inspection, an NTFS volume on a separate approximately 931-GiB Windows drive. That drive also has a 100-MiB FAT EFI partition, a 16-MiB reserved partition, and a 509-MiB recovery partition. Linux currently boots from the other NVMe drive. The Windows filesystem reports approximately 717 GiB used and 215 GiB free. The sandbox's mount view reports `ro`; the user clarified that the host mount is writable and the sandbox restricts writes outside the workspace and temporary directories. Check the actual host mount state before disk handoff. Resolve the final device by a stable disk identifier and verify its partition UUIDs; `/dev/nvme*` names can change.

The VM must see a disk, including the Windows EFI/partition layout, rather than the mounted `/mnt/windows` directory alone. Attach the complete Windows disk as a writable raw block device, as supported by [QEMU's host-drive access](https://www.qemu.org/docs/master/system/images.html#using-host-drives). The user selected direct writes to this installation; a write overlay or full clone is not part of the design. This is block-device attachment through an emulated controller, not PCI controller passthrough.

Preparation and first-boot checks:

1. Verify stable disk identity, GPT/EFI contents, NTFS consistency, and hibernation/Fast Startup state before boot. The presence of `hiberfil.sys` alone does not establish an active hibernation state. Perform required boot/filesystem repairs on the authorized Windows installation, preserving diagnostic logs. Configure clean guest shutdowns for reliable handoff.
2. Unmount every host-mounted volume on the Windows disk before starting the VM. The start wrapper must verify the actual host namespace and refuse to start if a volume is still mounted or otherwise in use. Prevent automatic host remounts while the VM owns the disk. Use normal unmounts; do not hide active users with lazy/forced unmounts. Permit host mounting again only after the VM has fully shut down and flushed its writes. This exclusive access rule applies regardless of the host mount's read/write mode.
3. Preserve the Windows disk's existing boot layout. Give the VM its own UEFI variables and register the existing Windows EFI bootloader there if needed. Start with emulated NVMe using Windows' existing native storage driver; install alternative guest drivers after a successful boot if useful. Linux's boot disk and host firmware variables stay outside the guest.
4. Cold-boot the installation with direct disk writes. Handle virtual-hardware discovery, account access, required startup repair, and any activation/recovery prompts in the guest. Existing activation and physical-machine credentials are not assumed to transfer automatically to the virtual hardware.
5. Inventory Visual Studio editions/workloads, SDKs, and Windows version from the running guest. Reuse compatible installations, reinstall Unity, and add missing requirements inside Windows. Record a working baseline only after a clean shutdown and successful repeat boot.

The existing physical disk is the VM's system disk. Windows updates, driver changes, Unity/VS installations, repairs, and test files persist there directly. A later native boot would see those same changes; the user currently intends to use this installation through the VM.

Offline inventory confirmed Visual Studio Build Tools `17.14.36401.2`, MSVC `14.44.35207`, Windows SDK headers `10.0.22621.0` and `10.0.26100.0`, and .NET SDKs `8.0.201` and `2.1.818`. The full Visual Studio Community IDE has not been located in the inspected standard paths. The user confirmed Unity may have been uninstalled and authorized reinstalling it. These findings reduce potential provisioning work but do not prove that the installed native compiler works with the selected Unity version. SDK 10 and the fixture-compatible SDK 8 patch band still need verification/installation.

Graphics are the main feasibility gate. Microsoft documents software Direct3D through [WARP](https://learn.microsoft.com/en-us/windows/win32/direct3darticles/directx-warp), and Unity 6.5 documents `-force-driver-type-warp` for [players](https://docs.unity.com/en-us/engine/6000.5/manual/unity-editor/command-line-arguments/player). This makes software rendering worth probing; it does not establish that the pinned Unity Editor and our rendered Inspector checks work in this VM. Record the actual Editor renderer and run real controls. Headless success alone cannot satisfy this gate. A native Windows machine is the fallback if this fails; host GPU reassignment is a separate design decision.

## Installed software and version policy

| Software | Baseline |
|---|---|
| Unity Hub and Windows Editor | Reinstall Hub and `6000.5.6f1` for initial fixture parity, reusing an existing exact-version installation only if found |
| Unity player support | Windows x64 Mono support and the matching Windows IL2CPP module |
| Unity packages | Preserve fixture Test Framework/JSON pins; add `com.unity.ide.visualstudio`, initially proposing `2.0.29`, and lock the resolved dependencies after the package resolves successfully |
| Visual Studio Community | Reuse a suitable full IDE if found; otherwise install current stable Community, initially targeting Visual Studio 2026; preserve installation metadata/layout where available and record the exact installed build |
| VS workloads | Game development with Unity, Desktop development with C++, and Visual Studio extension development for the test driver |
| Native compiler | Try the existing MSVC v143 toolset/SDKs; add a compatible selection if the pinned Unity Editor's build requires it |
| .NET SDKs | .NET 10 for Spire tooling/tests; an SDK satisfying the fixtures' `8.0.318` / `latestPatch` pin for Unity build preparation |
| Supporting tools | Git, PowerShell, Windows OpenSSH Server, signed guest integration drivers |

Unity requires C++ tools and a Windows SDK for [Windows IL2CPP](https://docs.unity.com/en-us/engine/6000.5/manual/platform-specific/windows/requirements-and-compatibility). Resolve the exact compatible toolset in the first trivial IL2CPP build. Install VS through explicit [workload/component selections](https://learn.microsoft.com/en-us/visualstudio/install/workload-component-id-vs-community), rather than every recommended component. Preserve the installer configuration and capture installation/reboot exit codes.

Community's [release history](https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-history) offers the current Stable installer; fixed older-release downloads have edition restrictions. Record the actual installed version, component configuration, and retained installer/layout where available. An exact older Community reinstall is not guaranteed merely by having provisioning records.

The first Unity version isolates Windows differences from Unity-version differences. It does not settle the maintained shipping baseline: `6000.5.6f1` is an existing compatibility baseline, and [docs/unity.md](../docs/unity.md) records its lifecycle and the separate 6.6 serialization issue. Add a maintained-version lane as a separately reviewed follow-up; never transfer a passing result across Unity versions.

Unity Personal versus a paid license remains an input to provisioning. Check existing activation from the guest and use the supported flow for the selected entitlement if activation is required. Keep account credentials and activated VM images outside the repository and exported artifacts. Record the existing Windows activation state; obtain recovery media if the migrated installation needs boot repair. Reusing disk contents does not establish activation on the new virtual hardware.

## Execution model

The Linux host submits a run manifest over SSH. A guest coordinator starts an interactive scheduled task under the logged-in test account for graphical stages, and returns logs, reports, and an overall exit status. Direct SSH commands can run non-UI stages. An SSH connection or Windows service does not replace the interactive desktop needed by the UI stages.

Each run uses a short local path such as `C:\Spire\runs\<run-id>` and a single exclusive job lock. UI readiness checks verify the account, session, unlocked desktop, expected window/process, resolution, and DPI. Test-only native input stays inside the guest and preserves the explicit `SPIRE_UNITY_NATIVE_INPUT=1` opt-in. Release held keys/buttons on failure and terminate only processes owned by that run.

Use a clean checkout of an exact revision without fetching the example submodules. An explicitly selected working-tree run may instead use a source bundle with a complete input/hash manifest; it must be reported as a working-tree result. Build the distributed package once per run and verify identical package file hashes in each consuming fixture. Preserve the consumer assembly layout and compiler-only plugin metadata.

Keep the package-building and validation logic in `Spire.Tool`; PowerShell handles Windows provisioning, session dispatch, and environment discovery. A proposed Windows validation entry point can sequence existing commands and the new VS/player stages. Proposed entry points are implementation work, not currently available commands.

## Required acceptance gates

| Gate | Required evidence |
|---|---|
| Provisioning smoke | Reboot, reconnect, license initialization, Unity import, a rendered Inspector edit, VS solution/editor/Quick Actions opening, and trivial Windows Mono/IL2CPP builds and launches |
| Distributed-package import | Fresh project consumes the actual package; runtime/compiler DLLs have the intended roles; generation succeeds and expected diagnostics appear |
| Windows .NET tests | Existing applicable analyzer, generator, pattern, behavioral, global-config, catalog, benchmark-correctness, and DevTools tests run against real dependencies |
| Unity Editor Mono harness | All expected platform-applicable case identities are discovered and executed; compilation/load contracts and runtime assertions pass |
| Native Unity serialization | Asset write/read in separate Editor processes, unchanged-save checks, recovery/failure cases, prefab behavior, and generated drawer support pass |
| Rendered Inspector | Real UI Toolkit and IMGUI field edits, variant switching, focus, Undo/Redo, prefab overrides, and multi-object editing; native-input availability is required |
| Windows Mono player | Build and launch the `.exe`; assert backend, every expected runtime/serialization case, process exit, and report identity |
| Windows IL2CPP player | Build and launch the `.exe`; assert the IL2CPP runtime marker and behavioral cases; separately run the release/High-stripping consumer fixture without reflection-test linker roots |
| Visual Studio Community | Unity's VS integration generates the solution; actual IDE analysis, generated symbols, Quick Actions, edits, and Unity recompilation pass |
| Repeatability | Complete one cold fixture run and one repeat run, including a guest reboot; stale reports and warm IDE state cannot produce a pass |

The compiler harness remains in Editor Mono: its dynamic compilation and process-launch checks cannot all execute in an AOT player. The player fixtures cover the generated consumer code. Require exact current case identities, zero unexpected skips, and evidence for every required gate; missing prerequisites or an unavailable control cannot silently reduce coverage. Keep platform-specific case manifests explicit, retaining Linux checks and adding Windows counterparts.

For Visual Studio, start with a small real-IDE matrix rather than duplicating every Roslyn test in the UI:

1. Generate projects with `com.unity.ide.visualstudio`; verify analyzer/code-fix references across the existing eight assembly scenarios and clean controls.
2. Open a fixture in Community and wait for live diagnostics and generated symbols. Require correct diagnostic IDs/locations and usable generated members.
3. Discover and invoke the real `Replace with SpireEnum<T>.From(value)` Quick Action. Verify the exact persisted change, disappearance of that diagnostic, and unchanged unrelated diagnostics.
4. Edit a union declaration and verify generation/IntelliSense refresh; reopen the solution to exercise persisted state. Include a deliberately invalid edit and its recovery so stale cached output cannot pass.
5. Save and close the IDE, then recompile in Unity and execute valid/invalid conversion probes. Retain the runtime assertions from the Rider fixture.

Use a test-only VS driver in an [experimental instance](https://learn.microsoft.com/en-us/visualstudio/extensibility/the-experimental-instance), with Unity tooling enabled there. The driver observes the installed IDE's workspace/editor and invokes its suggested actions; it must not load Spire providers directly or inject missing analyzer references. Verify that its plumbing also works without Spire installed as a VSIX: the consumer package remains the delivery path.

Visual Studio's Unity package has [project-generation changes](https://docs.unity3d.com/Packages/com.unity.ide.visualstudio@2.0/changelog/CHANGELOG.html), including `.slnx` and SDK-style generation for VS 2026. Accept the solution format actually generated by the pinned integration, and record its compiler inputs; copying Rider-generated projects or hand-patching references would miss the intended coverage.

## Repository work and sequence

| Phase | Changes / responsibility | Completion condition |
|---|---|---|
| 0. Resolve inputs | Existing Windows disk identity/consistency, exclusive host/guest disk handoff, guest free space and account/activation access, Unity entitlement, actual-host KVM/raw-disk access | Disk identity and consistency checks pass; device access and guest capacity are available |
| 1. Reuse and prove desktop | Configure writable raw-disk attachment and independent UEFI state; cold boot; reuse installed native tools; reinstall Unity and add missing tools, activation, SSH and interactive task; minimal Unity/VS/Mono/IL2CPP probes | Exclusive disk handoff, repeat guest boot, and all provisioning-smoke evidence collected; choose fallback host if VM graphics fail |
| 2. Port existing execution | `ToolContext`, `BehavioralWorkflow`/`UnityRunner`, `CorePlayerBuild`, process lifecycle, installed-editor resolution, shared prebuilt-package input for fixture preparation, and DevTools platform tests | Windows targets/paths/modules work; each fixture consumes the same package; Linux regression checks remain green |
| 3. Complete Unity acceptance | Windows native input in `NativeInspectorInput`; platform-aware Inspector messages; explicit launch/report verification for both core players | Full Windows Unity gates pass, including rendered edits and actual player execution |
| 4. Add VS acceptance | Parameterize asmdef project generation; new VS driver and integration cases; a VS-specific recompile report backed by shared assertions | All five real-IDE scenarios pass with the package delivery path |
| 5. Consolidate runner | One command, version/input manifests, repeatable provisioning records, disposable-project cleanup, artifact export, cold/repeat runs, documentation | A run can be initiated from Linux and produces a complete, reviewable result without IDE babysitting |
| 6. Optional CI connection | Trusted-revision dispatch to the proven runner; define isolation and credential handling before runner registration | CI uses the same acceptance command and preserves its evidence |

Current implementation boundaries to address:

- [BehavioralWorkflow.cs](../tools/Spire.Tool.Core/BehavioralWorkflow.cs#L159) selects `StandaloneLinux64`; [CorePlayerBuild.cs](../tests/UnityVariantPayloadCore/Assets/Editor/CorePlayerBuild.cs#L17) also hard-codes Linux outputs. Add explicit host/target selection and Windows `.exe` paths.
- [IntegrationWorkflow.cs](../tools/Spire.Tool.Core/IntegrationWorkflow.cs#L20) currently invokes the Editor build method but does not launch the generated core players. Add actual execution and report validation; a successful build alone is insufficient.
- [NativeInspectorInput.cs](../tests/UnityVariantPayloadCore/Assets/Editor/NativeInspectorInput.cs#L16) implements X11/XTest. Add a Windows backend for native pointer/key input with correct window coordinates, focus, and DPI handling. Keep real rendered-edit assertions.
- [AsmdefIntegration.cs](../tests/UnityAsmdefIntegration/Assets/Editor/AsmdefIntegration.cs) currently selects Rider's project generator. Isolate IDE-specific generation while sharing the assembly scenarios and Unity assertions.
- [JetBrainsWorkflow.cs](../tools/Spire.Tool.Core/JetBrainsWorkflow.cs#L118) launches Linux Rider through Xvfb. Preserve this existing lane; add a separate VS host driver rather than treating the Rider launcher as cross-platform.
- [UnityHarnessRunnerTests.cs](../tests/DevTools.Tests/UnityHarnessRunnerTests.cs#L158) has a `/bin/sh` lifecycle test. Keep its Linux contract and add Windows process-tree/timeout coverage.
- Existing SDK pins and inherited MSBuild policy must remain isolated from the disposable consumer projects. Resolve installed Unity/VS paths through configuration/discovery and verify the actual selected instances.

Every implementation phase needs real integration tests for its successful and failing paths. Include missing executable/module, expected diagnostic absent, Quick Action unavailable, malformed/stale/missing report, failed player assertion, crash, timeout/child cleanup, and case-discovery mismatch. Preserve failures as artifacts; never turn them into skipped success. Format changed C# files and run the affected real test projects plus Linux regressions before accepting a phase.

For the public GitHub repository, start with host-initiated trusted local runs. Decide CI isolation separately before exposing a persistent signed-in VM to externally supplied revisions. GitHub documents the persistent-runner boundary in its [self-hosted runner guidance](https://docs.github.com/en/actions/concepts/runners/self-hosted-runners). Optional CI does not postpone any of the Unity or VS acceptance gates.

## Results and open inputs

Export a run summary plus raw Unity XML/JSON, player logs, VS ActivityLog and driver results, source before/after snapshots, and failure screenshots. Each result identifies the source revision or source bundle, package hashes, OS/Unity/IDE/native-toolchain/SDK versions, renderer, backend, stripping level, expected/actual case identities, exit codes, and durations. Reports carry a unique run ID and are validated against the run inputs. Keep credentials, signed-in profiles, package caches, and VM disks out of artifacts.

An overall Windows acceptance pass requires every mandatory gate above. A hardware/driver claim or performance conclusion requires separate native-hardware measurements; software rendering is sufficient only if the actual functional controls pass.

The existing Windows installation has booted successfully through the VM, and exclusive host/guest disk handoff plus KVM access were verified. Pending checks are user sign-in, actual Windows version and activation on virtual hardware, tool installation/Unity activation, repeat cold boot, and all Unity/Visual Studio acceptance gates. The user will reuse/update the installed native tools, reinstall Unity, and add the full Community IDE and required SDKs as needed. The Unity and Visual Studio acceptance contract stays the same.
