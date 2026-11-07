const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');

const root = path.resolve(__dirname, '../..');
const out = path.join(__dirname, 'submission');
const archive = path.join(__dirname, 'submission-handoff.zip');
const listingPath = path.join(root, 'docs/asset_store_listing.md');
const listing = fs.readFileSync(listingPath, 'utf8');
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
write('listing/approved-copy.md', listing);

const galleryRoot = 'marketing/storefront/gallery/';
const gallery = JSON.parse(fs.readFileSync(path.join(root, galleryRoot, 'manifest.json'), 'utf8'));
if (gallery.status !== 'approved-for-first-release' || gallery.revision !== 3)
    throw new Error('Expected approved gallery revision 3');
const sources = [
    { role: 'cover', source: 'marketing/storefront/cover-mockup-v2.png', file: 'images/cover.png', width: 1950, height: 1300 },
    { role: 'card', source: 'marketing/storefront/card-mockup-v3.png', file: 'images/card.png', width: 420, height: 280 },
    { role: 'icon', source: 'marketing/brand/logo_160x160.png', file: 'images/icon.png', width: 160, height: 160 },
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
const portalFields = {
    title: section('Title'), summary: section('Summary'), regularPriceUsd: 20,
    launchDiscountPercent: null,
    proposedCategory: 'Tools/Utilities', proposedDeliveryFormat: 'UPM, single-package product',
    currentPackageTechnicalName: 'com.houtamelo.spire', currentSourceVersion: versions[0],
    releaseVersion: null, currentManifestMinimumUnityVersion: '6000.5.6f1',
    publisherProfile: 'exists', upmEnrollmentStatus: 'accepted; publisher-confirmed',
    upmEnrollmentSubmittedDate: '2026-11-03', upmEnrollmentConfirmedDate: '2026-11-06',
    supportUrl: 'https://github.com/Houtamelo/spire_csharp_analyzers/issues',
    publicDocumentationUrl: null, videoUrl: null,
    sourceListing: 'docs/asset_store_listing.md', sourceListingSha256: sourceListingHash,
    descriptionFile: 'listing/description.md', technicalDetailsFile: 'listing/technical-details.md',
    keywordsFile: 'listing/keywords.txt', media: images.map(({ role, file }) => ({ role, file })),
};
write('portal-fields.json', JSON.stringify(portalFields, null, 2) + '\n');
write('README.md', `# Spire Asset Store submission handoff

Approved storefront images and listing copy, collected on ${preparedDate} (UTC).
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
   validation scope and limitations. The observed source version is ${versions[0]};
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
`);
const manifest = {
    preparedDate, status: 'marketing-ready; account and software delivery pending',
    regularPriceUsd: 20, sourceListingSha256: sourceListingHash, images,
    files: outputs.map(file => ({ file, sha256: hash(path.join(out, file)) })),
};
write('manifest.json', JSON.stringify(manifest, null, 2) + '\n');
const zip = spawnSync('zip', ['-q', '-X', archive, ...outputs.map(file => 'submission/' + file)],
    { cwd: __dirname, maxBuffer: 4 * 1024 * 1024 });
if (zip.error) throw zip.error;
if (zip.status !== 0) throw new Error(zip.stderr.toString());
if (hash(listingPath) !== sourceListingHash) throw new Error('Listing changed while collecting the handoff; rebuild');
console.log(JSON.stringify({ folder: path.relative(root, out), archive: path.relative(root, archive),
    approvedImages: images.length, files: outputs.length, regularPriceUsd: 20 }, null, 2));
