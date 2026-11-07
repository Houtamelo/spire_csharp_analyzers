# UPM signing and tarball installation

Service-account signing succeeds. Unity `6000.5.6f1` on Linux installs the signed
Spire `4.7.6` tarball and reports **Valid**, with reason `Signature is ok`. A
one-byte README payload change reports **Invalid**, with reason
`Package file integrity check failed`, and the same acceptance probe exits 1.

This validates signing credentials, package integrity, and local-tarball
installation. Store Validator acceptance, Portal reservations, uploading,
publication, and Store acquisition remain separate. No package minimum/version,
production code, or shipped dependency policy changed.

## Evidence

| Check | Result |
| --- | --- |
| UPM CLI | Official Linux x64 `9.36.0`, download SHA-256 checked against Unity's CDN |
| Service-account signing | CLI exits 0 and produces `com.houtamelo.spire-4.7.6.tgz` |
| Product DLL provenance | All four DLLs match the completed three-version Web validation matrix |
| Archive contents | 103 exported files verified; all original manifest values retained, with the CLI's repository metadata addition checked explicitly |
| Actual Unity install | `PackageInfo.source` is `LocalTarball`; package version is `4.7.6` |
| Installed payload | 102 files match archive bytes; manifest JSON preserves all artifact values and adds only Unity's `_fingerprint` cache field |
| Installed signature | `Valid`; no Package Manager package errors |
| Compiler plugin imports | All three compiler DLLs retain `RoslynAnalyzer` and remain excluded from Editor/Any-platform plugin imports |
| Consuming code | Unity compiles generated union factories/accessors; integer and reference payload assertions pass |
| Tampered payload control | Identical tar headers/signing metadata, one changed README byte: signature `Invalid`, probe exits 1 |

The standalone CLI is installed at `~/.upm/bin/upm`; shell startup files were not
changed. Its official archive SHA-256 is
`a7c00db477daa4161f24bf0af5d2589e88f3e6276f9798d44bae601d957dbe04`.
Credentials remain in `~/.config/spire/upm-signing.env` with mode `0600`; the
signing log redacts configured credential/organization values. Unity installation
does not receive those credential environment variables.

The package folder comes from the existing packager with unchanged, previously
validated Release DLLs. The CLI adds a `repository` object whose URL and revision
match the repository at `060db4f`. Unity adds `_fingerprint` on installation.
Initial byte-equality checks exposed both transformations; final checks allow
only these specific additions and preserve every other payload assertion.

The first negative-control attempt unpacked/repacked with ordinary `tar`, which
lost signing information and was classified `Unsigned`. That result is retained
but is not the tampering proof above. The final control edits the decompressed
archive buffer directly, preserving headers and all trailing signing data, then
recompresses it. Unity classifies that artifact `Invalid`.

Evidence and disposable projects are under `tmp/upm-signing-20261106/`:

- `signed/com.houtamelo.spire-4.7.6.tgz`: valid signed artifact.
- `archive-audit.json` and `manifest-install-normalization.json`: source/archive
  identity and the explicitly checked manifest transformations.
- `customer/tarball-acceptance.json`: successful Unity signature, plugin-import,
  generated-code, and installed-file checks.
- `negative/tarball-acceptance.json`: final intentional integrity failure.
- `acceptance-summary.json`: composite acceptance and artifact hashes.
- `signing.log`, `signed-install-final.unity.log`, and
  `tampered-payload.unity.log`: retained signing and Editor evidence.

The probes and orchestration are disposable validation code, not yet a tracked
release automation feature. Inputs stay outside `examples/`. The existing full
runtime/Inspector/player suites were not rerun: their product DLL hashes match,
while this run establishes the new packaging and signature boundaries.

Unity documents the [service-account role and environment variables](https://docs.unity.com/en-us/engine/6000.5/manual/packages-list/upm-signature-lp/upm-command-line/upm-cli-install),
the [pack/sign command](https://docs.unity.com/en-us/engine/6000.5/manual/packages-list/upm-signature-lp/upm-command-line/upm-cli-pack),
and [signature status behavior](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/upm-signature-lp/upm-signature).
