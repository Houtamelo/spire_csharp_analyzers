# Build and validate a signed Unity package

Use `Spire.Tool` to export, sign, and install the exact release artifact. Signing
credentials stay in the local environment; only the signing command needs them.
The [release validation report](reviews/2026-11-06-upm-release-tooling.md) records
the measured Editor/platform boundaries and remaining publishing work.

## Local release candidate

Build the CLI and package from a reviewed source checkout:

```sh
dotnet build tools/Spire.Tool/Spire.Tool.csproj -c Release
dotnet tools/Spire.Tool/bin/Release/net10.0/Spire.Tool.dll package \
  --output tmp/release/package/com.houtamelo.spire
```

In a separate shell scope, load the private credentials file and sign. Disable
shell tracing before loading credentials. The installed UPM CLI can be selected
with `--upm`; it need not be on PATH.

```sh
(
  set +x
  source "$HOME/.config/spire/upm-signing.env"
  dotnet tools/Spire.Tool/bin/Release/net10.0/Spire.Tool.dll package-sign \
    --package tmp/release/package/com.houtamelo.spire \
    --output tmp/release/signed --upm "$HOME/.upm/bin/upm"
)
```

The file defines `UPM_SERVICE_ACCOUNT_KEY_ID`, `UPM_SERVICE_ACCOUNT_KEY_SECRET`,
and `UPM_ORGANIZATION_ID`. Do not commit it. Signing output must be a new or empty
directory. The command preserves a redacted `signing.log` and an `artifact.json`
containing the artifact/payload hashes. It checks all source bytes, allowing
only the official CLI's checked repository metadata addition to the manifest.

Install and validate the actual `.tgz` in an owned disposable project:

```sh
dotnet tools/Spire.Tool/bin/Release/net10.0/Spire.Tool.dll unity-tarball \
  --package tmp/release/signed/com.houtamelo.spire-4.7.6.tgz \
  --project tmp/release/customer-6000 \
  --editor /path/to/6000.5.6f1/Editor/Unity
```

Use the version produced by the build in the artifact filename. On Windows the
Editor path ends in `Editor/Unity.exe`. This command inspects every installed
payload file, compares manifest values, verifies analyzer import settings,
imports Quick Start through Package Manager, and runs its compiled generated
APIs, negative accessor assertion, and native JSON persistence check. The lock
file and reports remain in the project. Unity processes have signing credentials
removed from their environment. Linux runs use an owned Xvfb display.

For Unity `2022.3.62f3` and `2023.2.22f1`, pass
`--allow-unsupported-signature`. These Editors install and run this modern signed
artifact but report `Unsigned` or `Unchecked`; this is compatibility evidence,
not signature verification. The option never accepts `Invalid` or `Error`, and
does not relax Unity 6000's signature requirement. Validate the same artifact
hash on Unity 6000 as well. Older Linux Editors may need a process-local
`LD_LIBRARY_PATH` supplying their missing `libxml2.so.2` dependency.

Preparation alone uses `--prepare-only`, without starting an Editor. Inputs and
outputs inside the repository's live `examples/` directory are rejected.

## Integrity failure control

Create a test-only artifact with exactly one README byte changed. The command
retains the complete decompressed tar stream, including its original signing
data; it does not unpack and repack the archive.

```sh
dotnet tools/Spire.Tool/bin/Release/net10.0/Spire.Tool.dll package-tamper \
  --package tmp/release/signed/com.houtamelo.spire-4.7.6.tgz \
  --output tmp/release/tampered.tgz
dotnet tools/Spire.Tool/bin/Release/net10.0/Spire.Tool.dll unity-tarball \
  --package tmp/release/tampered.tgz --project tmp/release/negative \
  --editor /path/to/6000.5.6f1/Editor/Unity
```

The second command must fail with an `Invalid` signature and package integrity
failure in `tarball-install.json`. Unity can install an invalid package while
flagging it; our acceptance rejects it. Never upload the tampered artifact.

## Publisher Portal handoff

1. Confirm the enrolled publisher namespace and reserve the technical name in a
   single-package product draft. The current local name is `com.houtamelo.spire`.
2. Choose the release version and declared minimum. The package remains `4.7.6`
   with minimum `6000.5.6f1`; older-Editor validation has not changed that policy.
3. Install current **Asset Store | Publishing Tools** from Unity's official
   Store listing in the clean publishing project. The public GitHub 12.0.0
   source lacks UPM validation mode; it cannot substitute for Store 12.0.6.
4. Run **Window → Tools → Asset Store → Validator**, Validation Type **UPM**.
   Preserve its report and resolve findings before upload. Use Uploader's UPM
   Packages tab, then complete the Portal listing and approval submission.
5. Choose manual publication for the first release. Once Store acquisition is
   available, verify My Assets installation and version changes separately.

Unity requires a reserved draft/package identity before official UPM validation
and upload. See [Unity's validation workflow](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/validate)
and [official Publishing Tools](https://assetstore.unity.com/packages/tools/utilities/asset-store-publishing-tools-115).
Local signing and customer acceptance do not establish Store approval.
