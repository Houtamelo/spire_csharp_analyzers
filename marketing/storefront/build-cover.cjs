const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');

const root = __dirname;
const original = path.join(root, 'cover-mockup-v1.png');
const originalSvg = path.join(root, 'cover-mockup-v1.svg');
const cover = path.join(root, 'cover-mockup-v2.png');
const card = path.join(root, 'cover-mockup-v2-card.png');
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const originalHash = hash(original);
const originalSvgHash = hash(originalSvg);
const titleFont = '/usr/share/fonts/truetype/Poppins-SemiBold.ttf';
const bodyFont = '/usr/share/fonts/truetype/SourceSans3-Regular.otf';
const palette = { background: '#07050A', white: '#F5F0F7', magenta: '#C80BFB', muted: '#BDB1C5' };
const xml = value => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
function magick(args) {
    const result = spawnSync('magick', args, { maxBuffer: 4 * 1024 * 1024 });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(result.stderr.toString());
    return result.stdout.toString();
}

// Retain the approved artwork's pixels while replacing its text blocks.
const args = [original, '-fill', palette.background,
    '-draw', 'rectangle 910,235 1949,420',
    '-draw', 'rectangle 910,470 1949,850',
    '-draw', 'rectangle 910,915 1949,1195'];
const textNodes = [];
function text(copy, x, y, size, color, title = false) {
    textNodes.push(`    <text x="${x}" y="${y}" fill="${color}" font-family="${title ? 'Poppins' : 'Source Sans 3'}" font-weight="${title ? 600 : 400}" font-size="${size}">${xml(copy)}</text>`);
    args.push('-font', title ? titleFont : bodyFont, '-pointsize', String(size),
        '-fill', color, '-draw', `text ${x},${y} "${copy}"`);
}
text('Spire', 930, 354, 140, palette.white, true);
text('Save time by catching', 930, 578, 90, palette.white);
text('more errors before even', 930, 696, 90, palette.white);
text('running the game', 930, 814, 90, palette.magenta);
const highlightWidth = Number(magick(['-font', bodyFont, '-pointsize', '90',
    'label:running the game', '-format', '%w', 'info:']));
text('.', 930 + highlightWidth, 814, 90, palette.white);
for (const [index, copy] of [
    'Exhaustive matching for enums/classes/interfaces',
    'Lightweight Discriminated Unions',
    'Performance without sacrificing ergonomics',
].entries()) {
    const y = 974 + index * 66;
    text('→', 930, y, 40, palette.magenta);
    text(copy, 986, y, 40, palette.muted);
}

let svg = fs.readFileSync(originalSvg, 'utf8');
if (!svg.includes('<g id="copy">')) throw new Error('Original SVG text group is missing');
svg = svg
    .replace(/<title id="title">[^<]*<\/title>/, '<title id="title">Spire — Asset Store cover mockup v2</title>')
    .replace(/<desc id="description">[^<]*<\/desc>/,
        '<desc id="description">The selected purple slime S beside Spire. Save time by catching more errors before even running the game. Exhaustive matching for enums/classes/interfaces. Lightweight Discriminated Unions. Performance without sacrificing ergonomics.</desc>')
    .replace('Draft layout.', 'Draft v2: original layout with revised text; shared cover and card design.')
    .replace(/  <g id="copy">[\s\S]*?<\/g>/, '  <g id="copy">\n' + textNodes.join('\n') + '\n  </g>');
fs.writeFileSync(path.join(root, 'cover-mockup-v2.svg'), svg);
args.push('-alpha', 'off', '-depth', '8', '-strip', `PNG24:${cover}`);
magick(args);
magick([cover, '-filter', 'Lanczos', '-resize', '420x280!', '-strip', `PNG24:${card}`]);
if (hash(original) !== originalHash || hash(originalSvg) !== originalSvgHash)
    throw new Error('Original artwork was modified');
console.log(JSON.stringify({ cover: path.basename(cover), coverDimensions: [1950, 1300],
    card: path.basename(card), cardDimensions: [420, 280], originalUnchanged: true }, null, 2));
