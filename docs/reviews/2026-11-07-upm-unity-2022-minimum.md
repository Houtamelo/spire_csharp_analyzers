# Unity 2022 package minimum and upload preparation

The publisher requested a Unity 2022 minimum on November 7, 2026. The exporter
now writes `unity: "2022.3"` and `unityRelease: "62f3"`, matching the oldest
validated Editor, **2022.3.62f3**. Earlier Unity 2022 patches are not claimed.

The optional Showcase still requires Unity 6000.5.6f1, URP 17.5.0, and Unity Test
Framework 1.7.0. Its Package Manager description now states that Unity version.
Quick Start remains independent of those packages. Unity 2022's native
serialization limitation for generic unions remains documented.

Package docs, changelog, installation guide, storefront copy, and the generated
handoff agree on the minimum. The handoff builder reads the exporter fields
rather than maintaining a second hard-coded minimum. Package version remains
4.7.6; C# namespaces and the four product DLLs are unchanged.

## Verification

Evidence root: `tmp/upm-minimum-20261107/`.

- The actual package-export integration test first failed with expected
  `2022.3`, actual `6000.5`. After the change, all 14 packaging/UPM cases pass.
- Full Linux DevTools run: 179 passed, eight Windows-platform exclusions.
- Changed C# files were formatted with `dotnet format`; scoped diff checks pass.
- The updated 4.7.6 candidate is signed with the official UPM CLI. Its SHA-256 is
  `d4a0482bdfa3fd8707f0e442c7af698338425d6e47e201692fa6b0d4297238bf`.
- Linux Unity 2022.3.62f3 and 6000.5.6f1 install that same artifact, verify all 115
  payload files, import Quick Start, and pass all five generated API/assertion/
  native JSON checks after reopening the Editor. Installed manifests retain the
  new minimum. Unity 6000 reports a Valid signature; Unity 2022 reports
  Unsigned/Unchecked and does not cryptographically verify this signature format.
- All four DLL hashes match the previous six-run identity candidate. The complete
  Editor/player/browser/IDE matrices and Windows runs were not repeated for this
  manifest/documentation change.

Candidate:
`tmp/upm-minimum-20261107/signed-final/individual.antonio-pedro-goncalves-ferreira.spire-4.7.6.tgz`.
The initial signing attempt could not write the sandbox-visible host UPM cache;
retrying with an owned `/tmp` UPM cache succeeded. No host cache permissions or
signing credentials were changed.

## Upload preparation

Unity's current [validation and upload guide](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/validate)
links to [Asset Store UPM Publishing Tools](https://assetstore.unity.com/packages/tools/asset-store-upm-publishing-tools-5368745),
`com.unity.upm-publishing-tools` 0.3.3. The official registry also reports that
version. This supersedes the older Publishing Tools listing previously used in
our current handoff instructions; the historical 12.0.0 source investigation
does not establish validation with the new tool.

A clean Unity 2022.3.62f3 project is prepared at
`tmp/upm-minimum-20261107/publishing`, containing the matching embedded Spire
package and a dependency on the official publishing tool. The direct download
returned HTTP 401. An actual Unity Editor resolution attempt reported:

> Cannot install package. You are not signed in. Please sign in using the Hub and retry.

The publisher was already signed into Unity Hub. Follow-up inspection of Hub's
authenticated local API confirms a logged-in, valid account with an access
token. Our direct batch launch did not pass Hub's normal authentication handoff.
An authenticated tool download instead returns HTTP 403 with `Not allowed to
download the com.unity.upm-publishing-tools package`. Store acquisition/access
must be checked; another Hub login is not established as necessary. A separate
legacy cache permission error also needs to be isolated from package access.
Evidence is under `tmp/upm-auth-20261107/`; account tokens are omitted from its
JSON reports.

Automatic approval review rejected a custom authenticated batch launch because
passing a live Hub token on the Editor command line could expose the session
credential to process inspection. That launch did not run. Use Hub's normal
project launch flow instead of injecting account tokens into a custom launcher.

At this point the tool has not installed, official UPM validation has not run,
and no version has uploaded. Use the enrolled publisher account to acquire the
free official tool if needed. No credentials should be sent in chat.

Follow-up: the publisher acquired the tool. Its authenticated download now
succeeds, and all 47 official checks ran (46 passes, one dependency warning,
zero failures). See the [official validation and upload handoff](2026-11-07-official-upm-validation.md)
for the current state; the preceding failures remain historical evidence.

The previous description-preview issue was resolved by the publisher. Draft:
<https://cloud.unity.com/assetstore/publisher/products/6893155>.
