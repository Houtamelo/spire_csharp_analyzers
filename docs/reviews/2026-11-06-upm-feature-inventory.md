# UPM feature inventory for Spire

Start with one Spire package, retain optional samples, and add release metadata,
signed tarballs, and Unity's publishing validation. A multi-package product is a
useful later option for independently installable integrations or a Showcase with
automatic URP dependencies; it is not required to ship the current core.

Researched November 6, 2026 against Unity's official documentation and current
repository code at `060db4f`. Enrollment is **accepted**, as confirmed by the
publisher today. The subsequent [signing validation](2026-11-06-upm-signing-validation.md)
uses the configured service account and organization to sign and install a real
artifact. Enrolled namespace, product reservations, and account-specific
publishing settings remain unchecked. Recommendations below remain proposals
except where the signing follow-up records completed work. No Portal draft or
software upload was created.

## Existing foundation

The [packager](../../tools/Spire.Tool.Core/PackageWorkflow.cs) exports four DLLs,
deterministic plugin metadata/GUIDs, runtime XML tooltips, licensing/notices,
`Documentation~`, and one optional `Samples~/Showcase` entry. Only IMGUI and UI
Elements modules are dependencies. URP 17.5.0 and Test Framework 1.7.0 are manual
sample prerequisites. Compiler DLLs carry the `RoslynAnalyzer` label and are
excluded from runtime imports; that policy must survive every distribution route.

The [customer acceptance fixture](../../tests/UnityCustomerAcceptance/Assets/Editor/CustomerAcceptance.cs)
uses `Client.Add("file:" + packagePath)` and Package Manager's sample API. Its
workflow covers fresh installation and replacement from a local package folder.
The separate signing follow-up covers signed-tarball installation and integrity
on Unity 6000.5.6f1. Store acquisition, registry authentication, and Portal approval
remain separate gates. Existing runtime/version results remain in the
[compatibility guide](../unity.md).

## Publishing and delivery

