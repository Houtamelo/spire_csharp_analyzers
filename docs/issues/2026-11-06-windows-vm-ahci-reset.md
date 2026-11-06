# Windows VM AHCI reset during validation

The Windows Unity matrix passes, but the guest records one `storahci` event 129
at **2026-11-06 06:57:49.517 UTC**:

```text
Reset to device, \Device\RaidPort0, was issued.
```

VirtualBox's current `VBox.log` also records an AHCI port 0 reset at VM elapsed
`01:17:08.446114`. It records another port reset at `00:10:48.053723`; no second
guest event 129 appears in the collected system-event set. Controller resets
logged during the first seconds of VM startup are separate observations.

The final collection also contains events 41 and 6008 at this boot's startup
(`05:40:42` and `05:40:43` UTC). Their message describes the previous unclean
shutdown on November 5; they do not establish a new crash during this validation.

All accepted test processes complete and the verified source/package hashes
match. This supports the recorded package results, not a claim of storage
stability. The reset's cause, connection to the physical SSD or virtual
controller, and recurrence remain unproven. No controller, host driver, disk
mount, or passthrough setting is changed during this investigation.

Evidence: `final/final-provenance.json` beneath
`~/.local/share/spire-windows/virtualbox/results/windows-matrix-20261106/`, plus
`machines/Spire Windows VirtualBox/Logs/VBox.log` beside that setup's `results/`.
Correlate guest and host storage logs in a separate investigation before treating
the VM as a stable unattended CI runner.
