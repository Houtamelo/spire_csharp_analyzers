const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');

const root = __dirname;
const logo = path.resolve(root, '../brand/logo.png');
const sourceFiles = [logo, ...['cover-mockup-v2', 'card-mockup-v3'].flatMap(name =>
    ['png', 'svg'].map(extension => path.resolve(root, `../storefront/${name}.${extension}`)))];
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const sourceHashes = sourceFiles.map(hash);
const width = 1920;
const height = 1080;
const basename = 'promo-banner-v1';
const palette = {
    background: '#07050A', panel: '#160B1C', footer: '#1B0D23',
    purple: '#3E1149', magenta: '#C80BFB', white: '#F5F0F7', muted: '#BDB1C5',
};
const titleFont = '/usr/share/fonts/truetype/Poppins-SemiBold.ttf';
const bodyFont = '/usr/share/fonts/truetype/SourceSans3-Regular.otf';
const xml = value => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
const svg = [
    '<?xml version="1.0" encoding="UTF-8"?>',
    `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img" aria-labelledby="title description">`,
    '  <title id="title">Spire by Houtamelo — publisher promo banner draft</title>',
    '  <desc id="description">The original purple slime S beside Spire by Houtamelo. Save time by catching more errors before even running the game. Exhaustive matching for enums/classes/interfaces. Lightweight Discriminated Unions. Performance without sacrificing ergonomics.</desc>',
    `  <metadata>1920 x 1080 draft; Portal dimensions are unconfirmed. Reuses the logo, colors, fonts, and headline from the approved Spire cover v2. Source logo SHA-256: ${sourceHashes[0]}. Rebuild with node marketing/publisher/build-banner.cjs.</metadata>`,
    `  <rect width="${width}" height="${height}" fill="${palette.background}"/>`,
    `  <path d="M0 0 H810 L750 1080 H0 Z" fill="${palette.panel}"/>`,
    `  <path d="M0 936 H758 L750 1080 H0 Z" fill="${palette.footer}"/>`,
    `  <path d="M810 0 L750 1080" fill="none" stroke="${palette.purple}" stroke-width="2"/>`,
    `  <image x="10" y="130" width="810" height="810" xlink:href="data:image/png;base64,${fs.readFileSync(logo).toString('base64')}"/>`,
];
const args = ['-size', `${width}x${height}`, `xc:${palette.background}`,
    '-fill', palette.panel, '-draw', 'polygon 0,0 810,0 750,1080 0,1080',
    '-fill', palette.footer, '-draw', 'polygon 0,936 758,936 750,1080 0,1080',
    '-stroke', palette.purple, '-strokewidth', '2', '-draw', 'line 810,0 750,1080', '-stroke', 'none',
    '(', logo, '-filter', 'Lanczos', '-resize', '810x810', ')',
    '-geometry', '+10+130', '-compose', 'over', '-composite'];

function magick(commandArgs) {
    const result = spawnSync('magick', commandArgs, { maxBuffer: 4 * 1024 * 1024 });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(result.stderr.toString());
    return result.stdout.toString();
}

function text(copy, x, y, size, color, title = false) {
    const font = title ? titleFont : bodyFont;
    const textWidth = Number(magick(['-font', font, '-pointsize', String(size),
        `label:${copy}`, '-format', '%w', 'info:']));
    if (x + textWidth > width - 70) throw new Error(`Text exceeds right margin: ${copy}`);
    svg.push(`  <text x="${x}" y="${y}" fill="${color}" font-family="${title ? 'Poppins' : 'Source Sans 3'}" font-weight="${title ? 600 : 400}" font-size="${size}">${xml(copy)}</text>`);
    args.push('-font', font, '-pointsize', String(size), '-fill', color,
        '-draw', `text ${x},${y} "${copy}"`);
    return textWidth;
}

text('Spire', 892, 286, 140, palette.white, true);
text('by Houtamelo', 900, 344, 36, palette.muted);
text('Save time by catching', 892, 500, 76, palette.white);
text('more errors before even', 892, 594, 76, palette.white);
const highlightWidth = text('running the game', 892, 688, 76, palette.magenta);
text('.', 892 + highlightWidth, 688, 76, palette.white);
for (const [index, copy] of [
    'Exhaustive matching for enums/classes/interfaces',
    'Lightweight Discriminated Unions',
    'Performance without sacrificing ergonomics',
].entries()) {
    const y = 818 + index * 62;
    text('→', 892, y, 36, palette.magenta);
    text(copy, 942, y, 36, palette.muted);
}
svg.push('</svg>');
args.push('-alpha', 'off', '-depth', '8', '-strip', `PNG24:${path.join(root, basename + '.png')}`);
magick(args);
fs.writeFileSync(path.join(root, basename + '.svg'), svg.join('\n') + '\n');
sourceFiles.forEach((file, index) => {
    if (hash(file) !== sourceHashes[index]) throw new Error(`Source artwork changed: ${file}`);
});
const dimensions = magick([path.join(root, basename + '.png'), '-format', '%wx%h %[channels]', 'info:']);
if (!dimensions.startsWith(`${width}x${height} `)) throw new Error(`Unexpected export: ${dimensions}`);
console.log(JSON.stringify({ png: basename + '.png', svg: basename + '.svg', dimensions,
    sourceArtworkUnchanged: true, portalSizeConfirmed: false }, null, 2));
