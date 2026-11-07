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
  --output tmp/release/package/individual.antonio-pedro-goncalves-ferreira.spire
```

In a separate shell scope, load the private credentials file and sign. Disable
shell tracing before loading credentials. The installed UPM CLI can be selected
with `--upm`; it need not be on PATH.

```sh
(
  set +x
  source "$HOME/.config/spire/upm-signing.env"
  dotnet tools/Spire.Tool/bin/Release/net10.0/Spire.Tool.dll package-sign \
    --package tmp/release/package/individual.antonio-pedro-goncalves-ferreira.spire \
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
  --package tmp/release/signed/individual.antonio-pedro-goncalves-ferreira.spire-4.7.6.tgz \
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
  --package tmp/release/signed/individual.antonio-pedro-goncalves-ferreira.spire-4.7.6.tgz \
  --output tmp/release/tampered.tgz
dotnet tools/Spire.Tool/bin/Release/net10.0/Spire.Tool.dll unity-tarball \
  --package tmp/release/tampered.tgz --project tmp/release/negative \
  --editor /path/to/6000.5.6f1/Editor/Unity
```

The second command must fail with an `Invalid` signature and package integrity
failure in `tarball-install.json`. Unity can install an invalid package while
flagging it; our acceptance rejects it. Never upload the tampered artifact.

## Publisher Portal handoff

1. The publisher confirmed a single-package draft on November 7, 2026:
   `individual.antonio-pedro-goncalves-ferreira.spire`, product namespace `spire`.
   The exporter and acceptance fixtures now match the
   [recorded identity](../marketing/storefront/portal-draft.json). The previous
   private name was `com.houtamelo.spire`; remove that local package before adding
   the new identity to an existing project, to avoid duplicate Spire DLLs.
2. The package remains `4.7.6`; the publisher selected `2022.3.62f3` as its minimum
   on November 7, 2026. The optional Showcase still requires Unity 6000.5.6f1,
   URP 17.5.0, and Unity Test Framework 1.7.0. Quick Start needs neither package.
3. Install **Asset Store UPM | Publishing Tools** (`com.unity.upm-publishing-tools`)
   from the current upload guide's official Store listing in a clean publishing
   project. The listing reports version 0.3.3 on November 7, 2026. The historical
   public GitHub 12.0.0 source lacks UPM validation mode and is not this package.
4. Run **Window → Tools → Asset Store → Validator**, Validation Type **UPM**.
   Preserve its report and resolve findings before upload. Use Uploader's UPM
   Packages tab, then complete the Portal listing and approval submission.
   In package metadata, set supported Unity families **2022.3, 2023.2, 6000.5**.
   The default uploaded-from entry records the actual Editor used for upload;
   uploader 0.3.3 does not send this supported-version list. The uploaded manifest
   separately declares the minimum as 2022.3.62f3. No re-upload is needed to edit
   Portal compatibility. Preserve the uploaded-from provenance.
5. Choose manual publication for the first release. Once Store acquisition is
   available, verify My Assets installation and version changes separately.

Unity requires a reserved draft/package identity before official UPM validation
and upload. See [Unity's validation workflow](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/validate)
and [official UPM Publishing Tools](https://assetstore.unity.com/packages/tools/asset-store-upm-publishing-tools-5368745).
Local signing and customer acceptance do not establish Store approval.

The [November 7 official validation](reviews/2026-11-07-official-upm-validation.md)
records the acquired 0.3.3 tool's complete run against the current 4.7.6
candidate: 46 passes, one retained dependency warning, zero failures. The exact
signed 4.7.6 artifact has uploaded successfully through the official SDK from
the Hub-authenticated Editor. On November 7, 2026, the publisher confirmed that
the product was submitted for vetting. Unity approval and publication remain
pending. Keep the optional-Showcase/generated-code warning explanation with the
submission evidence.
