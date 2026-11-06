# Windows Unity validation

The Windows environment runs Unity 6000.5.6f1 with actual Editor, rendered
Inspector, Mono/IL2CPP player, and Visual Studio checks. The November 5
[platform follow-up](2026-11-05-windows-platform-tests.md) resolved the 13
Linux-specific fixture failures: all 2,671 applicable Editor Mono checks passed,
with 12 Linux exclusions independently verified. The historical failures remain
recorded below.
The initial customer run rejected the sample preset's edited authored data.
The subsequent [fixture isolation verification](2026-11-05-test-fixture-isolation.md)
passed fresh customer acceptance and all 16 sample tests without touching the
interactive examples.

## Environment

| Component | Observed version/configuration |
|---|---|
| Windows | Existing Windows 10 Enterprise, build 19045.6456 |
| VirtualBox / Guest Additions | 7.2.22 / 7.2.22 r175245 |
| Resources | 8 vCPUs, 12 GiB RAM, VBoxSVGA 3D, 256 MiB VRAM, 1920×1080 |
| Unity | 6000.5.6f1, Windows Mono and IL2CPP support |
| Visual Studio Community | 2026, 18.11.3; executable version 18.11.12328.437 |
| .NET SDKs | 10.0.403 and fixture-pinned 8.0.318 |
| Native toolchain | MSVC 14.44.35207; Windows SDKs 22621 and 26100 |
| Storage | Original Windows SSD through emulated Intel AHCI; direct writes |

The VM UUID is `2e59366f-6c6e-40b2-8e75-87c9d4940018`. QEMU remains off while
VirtualBox uses this SSD. There is no PCI GPU passthrough. Guest Direct3D 11
hardware-device creation and rendered pixel readback succeeded through the
VirtualBox graphics stack.

## Recorded checks

| Gate | Recorded result |
|---|---|
| Windows .NET analyzers | 926 passed |
| Windows .NET generators/code fixes | 1,042 passed |
| Windows .NET behavioral tests | 100 passed |
| Windows catalog | 21 passed, including malformed-reference rejection |
| Windows development tools | 115 passed, 13 Linux-only failures; subsequent fixture guards also passed |
| Unity compiler probe | 5 passed in Editor Mono |
| Full Editor Mono, initial snapshot | 2,631 passed, 13 Linux-only failures; all case identities matched |
| Full Editor Mono, post-AHCI repeat | 2,645 executed: 2,631 passed, 14 failed; exact identities and complete execution verified |
| Packaging assertion follow-up | The additional stale IDE-documentation assertion was fixed; its real package-export case passed in Windows .NET and Editor Mono |
| Native serialization | Write 126, fresh-process Read 87, Recovery 59 checks passed |
| Rendered Inspector | 18 cases / 202 checks passed; zero failures or unverified cases |
| Release Windows Mono player | 60 checks passed, non-development build |
| Release Windows IL2CPP player | 60 checks passed, non-development build |
| Behavioral Editor Mono | 107 passed, including all 100 original facts |
| Behavioral Windows IL2CPP | 108 passed, including all 100 original facts |
| Visual Studio | Eight live SPIRE002 locations, SPIRE016, actual Quick Action, exact saved edit, diagnostic refresh, generated Kind navigation passed |
| Unity after the IDE edit | All eight assemblies recompiled; valid/invalid conversion probes passed |
| Package Manager installation and sample import | Completed; subsequent authored-preset acceptance failed |
| Customer follow-up after fixture isolation | All stages and 16 sample Editor/Play Mode tests passed; see the November 5 report |
| Customer polling deadline regression | 6,002 callbacks remain within budget; expired deadline correctly rejected |
| Post-AHCI repeat | Visual Studio, fresh rendered Inspector, and both 60-check players passed; 13 Linux-only cases still failed at that stage |
| November 5 platform follow-up | Windows .NET development tools: 150 passed; full Editor Mono: 2,671 passed; exact applicable identities and 12 Linux exclusions verified |

