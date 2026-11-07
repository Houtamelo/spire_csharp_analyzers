# Spire Asset Store submission handoff

Storefront images and listing copy, collected on 2026-11-07 (UTC). Cover,
card, icon, and seven gallery slides retain their approved revisions. The new
1200 x 630 social image is an optional draft using the existing brand design.
Regular price: **20 USD**, chosen by the publisher. The publisher already has an
Asset Store profile. UPM enrollment is accepted, as confirmed by the publisher on
November 6, 2026. Windows / Visual Studio and three-version WebGL validation are
complete within the listing's documented scope. Video is being handled in a
separate session.

## Publisher Portal fields

| Field | File or value |
|---|---|
| Product name | [listing/title.txt](listing/title.txt) |
| Summary | [listing/summary.txt](listing/summary.txt) |
| Description | Open [listing/upm-description.html](listing/upm-description.html) in a browser; select and copy its rendered description, including technical details and support |
| Technical details | [listing/technical-details.md](listing/technical-details.md) |
| Documentation and support | [listing/documentation-and-support.md](listing/documentation-and-support.md) |
| Keywords | [listing/keywords.txt](listing/keywords.txt) |
| Regular price | 20 USD |
| Category proposal | Tools/Utilities |
| Cover | [images/cover.png](images/cover.png), 1950 x 1300, cover revision 2 |
| Card | [images/card.png](images/card.png), 420 x 280, card revision 3 |
| Icon | [images/icon.png](images/icon.png), 160 x 160 |
| Gallery | Seven files in images/gallery, ordered 01 through 07, revision 3 |
| Social media (optional) | [images/social.png](images/social.png), 1200 x 630, draft v1 |
| Publisher namespace | individual.antonio-pedro-goncalves-ferreira |
| Product namespace | spire |
| Portal package technical name | individual.antonio-pedro-goncalves-ferreira.spire |
| Portal compatible Unity versions | 2022.3, 2023.2, 6000.5; enter separately after upload |

For Description, open the HTML file in a browser, click **Select description**,
press Ctrl+C, then paste with Ctrl+V (normal paste, with formatting) into the
Portal's rich-text editor. If the editor offers an explicit HTML/source mode,
use **Select HTML source** on that page, or the contents of
[listing/upm-description-fragment.html](listing/upm-description-fragment.html),
in that mode. Do not paste HTML source into a visual editor or raw Markdown into
either mode. The Markdown file remains the editable source copy;
[listing/upm-description.txt](listing/upm-description.txt) is an unformatted fallback.

The description uses only the p, strong, em, a, and br tags documented in
[Unity's Asset Store metadata manual](https://docs.unity3d.com/2021.3/Documentation/Manual/AssetStorePkgDetails.html).
Code line breaks and indentation use br and non-breaking spaces, so they survive
without pre/code tags or custom CSS. Monospace fonts and syntax highlighting are
not assumed. The Cloud UPM editor's current rendering still needs a Portal preview;
local formatting checks do not establish that result.

Preserve any wording edits already made in the Portal, and check its preview
after pasting. The publisher confirmed that the previous Cloud Portal
Preview issue is resolved. Its cause and resolution were not recorded in
[the reproduction report](../../../docs/reviews/2026-11-07-upm-description-preview.md).
Publisher notes are internal and excluded from the public description.
portal-fields.json records the field mapping and undecided values for this handoff.

## Delivery and account steps

UPM is the delivery route, and the existing Spire builder produces a
UPM folder. Unity's current [UPM publishing guide](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/publish)
requires enrollment, a product draft, then package validation and upload.
The current [UPM revenue guide](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/revenue)
documents paid products; the older Unity 6000.3 guide's free-only note is not the
current unversioned workflow.

1. The publisher confirmed the product draft identity on 2026-11-07:
   individual.antonio-pedro-goncalves-ferreira.spire, single package. The namespace appears during
   draft creation. The identity was also confirmed through Unity's official
   publishing SDK; the Portal's browser UI was not independently inspected.
2. Populate the draft using the fields and images above. The public description
   combines features, technical requirements/limits, and support; internal
   publisher notes are excluded.
3. The exporter and acceptance fixtures use individual.antonio-pedro-goncalves-ferreira.spire. Build, sign, and validate the exact upload candidate under that identity.
   Historical com.houtamelo.spire artifacts belong to the previous local identity.
4. The declared minimum is 2022.3.62f3, selected by the publisher to
   match the validated Unity 2022 baseline. Retain the documented serialization
   limits and optional Showcase's Unity 6000 requirements. The observed source
   version is 4.7.6; the uploaded version is 4.7.6.
   This marketing builder does not build package DLLs.
5. Install the exact candidate in a clean project, run the
   [Asset Store UPM Validator and Uploader](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/validate),
   and populate the draft from the files above. The technical details and publisher
   notes include the completed Windows / Visual Studio and WebGL results.

Upload completed at 2026-11-07T23:07:53.698Z: version **4.7.6**, minimum
**2022.3.62f3**, through official UPM Publishing Tools 0.3.3.
The complete validator run reports 46 passes, 1 documented dependency warning,
and zero failures. Artifact SHA-256: d4a0482bdfa3fd8707f0e442c7af698338425d6e47e201692fa6b0d4297238bf.
The agent has not submitted or published the product.

The publisher confirmed that version **4.7.6** was submitted
for vetting on **2026-11-07**. Unity approval is pending.
This status comes from the publisher; the live Portal was not independently inspected.

The package manifest's minimum (2022.3.62f3) and the Portal's supported
Unity-version list are separate. Official uploader 0.3.3 sends the manifest and
the actual uploaded-from Editor version; it does not send the full compatibility
list. Unity initially selects the upload Editor in the Portal. Set compatible
families **2022.3, 2023.2, 6000.5** in the package metadata
after upload, preserving the uploaded-from provenance. No re-upload is needed.
These recommendations come from exact validation on 2022.3.62f3, 2023.2.22f1, 6000.5.6f1.
The entered Portal compatibility list is not independently confirmed in this
record; this builder does not edit it.

Unity 7000.0.0a12 serialization and Linux IL2CPP results are recorded in the
internal publisher notes. All 100 original behavioral tests and 60 native
runtime/serialization checks pass. The full unfiltered NUnit run passes all 107
cases after removal of an unrelated subprocess capability probe.

## Remaining publication inputs

- Await Unity's vetting result for 4.7.6 and address any reviewer feedback. The declared minimum is 2022.3.62f3.
- The recommended supported Unity families are 2022.3, 2023.2, 6000.5; the entered Portal list is not independently confirmed in this record.
- Existing publisher profile contact/website details. The public Getting Started
  URL is verified and included; offline documentation and changelog also ship.
- Video URL from the separate session when available.
- Portal-required AI-assistance information for functional package content;
  see [Unity's submission guidelines, section 1.6](https://marketplace.unity.com/publishing/submission-guidelines).

The approved copy's [publisher notes](listing/publisher-notes.md) retain the other
publication decisions. The publisher supplied the draft identity; this handoff
builder did not create the draft or perform the upload. This archive contains
marketing files; the software bundle is built and uploaded separately through
Unity's publishing tools. The recorded upload above is separate from this archive.

## Provenance

manifest.json records source paths, PNG dimensions, and SHA-256 hashes. The seven
gallery images were checked against their approved capture manifest. Copies are
byte-identical to the originals. Rebuild this folder and archive from the repo
with: node marketing/storefront/build-submission.cjs
