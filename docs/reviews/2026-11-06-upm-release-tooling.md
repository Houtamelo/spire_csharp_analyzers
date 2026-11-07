# Signed UPM release tooling and customer acceptance

The tracked release workflow exports and signs Spire, installs the exact tarball,
imports its new non-URP Quick Start sample through Package Manager, and rejects a
one-byte integrity failure. The final artifact passes on Linux and Windows in
Unity `2022.3.62f3`, `2023.2.22f1`, and `6000.5.6f1`.

Unity 6000 reports **Valid**. Unity 2022/2023 report **Unsigned** on initial
installation and **Unchecked** after reopening: they do not verify this modern
signature format. Their compatibility runs require an explicit option, preserve
those classifications, and still check the exact artifact/payload. They never
accept `Invalid` or `Error`. Signature verification on both modern platforms and
the identical artifact hash are separate evidence.

## Delivered changes

- `package-sign`: official UPM CLI invocation, required credential checks,
  redacted output, empty-output protection, source/payload comparison, manifest
  audit, and artifact hashes. Credentials remain outside the repository.
- `unity-tarball`: disposable project preparation, actual archive installation,
  strict source/version/signature checks, every installed file, manifest values,
  compiler importer exclusions/labels, real sample import, compiled APIs, and
  native JSON persistence. Unity processes have signing credentials removed from
  their environment. The resolved project lock file is retained.
- `package-tamper`: test-only one-byte README change retaining the complete
  original tar stream, headers, and signing data. Existing outputs are preserved.
- Package metadata: measured three-version Linux/Windows description, keywords,
  author/support URL, verified documentation/license URLs, `LICENSE.md`, and a
  release-candidate `CHANGELOG.md`. A public changelog link is deferred because
  its new page is not published yet; the changelog ships offline.
- Quick Start: a non-generic union and component with no URP or Test Framework
  dependency. Authored bytes/GUIDs have an explicit input inventory. The existing
  Showcase remains optional; its acceptance selects that sample explicitly.
- Documentation, inventory, listing publisher notes, and the generated storefront
  handoff reflect the completed workflow and verified public documentation URL.

The package remains `com.houtamelo.spire` version `4.7.6`, minimum
`6000.5.6f1`. Neither the shipping minimum nor dependency policy was broadened.
Commands and credential setup are in the [release guide](../upm-release.md).

## Acceptance evidence

The single final artifact is `tmp/upm-release-20261106/final-signed/com.houtamelo.spire-4.7.6.tgz`,
300,937 bytes, SHA-256
`ac2f48ac5a784a9553f2b790930491c45a3fd8a348c4f5aac3e72fd3cc394bd8`.
Official CLI `9.36.0` signs it successfully. All four product DLLs are byte-identical
to the earlier signed artifact and completed runtime/version validation evidence.

| Check | Linux | Windows |
| --- | --- | --- |
| Unity 2022.3.62f3 installation/import/behavior | Passed; legacy signature limitation | Passed; legacy signature limitation |
| Unity 2023.2.22f1 installation/import/behavior | Passed; legacy signature limitation | Passed; legacy signature limitation |
| Unity 6000.5.6f1 installation/import/behavior | Passed; signature Valid | Passed; signature Valid |
| Installed payload, each run | 115 files verified | 115 files verified |
| Imported Quick Start, each run | 5 API/assertion/persistence checks | 5 API/assertion/persistence checks |
| One changed README byte on Unity 6000 | Invalid; acceptance exits 1 | Invalid; acceptance exits 1 |
| Selected packaging/preparation .NET regressions | Included in full project below | 65 passed, no skips |

Each positive workflow starts and restarts the actual Editor. It checks empty,
value, and reference variant factories/accessors; captures the generated Unity
assertion for a wrong-variant access; and round-trips the imported component's
union through native Unity JSON serialization. All compiler DLLs retain
`RoslynAnalyzer` and disabled plugin platform imports.

The complete Linux DevTools project passes **178 tests**, with **8 explicit
Windows exclusions**. Actual Unity 6000 Editor Mono passes all **65 selected
packaging/preparation cases**, including all **9 new tarball/control cases**.
These tests use real CLI processes, package files, archives, and filesystem
fixtures. Malformed/traversing/duplicate archives, missing DLLs or credentials,
missing Quick Start inputs, unrelated output preservation, and exact one-byte
tampering are covered. Existing Unity 6000 customer acceptance still passes all
**16 Showcase tests** after adding the second sample.

