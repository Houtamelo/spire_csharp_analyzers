const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');
const { renderListing, pastePage } = require('./listing-format.cjs');

const root = path.resolve(__dirname, '../..');
const out = path.join(__dirname, 'submission');
const archive = path.join(__dirname, 'submission-handoff.zip');
const listingPath = path.join(root, 'docs/asset_store_listing.md');
const listing = fs.readFileSync(listingPath, 'utf8');
const draftPath = path.join(__dirname, 'portal-draft.json');
const draft = JSON.parse(fs.readFileSync(draftPath, 'utf8'));
const preparedDate = new Date().toISOString().slice(0, 10);
const headings = [...listing.matchAll(/^## (.+)$/gm)];
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const sourceListingHash = hash(listingPath);
const outputs = [];
function section(name) {
    const index = headings.findIndex(match => match[1] === name);
    if (index < 0) throw new Error('Listing section missing: ' + name);
    return listing.slice(headings[index].index + headings[index][0].length,
        headings[index + 1]?.index ?? listing.length).trim();
}
function write(relative, text) {
    const target = path.join(out, relative);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, text);
    outputs.push(relative);
}
const fields = [
    ['title.txt', 'Title'], ['summary.txt', 'Summary'], ['description.md', 'Description'],
    ['technical-details.md', 'Technical details'],
    ['documentation-and-support.md', 'Documentation and support'],
    ['keywords.txt', 'Keywords'], ['publisher-notes.md', 'Publisher notes'],
];
for (const [file, name] of fields) write('listing/' + file, section(name) + '\n');
if (section('Title') !== draft.title) throw new Error('Listing title differs from the confirmed product draft');
if (draft.structure !== 'Single package' || draft.packageTechnicalName !== draft.publisherNamespace + '.' + draft.productNamespace)
    throw new Error('Invalid single-package Portal identity');
if (section('Summary').length > 200) throw new Error('Summary exceeds 200 characters');
const description = section('Description') + '\n\n### Technical details\n\n' +
    section('Technical details') + '\n\n### Documentation and support\n\n' + section('Documentation and support') + '\n';
write('listing/upm-description.md', description);
const formatted = renderListing(description);
write('listing/upm-description-fragment.html', formatted.html + '\n');
write('listing/upm-description.html', pastePage(formatted.html));
write('listing/upm-description.txt', formatted.plain);
write('listing/approved-copy.md', listing);

const galleryRoot = 'marketing/storefront/gallery/';
const gallery = JSON.parse(fs.readFileSync(path.join(root, galleryRoot, 'manifest.json'), 'utf8'));
if (gallery.status !== 'approved-for-first-release' || gallery.revision !== 3)
    throw new Error('Expected approved gallery revision 3');
const sources = [
    { role: 'cover', source: 'marketing/storefront/cover-mockup-v2.png', file: 'images/cover.png', width: 1950, height: 1300 },
    { role: 'card', source: 'marketing/storefront/card-mockup-v3.png', file: 'images/card.png', width: 420, height: 280 },
    { role: 'icon', source: 'marketing/brand/logo_160x160.png', file: 'images/icon.png', width: 160, height: 160 },
    { role: 'social', source: 'marketing/storefront/social-card-v1.png', file: 'images/social.png', width: 1200, height: 630, approvalStatus: 'optional draft' },
    ...gallery.slides.map(slide => ({ role: 'gallery', source: galleryRoot + slide.png,
        file: 'images/gallery/' + slide.png, width: 1950, height: 1300, expectedHash: slide.sha256 })),
];
const images = sources.map(({ expectedHash, ...asset }) => {
    const source = path.join(root, asset.source);
    const data = fs.readFileSync(source);
    if (data.subarray(0, 8).toString('hex') !== '89504e470d0a1a0a' ||
        data.readUInt32BE(16) !== asset.width || data.readUInt32BE(20) !== asset.height)
        throw new Error('Unexpected image dimensions or format: ' + asset.source);
    const sha256 = hash(source);
    if (expectedHash && sha256 !== expectedHash)
        throw new Error('Gallery differs from its approved manifest: ' + asset.source);
    const target = path.join(out, asset.file);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.copyFileSync(source, target);
    if (hash(target) !== sha256) throw new Error('Copy verification failed: ' + asset.file);
    outputs.push(asset.file);
    return { ...asset, sha256 };
});
const versions = ['Houtamelo.Spire', 'Houtamelo.Spire.Analyzers', 'Houtamelo.Spire.CodeFixes']
    .map(name => fs.readFileSync(path.join(root, 'src', name, name + '.csproj'), 'utf8').match(/<Version>([^<]+)<\/Version>/)?.[1]);
if (!versions[0] || new Set(versions).size !== 1) throw new Error('Package versions do not match');
const packageSource = fs.readFileSync(path.join(root, 'tools/Spire.Tool.Core/PackageWorkflow.cs'), 'utf8');
const currentPackageName = packageSource.match(/\["name"\]\s*=\s*"([^"]+)"/)?.[1];
if (!currentPackageName) throw new Error('Current packager technical name was not found');
const minimumUnity = packageSource.match(/\["unity"\]\s*=\s*"([^"]+)"/)?.[1];
const minimumRelease = packageSource.match(/\["unityRelease"\]\s*=\s*"([^"]+)"/)?.[1];
if (!minimumUnity || !minimumRelease) throw new Error('Current package minimum Unity version was not found');
const minimumUnityVersion = minimumUnity + '.' + minimumRelease;
const compatibility = draft.compatibility;
if (!compatibility || !Array.isArray(compatibility.unityVersions) ||
    !Array.isArray(compatibility.validatedEditorVersions) ||
    !compatibility.unityVersions.every(version => /^\d+\.\d+$/.test(version)) ||
    new Set(compatibility.unityVersions).size !== compatibility.unityVersions.length ||
    JSON.stringify([...new Set(compatibility.validatedEditorVersions.map(version => version.match(/^\d+\.\d+(?=\.)/)?.[0]))]) !==
        JSON.stringify(compatibility.unityVersions) ||
    !compatibility.validatedEditorVersions.every(version => section('Technical details').includes(version)))
    throw new Error('Portal Unity compatibility must match the listing\'s validated Editor families');