| Feature | Use for Spire | Current state and recommendation | Official documentation |
| --- | --- | --- | --- |
| Publisher/product/package namespaces | Stable identity for releases and dependencies. | Enrollment is accepted. Confirm the enrolled prefix and reserve the product technical name before treating local `com.houtamelo.spire` as final. Folder and manifest names must match the reservation. | [Publishing workflow](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/publish) |
| One product containing multiple packages | Sell one product with optional integrations, a Showcase package, or distinct compatibility requirements. Each package has its own version history. | Start with the current single package. Consider a Showcase package depending on core, URP, and Test Framework if automatic setup becomes valuable. | [Product structure](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/create-draft) |
| Package compatibility metadata | Declare supported Editor families and optionally render pipelines/build targets. | Enter only validated combinations. Keep the core independent of URP; a URP sample does not make the core URP-only. Choose the shipping minimum separately. | [Compatibility settings](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/create-draft) |
| UPM Validator and Uploader | Catch Store packaging issues and upload the real package. | Run in a clean publishing project with Validation Type UPM. The documented package size limit is 550 MB. This is additional to our behavioral acceptance. | [Validate and upload](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/validate) |
| Approval with manual publication | Complete review while controlling launch timing. | Prefer manual publication for the first release so documentation and media can be finalized before launch. Enrollment acceptance does not approve the product. | [Submission modes](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/submit) |
| Versioned updates and metadata-only revisions | Deliver fixes through Package Manager and update listing/media separately. | Keep the technical name stable, publish unique package versions, and attach compatibility/release notes to each release. | [Product updates](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/update) |
| My Assets installation and version selection | Give customers the native Store-to-Editor install flow. | Add acceptance through an acquired Store product after it is available. Unity documents this flow in 2022.3 and 2023.2 too; our local-folder tests have not exercised it. | [Unity 2022.3](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-ui-install2.html), [Unity 2023.2](https://docs.unity3d.com/2023.2/Documentation/Manual/upm-ui-install2.html) |
| Signed `.tgz` plus standalone UPM CLI | Produce an installable artifact for release automation and direct validation. | CLI 9.36.0 installed; service-account signing and a real tarball install pass on Unity 6000.5.6f1. Integrating these steps into the tracked release tooling remains work. | [CLI prerequisites](https://docs.unity.com/en-us/engine/6000.5/manual/packages-list/upm-signature-lp/upm-command-line/upm-cli-install), [Pack/sign](https://docs.unity.com/en-us/engine/6000.5/manual/packages-list/upm-signature-lp/upm-command-line/upm-cli-pack) |
| Signature status in Package Manager | Give consumers integrity/provenance information. | The valid artifact reports Valid; a one-byte payload change reports Invalid and fails acceptance. Signing is independent of runtime compatibility. | [Package signatures](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/upm-signature-lp/upm-signature) |

The standalone CLI runs without an Editor and supports the Editor's platforms,
including Linux. Its signing credentials can come from CI secrets via
`UPM_SERVICE_ACCOUNT_KEY_ID` and `UPM_SERVICE_ACCOUNT_KEY_SECRET`; there is no need
to put a personal Unity password in our command line. The documented command is
`upm pack <package-directory> --organization-id <id> --destination <path>`.
This packs/signs; Store validation and uploading remain separate steps in the
documented publishing workflow. [CLI workflow](https://docs.unity.com/en-us/engine/6000.5/manual/packages-list/upm-signature-lp/upm-command-line/upm-cli),
[Credentials](https://docs.unity.com/en-us/engine/6000.5/manual/packages-list/upm-signature-lp/upm-command-line/upm-cli-install),
[Command](https://docs.unity.com/en-us/engine/6000.5/manual/packages-list/upm-signature-lp/upm-command-line/upm-cli-pack).

## Package contents and development

| Feature | Use for Spire | Current state and recommendation | Official documentation |
| --- | --- | --- | --- |
| Manifest display/search/contact metadata | Improve discovery and the Package Manager details panel. | Add useful `keywords`, public author/support information, and refresh the description: it currently mentions only Unity 6000.5/Linux. | [Manifest fields](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/upm-manifest-pkg) |
| Documentation, changelog, and license links | Make release guidance accessible from Package Manager, including offline fallbacks. | Offline guides exist. Add a package `CHANGELOG.md`, provide `LICENSE.md` for the license-link fallback, and add `documentationUrl`, `changelogUrl`, and `licensesUrl` once their public targets work. Preserve notices and XML tooltips. | [Manifest links/fallbacks](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/upm-manifest-pkg) |
| `unity` and `unityRelease` minimum | Filter incompatible package versions. | Currently `6000.5` + `6f1`. Older versions have validation evidence, but changing the published minimum remains a release decision. | [Minimum version fields](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/upm-manifest-pkg) |
| Transitive package dependencies | Automatically install packages that every consumer needs. | Existing Unity-module dependencies are appropriate. Keep URP/Test Framework out of core dependencies. A separate integration package can declare its own required dependencies. | [Dependency resolution](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/dependencies-lp/upm-dependencies) |
| Multiple optional `Samples~` entries | Offer small learning paths without compiling them on installation. | Keep the Showcase. Add a render-pipeline-independent Quick Start sample for the three tested Editor versions; JSON or Burst examples could be separate entries when useful. Samples are copied into `Assets` on import. | [Package samples](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/cus-samples) |
| Conventional layout and stable `.meta` files | Preserve serialized asset references across package updates. | Already largely present. Keep authored GUIDs, deterministic generated GUIDs, and the explicit input allowlist. Imported sample assets still need their authored metadata even though Unity ignores `Samples~` itself. | [Package layout](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/cus-layout) |
| Editor/runtime assembly definitions and `versionDefines` | Isolate future source-based integrations and compile optional APIs when a dependency is present. | Useful for new adapters, not a reason to replace the current precompiled DLL/plugin policy. Assembly conditions do not install missing packages. | [Assembly definitions](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/cus-asmdef) |
| Package tests enabled through project `testables` | Let advanced users run focused smoke tests against the installed package. | Optional later addition. Put opt-in tests under package `Tests` with test asmdefs; do not ship our full Roslyn/SDK subprocess harness as a consumer dependency. Existing sample tests remain sample content. | [Package tests](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/cus-tests) |
| SemVer and version-specific migration notes | Communicate breaking API/asset changes and support controlled upgrades. | Existing package project versions are synchronized. Add a customer changelog and exercise preserved assets through upgrade and downgrade candidates. Stable GUIDs are part of compatibility. | [Package versioning](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/upm-semver) |
| Project lock files | Reproduce dependency resolution in customer-acceptance projects. | Preserve/audit resolved `Packages/packages-lock.json` in acceptance evidence alongside the package/DLL hashes. This is a project file, not a package payload file. | [Lock files](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/dependencies-lp/upm-conflicts-auto) |
| Local folders, tarballs, and Git URLs | Support development, testing, and alternate distribution. | Local-folder acceptance exists; signed-tarball installation now passes its separate 6000.5.6f1 probe. A generated package-only Git branch/repository is a later option; the current source repository is not itself the built UPM bundle. | [Distribution methods](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/cus-share) |
| Authenticated scoped registries | Deliver internal or beta packages independently of the Store. | Defer unless frequent private releases justify hosting and authentication. Enrollment does not require us to operate a registry. | [Scoped registries](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/upm-scoped) |

## Publisher operations

| Feature | Use for Spire | Recommendation | Official documentation |
| --- | --- | --- | --- |
| Adoption and sales analytics, CSV export | Inspect acquisition, installs by Unity version, and installs by package version. | Use these signals after launch to prioritize support and testing. They measure installs, not correctness or game runtime behavior. | [Analytics](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/analytics) |
| Product reviews and publisher replies | Track customer feedback and explain fixes. | Use the Portal alongside the existing issue tracker; no custom review tooling needed. | [Reviews](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/reviews) |
| Paid products, sales/payouts and refunds | Support the selected paid distribution model. | Current unversioned docs describe paid UPM sales. Some 6000.3 pages still say free-only; use current publishing guidance and confirm the actual draft's commercial settings. Enrollment acceptance alone is not a check of those settings. | [Revenue](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/revenue), [Payout setup](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/revenue/payouts), [Conflicting versioned page](https://docs.unity.com/en-us/engine/6000.3/manual/assets-and-media/asset-store/publishing/upm/validate) |

## Boundaries that affect our design

- The manifest minimum is not a complete compatibility matrix or an upper bound.
  It cannot express our Unity 2022 generic-serialization restriction or the known
  6000.6 generic-reference crash. Keep those in the listing, guides, and acceptance
  matrix even after configuring Portal compatibility.
  [Manifest semantics](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/upm-manifest-pkg).
- The documented sample schema has no dependency field. Importing a sample is a
  file copy, not a declaration of its URP/Test Framework prerequisites. Keep the
  manual instructions, make a minimal sample, or use a separately installable
  package when automatic dependencies are required. Imported samples are customer
  assets; package updates should not silently overwrite edits.
  [Sample schema](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/upm-manifest-pkg),
  [Import behavior](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/cus-samples).
- Package dependencies use specific SemVer strings, not npm-style ranges. The
  resolver can select a different version because of other requirements, and
  only one version of a package can be installed in a project. Dependencies are
  therefore not an exact pin of the consumer's whole environment.
  [Manifest syntax](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/cus-pkg-development/upm-manifest-pkg),
  [Resolution](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/dependencies-lp/upm-dependencies).
- Product/package splitting does not select DLLs by IDE or fix compiler-host
  compatibility. Keep the current Roslyn baseline, analyzer labels, and player
  exclusions. The observed 2022/2023/6000 behavior remains an independent gate.
- Signing validates origin/integrity, not behavioral compatibility. Store
  validation likewise does not replace our Editor, rendered Inspector,
  serialization, Mono/IL2CPP, WebGL, or IDE checks. Preserve the failing controls
  and known version-specific restrictions.

## Suggested implementation order

1. Confirm enrolled namespace and product identity, choose the declared minimum,
   and retain one core package initially. No need to split the four DLLs.
2. Complete manifest metadata, license/documentation links and changelog. Add a
   minimal sample that runs without URP on each validated Editor family.
3. Add a separate pack/sign step using the standalone CLI and scoped service
   account. Keep credentials outside the repository. Validate the exact signed
   artifact by installing it, inspecting hashes/import settings, and running the
   relevant existing integration gates.
4. Run Unity's UPM Validator, then use Uploader and Portal metadata for the release
   candidate. Exercise Store installation/version changes when available. Select
   manual publication when preparing the first reviewed release.
5. Revisit a Showcase/integration package split only if it removes enough manual
   setup to justify another version history and test matrix. Use Portal analytics
   after release to inform that decision.

All automated preparation and acceptance inputs must remain under `tests/`, with
outputs in disposable locations outside `examples/`.