Archive inventory and installation inventory are distinct: Unity consumes the
signing attestation rather than installing it as a product file. Of the 115
installed files, 114 match artifact bytes; `package.json` preserves every
artifact value and adds only Unity's cache `_fingerprint` where present.
Unity 2023.2 generates 12 hex characters; Unity 6000 generates 32. The host audit
checks the version-specific shape and compares the remaining JSON exactly.
The CLI's repository metadata addition during packing is audited separately.

`tmp/upm-release-20261106/audit.mjs` reproduces the composite audit and writes
`acceptance-summary.json`. That folder retains signed/tampered artifacts,
source inventories, manifests, test results, all earlier attempts, and Linux
logs. Windows evidence is under
`~/.local/share/spire-windows/virtualbox/results/upm-release-20261106-release-accepted/`,
`upm-release-20261106-negative/`, and `upm-release-20261106-tooling-tests-verified/`.
The Windows source snapshots exclude live examples and credentials. Test/sample
fixtures stay under `tests/`; the live `examples/` tree is not an input or output.

The complete Editor/Roslyn, rendered Inspector, player, WebGL, and IDE matrices
were not rerun here. Their four shipped DLL hashes are unchanged. This follow-up
adds delivery/sample acceptance, not a new blanket compatibility claim.

## Retained failures and corrections

- A README test depended on a line break falling outside a phrase. It now
  normalizes whitespace while retaining every wording assertion.
- The acceptance probe initially lacked an assembly definition, so its C# 10
  response file did not apply. The fixture now declares its Editor assembly.
- An initial inventory incorrectly expected the signing attestation to be an
  installed payload file. Its hash is recorded separately; every product file
  remains checked.
- The negative accessor probe initially expected an exception. The emitter
  implements a Unity assertion for this shape. The final check captures and
  requires that exact assertion, including expected and actual variant names.
- Legacy signature states and the 2023 cache fingerprint length were exposed by
  real failures. The final checks retain explicit version boundaries instead of
  reporting old Editors as signature-verified.
- The older Linux Editor could not load `libxml2.so.2`. Accepted runs use the
  existing `tmp/unity-legacy-validation-20261103/native-libs` through a scoped
  `LD_LIBRARY_PATH`; host libraries were not modified.
- Two isolated behavioral-preparation fixtures needed the new sample inventory
  copied into their owned source roots. Four Windows preparation cases also
  exposed a duplicated Linux-only Editor default; their helper now uses
  `ToolContext.DefaultEditor`. Original success/failure assertions pass on
  Windows, Linux .NET, and Editor Mono.

## Remaining work requiring the publisher

1. Access the Publisher Portal, confirm the enrolled namespace and product
   identity, and reserve the technical name. The connected browser tool reports
   no available browser; no draft was inspected or created during this work.
2. Confirm release version and minimum supported Unity version. The current
   minimum remains unchanged despite older-Editor compatibility evidence.
3. Acquire/install current **Asset Store | Publishing Tools 12.0.6** through the
   signed-in Store account. The public official GitHub repository is 12.0.0 and
   exposes only Generic/UnityPackage validation modes, so it cannot perform the
   required UPM validation. The [Store listing](https://assetstore.unity.com/packages/tools/utilities/asset-store-publishing-tools-115)
   identifies the newer release.

A clean Unity 6000 publishing project is ready at
`tmp/upm-release-20261106/publishing`. An actual Editor run registers the core as
an Embedded package and exits 0; `publishing-preparation.json` explicitly records
no installed publishing tools and **officialUpmValidationPerformed: false**.
Once tools and the reserved draft identity are available, run official UPM
Validator, fix findings, upload the candidate, and prepare review submission with
manual publication. Store acquisition/version changes require an available
product and remain separate acceptance. Unity documents the draft reservation
and tools prerequisites in its [validation workflow](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/validate).

No software was uploaded, submitted for review, or published. No additional
service-account credentials are needed for the completed signing workflow.