const identityMigrationRequired = currentPackageName !== draft.packageTechnicalName;
const portalFields = {
    title: section('Title'), summary: section('Summary'), regularPriceUsd: 20,
    launchDiscountPercent: null,
    proposedCategory: 'Tools/Utilities', proposedDeliveryFormat: 'UPM, single-package product',
    currentPackageTechnicalName: currentPackageName, currentSourceVersion: versions[0],
    publisherNamespace: draft.publisherNamespace, productNamespace: draft.productNamespace,
    portalPackageTechnicalName: draft.packageTechnicalName, portalIdentityConfirmedDate: draft.confirmedDate,
    portalDraftUrl: draft.draftUrl,
    productStructure: draft.structure, packageIdentityMigrationRequired: identityMigrationRequired,
    releaseVersion: draft.upload?.version ?? null, lastUpload: draft.upload ?? null,
    productSubmission: draft.submission ?? null,
    currentManifestMinimumUnityVersion: minimumUnityVersion,
    portalCompatibility: compatibility,
    portalDescriptionPreviewStatus: draft.descriptionPreviewStatus,
    publisherProfile: 'exists', upmEnrollmentStatus: 'accepted; publisher-confirmed',
    upmEnrollmentSubmittedDate: '2026-11-03', upmEnrollmentConfirmedDate: '2026-11-06',
    supportUrl: 'https://github.com/Houtamelo/spire_csharp_analyzers/issues',
    publicDocumentationUrl: 'https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity-package/Documentation~/GettingStarted.md', videoUrl: null,
    sourceListing: 'docs/asset_store_listing.md', sourceListingSha256: sourceListingHash,
    descriptionFile: 'listing/upm-description.html',
    descriptionFormat: 'conservative HTML subset (p, strong, em, a, br); copy rendered content into the rich-text editor and check Portal preview',
    descriptionHtmlSourceFile: 'listing/upm-description-fragment.html',
    descriptionMarkdownFile: 'listing/upm-description.md', descriptionPlainTextFile: 'listing/upm-description.txt',
    featureDescriptionFile: 'listing/description.md', technicalDetailsFile: 'listing/technical-details.md',
    keywordsFile: 'listing/keywords.txt', media: images.map(({ role, file }) => ({ role, file })),
};
write('portal-fields.json', JSON.stringify(portalFields, null, 2) + '\n');
write('README.md', `# Spire Asset Store submission handoff

Storefront images and listing copy, collected on ${preparedDate} (UTC). Cover,
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
| Publisher namespace | ${draft.publisherNamespace} |
| Product namespace | ${draft.productNamespace} |
| Portal package technical name | ${draft.packageTechnicalName} |
| Portal compatible Unity versions | ${compatibility.unityVersions.join(', ')}; enter separately after upload |

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

1. The publisher confirmed the product draft identity on ${draft.confirmedDate}:
   ${draft.packageTechnicalName}, single package. The namespace appears during
   draft creation. The identity was also confirmed through Unity's official
   publishing SDK; the Portal's browser UI was not independently inspected.
2. Populate the draft using the fields and images above. The public description
   combines features, technical requirements/limits, and support; internal
   publisher notes are excluded.
3. ${identityMigrationRequired ? `Migrate the exporter/acceptance fixtures from ${currentPackageName} to ${draft.packageTechnicalName} before building an upload candidate.` : `The exporter and acceptance fixtures use ${draft.packageTechnicalName}. Build, sign, and validate the exact upload candidate under that identity.`}
   Historical com.houtamelo.spire artifacts belong to the previous local identity.
4. The declared minimum is ${minimumUnityVersion}, selected by the publisher to
   match the validated Unity 2022 baseline. Retain the documented serialization
   limits and optional Showcase's Unity 6000 requirements. The observed source
   version is ${versions[0]}; the uploaded version is ${draft.upload?.version ?? 'not recorded'}.
   This marketing builder does not build package DLLs.
5. Install the exact candidate in a clean project, run the
   [Asset Store UPM Validator and Uploader](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/validate),
   and populate the draft from the files above. The technical details and publisher
   notes include the completed Windows / Visual Studio and WebGL results.

${draft.upload ? `Upload completed at ${draft.upload.completedUtc}: version **${draft.upload.version}**, minimum
**${draft.upload.minimumUnityVersion}**, through official UPM Publishing Tools ${draft.upload.toolVersion}.
The complete validator run reports ${draft.upload.validation.passes} passes, ${draft.upload.validation.warnings} documented dependency warning,
and zero failures. Artifact SHA-256: ${draft.upload.artifactSha256}.
The agent has not submitted or published the product.` : 'Software upload remains pending.'}

${draft.submission ? `The publisher confirmed that version **${draft.submission.version}** was submitted
for vetting on **${draft.submission.confirmedDate}**. Unity approval is pending.
This status comes from the publisher; the live Portal was not independently inspected.` : 'Product submission remains pending.'}

The package manifest's minimum (${minimumUnityVersion}) and the Portal's supported
Unity-version list are separate. Official uploader 0.3.3 sends the manifest and
the actual uploaded-from Editor version; it does not send the full compatibility
list. Unity initially selects the upload Editor in the Portal. Set compatible
families **${compatibility.unityVersions.join(', ')}** in the package metadata
after upload, preserving the uploaded-from provenance. No re-upload is needed.
These recommendations come from exact validation on ${compatibility.validatedEditorVersions.join(', ')}.
The entered Portal compatibility list is not independently confirmed in this
record; this builder does not edit it.

Unity 7000.0.0a12 serialization and Linux IL2CPP results are recorded in the
internal publisher notes. All 100 original behavioral tests and 60 native
runtime/serialization checks pass. The full unfiltered NUnit run passes all 107
cases after removal of an unrelated subprocess capability probe.

## Remaining publication inputs

- ${draft.submission ? `Await Unity's vetting result for ${draft.submission.version} and address any reviewer feedback.` : `Review the uploaded ${draft.upload?.version ?? versions[0]} candidate and complete the Portal submission.`} The declared minimum is ${minimumUnityVersion}.
- The recommended supported Unity families are ${compatibility.unityVersions.join(', ')}; the entered Portal list is not independently confirmed in this record.
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
`);
const manifest = {
    preparedDate, status: draft.submission ? 'submitted-for-vetting; awaiting Unity review' : 'marketing-ready; product submission pending',
    regularPriceUsd: 20, sourceListingSha256: sourceListingHash, sourceDraftIdentitySha256: hash(draftPath), images,
    files: outputs.map(file => ({ file, sha256: hash(path.join(out, file)) })),
};
write('manifest.json', JSON.stringify(manifest, null, 2) + '\n');
const zip = spawnSync('zip', ['-q', '-X', archive, ...outputs.map(file => 'submission/' + file)],
    { cwd: __dirname, maxBuffer: 4 * 1024 * 1024 });
if (zip.error) throw zip.error;
if (zip.status !== 0) throw new Error(zip.stderr.toString());
if (hash(listingPath) !== sourceListingHash) throw new Error('Listing changed while collecting the handoff; rebuild');
console.log(JSON.stringify({ folder: path.relative(root, out), archive: path.relative(root, archive),
    approvedImages: images.filter(image => image.role !== 'social').length, optionalDraftImages: 1,
    files: outputs.length, regularPriceUsd: 20, packageIdentityMigrationRequired: portalFields.packageIdentityMigrationRequired }, null, 2));
