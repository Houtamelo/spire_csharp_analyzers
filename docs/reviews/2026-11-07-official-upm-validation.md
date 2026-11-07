# Official UPM validation and upload handoff

Current state: the publisher confirmed submission of version **4.7.6** for
vetting on **November 7, 2026**. Unity approval and publication remain pending.
The submission is publisher-reported; the live Portal was not independently
inspected. The upload and validation evidence below precedes that submission.

The publisher acquired **Asset Store UPM Publishing Tools** on November 7, 2026.
The authenticated download now succeeds. Version **0.3.3** matches the official
registry SHA-1 `96f31e73c4371a1af58993eb7fd7f7ed6d73ef84`. The tool was installed
in the owned disposable publishing project without changing its implementation.

The official `UpmValidator` and its complete `UpmTestProvider` ran in Unity
**2022.3.62f3**, category **Tools**, against
`individual.antonio-pedro-goncalves-ferreira.spire` version **4.7.6**.
Result: **46 Pass, one Warning, zero Fail**, no compilation errors or validator
exceptions. All Generic and UPM checks remain enabled; their outcomes were not
overridden.

Evidence: `tmp/upm-minimum-20261107/publishing/official-upm-validation.json` and
`official-upm-summary.json`. The publishing tool sources are the acquired
official archive, not the older public 12.0.0 project previously inspected.

## Remaining warning

Check 104, **Check Dependencies**, reports references in the optional
`Samples~/Showcase` asmdefs that are not loaded in the clean core-only project:
the Showcase runtime/Editor assemblies, URP/Core assemblies, and NUnit. It also
reports `com.unity.modules.imgui` and `com.unity.modules.uielements` as unused.
The optional Showcase requires Unity 6000.5.6f1, URP 17.5.0, and Test Framework
1.7.0 before import. Inspector dependencies also appear in generated source,
which this package-level dependency scan does not compile as a consumer union.

The warning is retained, not converted into a pass. Existing separate validation
covers the Showcase and generated Inspector workflows. Do not make URP/Test
Framework required core dependencies solely to satisfy this scan, or remove the
UI modules needed by generated Inspector code.

## Upload candidate

`tmp/upm-minimum-20261107/publishing/spire-4.7.6.tar.gz` is a byte-identical copy
of the previously validated signed `.tgz`. The official uploader requires the
`.tar.gz` filename suffix; no repacking occurred. SHA-256:

`d4a0482bdfa3fd8707f0e442c7af698338425d6e47e201692fa6b0d4297238bf`

`upload-manifest.json` is extracted from that exact signed artifact. It declares
Unity **2022.3.62f3**, and the four product DLLs retain the previously tested
hashes. Readiness and the full warning are preserved in `upload-readiness.json`.

## Authenticated handoff

Tool acquisition resolved the authenticated HTTP 403. The separate custom
authenticated batch launch was never run following automatic approval rejection.
Upload will use the official tool's normal Editor authentication, initialized by
Unity Hub; no Hub token is injected into a custom Editor command line.

The prepared project's launch version is now **6000.5.6f1** for that native Hub
session. Unity 2022 needs a process-local libxml2 compatibility library on this
host; its batch validation above supplies that library. This project launch
choice does not change the package's minimum.

Hub's local `project:open` API acknowledged the request but its own logs reported
`MISSING_EDITOR_VERSION`. Hub's `project:get-data` nevertheless reads the correct
6000.5.6f1 version. The publisher was asked to open the prepared project using
Hub's UI. A project-scoped driver is prepared for read-only SDK inspection of
product **6893155** after that native launch. No upload or submission has occurred
at the time of this handoff.

The publisher subsequently opened the project through Hub. The first official
SDK authentication failed at TLS, as did My Assets. The
[certificate-path investigation and verified repair](2026-11-07-unity-linux-certificate-paths.md)
resolved that failure. The existing native Editor then authenticated, found
approved publisher **Houtamelo** (161892), draft **6893155**, and the exact
reserved technical package name; its manifest author matches the publisher.
No custom authentication launch was needed.

A scoped upload driver is prepared for the official SDK's `UpmPackageUploader`
and `PackageUploadingService`. Before any upload it verifies the frozen artifact
hash, version/minimum, embedded payload hashes, target publisher/product/package,
draft status, the uploader's severe-issue checks, and a fresh complete official
validation run. Only the documented Check Dependencies warning is accepted.
Commands are claimed once before execution, and the official upload service
locks assembly reload while uploading. The driver does not submit or publish
the product. The Editor needs to refresh its new scripts before that command
can be dispatched; the publisher was asked to do so.

## Completed upload

After the publisher refreshed the Editor, the scoped command ran once and all
preflight checks passed. A fresh full official validation run on
**6000.5.6f1** again reported **46 Pass, one Warning, zero Fail**, with no
compilation errors or validator exception. The original 2022 reports remain
as `official-upm-validation-2022.json` and `official-upm-summary-2022.json`.

The official `PackageUploadingService.UploadUpmPackage` returned **Success**
after upload reached 100 percent and the SDK's complete-upload request finished.
Its subsequent SDK read still finds the correct technical package under draft
6893155. Uploaded version: **4.7.6**, minimum **2022.3.62f3**, unchanged artifact
SHA-256 `d4a0482bdfa3fd8707f0e442c7af698338425d6e47e201692fa6b0d4297238bf`.

Receipt and preflight journal:

- `tmp/upm-minimum-20261107/publishing/automation-upload-4-7-6-20261107-1.json`
- `tmp/upm-minimum-20261107/publishing/upload-upload-4-7-6-20261107-1-journal.json`

The live Portal browser page and its individual version rows were not inspected.
Upload completion is established by the official SDK's successful service
response, not by assuming that the prior draft package binding proves a new
version exists. The agent has not submitted the product for approval or
published it. The persistent local CA update hook is installed and verified;
see the linked certificate-path report.

## Portal compatibility is a separate metadata step

The publisher sees only `(uploaded from) 6000.5.6f1` in the build's compatibility
metadata. This is consistent with Unity's
[documented default](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/create-draft):
the upload Editor is selected initially, with additional compatible major.minor
versions entered separately.

Inspection of the acquired 0.3.3 uploader confirms its create-version payload
has three fields: `publisherId`, `uploadedFromUnityVersion` (from
`Application.unityVersion`), and `manifest`. Its upload settings expose publisher,
artifact path, and manifest, with no supported-version-list field. The exact
uploaded manifest contains `unity: "2022.3"` and `unityRelease: "62f3"`; that
minimum is not the Portal's compatibility matrix. An SDK Success response did
not establish that the Portal compatibility list had been configured.

The handoff now explicitly recommends **2022.3, 2023.2, 6000.5**, grounded in the
three exact validated Editors, and records Portal configuration as pending.
Keep the actual uploaded-from version. This is a separate Portal metadata edit;
no package modification or re-upload was performed for this follow-up.

The publisher subsequently confirmed product submission for vetting. This does
not independently establish which compatibility families were entered in the
Portal; the local record retains that boundary.
