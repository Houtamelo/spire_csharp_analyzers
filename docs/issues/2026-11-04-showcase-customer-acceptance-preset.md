# Showcase preset differs from customer-acceptance expectations

Resolved for automated acceptance on November 5 by isolating its sample inputs
under `tests/UnityPackageSample`. The interactive examples remain unchanged.
Fresh Windows installation/import/verification passed, followed by all 16 sample
Editor/Play Mode tests. See the [fixture isolation verification](../reviews/2026-11-05-test-fixture-isolation.md).
The observations below describe the original coupled run.

Windows Unity 6000.5.6f1 successfully installed the exported package through
Package Manager and imported its sample. Customer acceptance then failed while
capturing the authored preset. The checked-in sample and its exported copy
contain different values from the fixture's canonical `ResetExamples` snapshot.

| Value | Acceptance expects | Authored sample contains |
|---|---|---|
| Selected Move destination | `(3, 4)` | `(5, 5)` |
| Selected Move speed | `2` | `10` |
| Command sequence | Idle, Move, Speak, ApplyEffect, ApplyEffect, ApplyEffect | Move, Idle, Speak, Speak, ApplyEffect, ApplyEffect |
| Rectangle dimensions | `(3, 4)` in the reset helper | `(2554, 35)` |

The fixture accesses `commands[3].applyEffect` before validating the sequence.
That entry is `Speak`, so the generated accessor correctly throws
`InvalidCastException`; reflection wraps it in `TargetInvocationException`.
This is not evidence of a Windows serialization regression.

The relevant inputs are
`examples/UnityShowcase/Assets/Examples/ShowcasePreset.asset`,
`examples/UnityShowcase/Assets/Scripts/ShowcasePreset.cs`, and
`tests/UnityCustomerAcceptance/Assets/Editor/CustomerAcceptance.cs`.
The dedicated fixture now contains canonical reset data and supplies package
exports. Acceptance verifies the exported assets as imported; it does not reset
the imported sample to hide discrepancies in a shipped asset.

A separate callback-count timeout in the acceptance fixture was fixed and
verified with real Unity polling and expired-deadline checks. The later import
reached this preset check. The failure remains recorded, rather than weakening
the expected values or changing unrelated sample assets.

Local evidence:
`~/.local/share/spire-windows/virtualbox/results/sata-validation/windows-customer-sata-fixed/`
and `sata-validation/customer-debug/`. The run used an owned source snapshot
including uncommitted work, not a release commit.