Player builds select `StandaloneWindows64`, use High managed stripping, and
retain separate Mono/IL2CPP outputs. IDE coverage exercises one representative
Quick Action; provider-level tests cover the remaining transformations.
The complete 2,645-case run was retained with its 14 failures. The additional
packaging assertion then passed in a focused rerun; the full suite was not
repeated at that stage. No cases were skipped or silently excluded in that run.
The harness was prepared again for that focused case. Its regenerated input
manifests describe the corrected fixture; the retained full-run XML and log
describe the earlier run. They are not a single run of the final source.

The source is an owned working-tree snapshot at `C:\SpireValidation\source`,
including pre-existing uncommitted work. These results certify that snapshot's
recorded gates, not a clean release commit. The later portability/automation
changes and post-AHCI repeat have their own result directories.
The initial snapshot manifest records commit `420bc0bcff09d8ad7b1d46c548277d59847c84eb`
plus 4,126 working-tree files. Scoped source hashes and the installed Visual
Studio driver's SHA256 are recorded under `sata-validation`; the installed DLL
matches the locally built driver. One integration source differs only in
formatting, and the driver was deployed as a prebuilt DLL rather than built
from guest source.

Customer installation resolved the expected external package, and the sample
imported and compiled. The preset's command sequence and values differ from
the acceptance fixture's reset-data expectations. This failure is preserved;
the shipped sample was not reset to manufacture a passing result. The polling
regression's expected nonzero exit is independently checked against both its
success marker and its expired-deadline failure report.

## Storage fault and remediation

During the customer-import retry, the guest crashed with bugcheck `0x124`, first
parameter `0x10`. VirtualBox captured the stop code. Windows logged repeated
`stornvme` event 129 resets beforehand and a `volmgr` event 161 failure to write
the crash dump. A specific WHEA record/dump was unavailable. Microsoft documents
`0x10` as a device-driver error source; the preceding NVMe resets make the
emulated storage path the leading suspect, rather than establishing a physical
hardware diagnosis. [Microsoft bugcheck reference](https://learn.microsoft.com/windows-hardware/drivers/debugger/bug-check-0x124---whea-uncorrectable-error)

The enabled Windows AHCI boot driver was verified before a clean shutdown.
The VM configuration/NVRAM were backed up, its original disk was moved to the
existing Intel AHCI controller, and Windows booted successfully. The provisioning
recipe now uses SATA/AHCI. The post-change result directory is `sata-validation`;
short validation runs do not establish long-term stability.
The approximately 43-minute post-boot capture recorded no new storage reset,
dump-failure, bugcheck, or unexpected-restart events. The host kernel taint
remained 4100, without the kernel-oops flag.

Unity's machine-bound Personal license needed reactivation after the virtual
storage hardware changed. The existing signed-in Hub account was retained.

The crashed Inspector project's asset database was corrupted. Its first repeat
timed out, and the local wrapper initially accepted an older result file. The
timestamp audit caught this; the wrapper now rejects nonzero exits. A separate
fresh-project repeat also checks the report's completion timestamp against the
new run's start time.

## Reproduce and inspect

Run the [Visual Studio acceptance script](../../tools/VisualStudioIntegrationTestDriver/Run-Windows.ps1)
from an unlocked Windows desktop. Its [instructions](../../tools/VisualStudioIntegrationTestDriver/README.md)
list prerequisites, ownership guards, and evidence. Unity now selects the host
standalone target automatically; Windows Inspector input uses opt-in native
events with foreground/target-process checks.

Local evidence lives under
`~/.local/share/spire-windows/virtualbox/results/`: `dotnet-retry`, `dotnet-final`,
`dotnet-tools-final`, `native-initial`, `native-retry`, `secondary`,
`visual-studio-persistent`, `visual-studio-guards`, and `sata-validation`.
The local VM configuration/NVRAM backup is
`storage-backup-20261105T020940Z` beside `results`.

VirtualBox reports an unsupported `ID3D11Fence` creation (`0x80070057`) in some
Unity logs. Actual rendered checks succeeded; this is not a claim of complete
GPU-feature coverage or native GPU performance. No changes were committed.
