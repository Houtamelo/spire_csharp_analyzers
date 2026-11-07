# Spire Asset Store submission handoff

Approved storefront images and listing copy, collected on 2026-11-06 (UTC).
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
| Description | [listing/description.md](listing/description.md) |
| Technical details | [listing/technical-details.md](listing/technical-details.md) |
| Documentation and support | [listing/documentation-and-support.md](listing/documentation-and-support.md) |
| Keywords | [listing/keywords.txt](listing/keywords.txt) |
| Regular price | 20 USD |
| Category proposal | Tools/Utilities |
| Cover | [images/cover.png](images/cover.png), 1950 x 1300, cover revision 2 |
| Card | [images/card.png](images/card.png), 420 x 280, card revision 3 |
| Icon | [images/icon.png](images/icon.png), 160 x 160 |
| Gallery | Seven files in images/gallery, ordered 01 through 07, revision 3 |

The Markdown files retain the approved wording. Apply their formatting in the
Portal's rich-text fields. Publisher notes are internal; they are exported
separately from the public description. portal-fields.json records the field
mapping and undecided values for this handoff.

## Delivery and account steps

UPM is the proposed delivery route because the existing Spire builder produces a
UPM folder. Unity's current [UPM publishing guide](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/publish)
requires enrollment, a product draft, then package validation and upload.
The current [UPM revenue guide](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/revenue)
documents paid products; the older Unity 6000.3 guide's free-only note is not the
current unversioned workflow.

1. Select the enrolled publisher organization in the Cloud Dashboard and confirm
   its publisher namespace. Enrollment acceptance is already recorded; the exact
   reserved prefix has not been inspected in this handoff.
2. [Create a product draft](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/create-draft),
   initially using one package as recommended by the current packaging inventory.
   Reserve the technical name that the publisher is entitled to use. The current
   local name is com.houtamelo.spire; the Portal reservation has not been checked.
3. Build a release candidate with the existing package command, using a dedicated
   output directory: dotnet run -c Release --project tools/Spire.Tool -- package
   --output tmp/asset-store-release-candidate/com.houtamelo.spire
4. Choose the release version and declared minimum, retaining the documented
   validation scope and limitations. The observed source version is 4.7.6;
   this handoff does not choose a release version or build package DLLs.
5. Install the exact candidate in a clean project, run the
   [Asset Store UPM Validator and Uploader](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/validate),
   and populate the draft from the files above. The technical details and publisher
   notes include the completed Windows / Visual Studio and WebGL results.

Unity 7000.0.0a12 serialization and Linux IL2CPP results are recorded in the
internal publisher notes. All 100 original behavioral tests and 60 native
runtime/serialization checks pass. The full unfiltered NUnit run passes all 107
cases after removal of an unrelated subprocess capability probe.

## Remaining publication inputs

- Confirmed publisher namespace and reserved product/package technical name.
- Final software bundle, release version, and declared minimum.
- Existing publisher profile contact/website details and an online documentation
  URL if one will be advertised. The included offline guides are already listed.
- Video URL from the separate session when available.
- Portal-required AI-assistance information for functional package content;
  see [Unity's submission guidelines, section 1.6](https://marketplace.unity.com/publishing/submission-guidelines).

The approved copy's [publisher notes](listing/publisher-notes.md) retain the other
publication decisions. No Portal draft or package upload was created by this
handoff. This archive contains marketing files; the software bundle is built and
uploaded separately through Unity's publishing tools.

## Provenance

manifest.json records source paths, PNG dimensions, and SHA-256 hashes. The seven
gallery images were checked against their approved capture manifest. Copies are
byte-identical to the originals. Rebuild this folder and archive from the repo
with: node marketing/storefront/build-submission.cjs
