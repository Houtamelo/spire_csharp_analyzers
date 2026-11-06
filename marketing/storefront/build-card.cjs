const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');

const root = __dirname;
const revision = process.argv[2] ?? 'v3';
if (!['v1', 'v2', 'v3'].includes(revision)) throw new Error('Expected card revision v1, v2, or v3');
const basename = `card-mockup-${revision}`;
const logo = path.resolve(root, revision === 'v3' ? '../brand/logo.png' : '../brand/logo_256x256.png');
const logoHash = () => crypto.createHash('sha256').update(fs.readFileSync(logo)).digest('hex');
const sourceHash = logoHash();
const palette = {
    background: '#07050A', panel: '#160B1C', purple: '#3E1149',
    magenta: '#C80BFB', white: '#F5F0F7', muted: '#BDB1C5',
};
const titleFont = '/usr/share/fonts/truetype/Poppins-SemiBold.ttf';
const bodyFont = '/usr/share/fonts/truetype/SourceSans3-Regular.otf';
const svg = [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="420" height="280" viewBox="0 0 420 280" role="img" aria-labelledby="title description">',
    `  <title id="title">${revision === 'v1' ? 'Houtamelo.Spire' : 'Spire'} — Asset Store card mockup ${revision}</title>`,
    `  <desc id="description">${revision !== 'v1'
        ? 'Save time by catching more errors before even running the game. Exhaustive matching for enums/classes/interfaces. Lightweight Discriminated Unions. Performance without sacrificing ergonomics.'
        : 'Purple slime S with the product name, exhaustive matching and discriminated unions. Catch errors at compile time.'}</desc>`,
    `  <metadata>Draft for review. Source: marketing/brand/${path.basename(logo)}. SHA-256: ${sourceHash}. Fonts: Poppins SemiBold and Source Sans 3 Regular. Card dimensions: https://assetstore.unity.com/publishing/image-template</metadata>`,
    `  <rect width="420" height="280" fill="${palette.background}"/>`,
];
const args = revision === 'v3'
    ? [path.join(root, 'cover-mockup-v2-card.png'), '-fill', palette.background, '-draw', 'rectangle 196,0 419,279']
    : ['-size', '420x280', `xc:${palette.background}`];
if (revision === 'v3') {
    const originalSvg = fs.readFileSync(path.join(root, 'cover-mockup-v1.svg'), 'utf8');
    svg.push(`  <g transform="scale(${420 / 1950})">`);
    for (const id of ['background', 'selected-logo']) {
        const group = originalSvg.match(new RegExp(`  <g id="${id}">[\\s\\S]*?</g>`));
        if (!group) throw new Error(`Original cover group ${id} is missing`);
        svg.push(group[0]);
    }
    svg.push('  </g>');
}
function rectangle(x, y, width, height, color) {
    svg.push(`  <rect x="${x}" y="${y}" width="${width}" height="${height}" fill="${color}"/>`);
    args.push('-fill', color, '-draw', `rectangle ${x},${y} ${x + width - 1},${y + height - 1}`);
}
function image(x, y, size) {
    svg.push(`  <image x="${x}" y="${y}" width="${size}" height="${size}" xlink:href="data:image/png;base64,${fs.readFileSync(logo).toString('base64')}"/>`);
    args.push('(', logo, '-filter', 'Lanczos', '-resize', `${size}x${size}`, ')',
        '-geometry', `+${x}+${y}`, '-compose', 'over', '-composite');
}
function text(copy, x, y, size, color, title = false) {
    const escaped = copy.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
    svg.push(`  <text x="${x}" y="${y}" font-family="${title ? 'Poppins' : 'Source Sans 3'}" font-weight="${title ? 600 : 400}" font-size="${size}" fill="${color}">${escaped}</text>`);
    args.push('-font', title ? titleFont : bodyFont, '-pointsize', String(size),
        '-fill', color, '-draw', `text ${x},${y} "${copy}"`);
}
function measure(copy, size) {
    const result = spawnSync('magick', ['-font', bodyFont, '-pointsize', String(size),
        `label:${copy}`, '-format', '%w', 'info:']);
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(result.stderr.toString());
    return Number(result.stdout.toString());
}
if (revision === 'v1') {
    rectangle(0, 0, 420, 66, palette.panel);
    rectangle(26, 59, 54, 3, palette.magenta);
    image(14, 72, 180);
    text('Houtamelo.Spire', 26, 43, 25, palette.white, true);
    text('Exhaustive', 211, 112, 24, palette.white);
    text('matching', 211, 139, 24, palette.white);
    text('Discriminated', 211, 188, 21, palette.white);
    text('unions', 211, 215, 24, palette.white);
    text('Catch errors at compile time.', 26, 264, 16, palette.muted);
} else if (revision === 'v2') {
    image(17, 0, 96);
    text('Spire', 132, 65, 48, palette.white, true);
    rectangle(134, 85, 44, 3, palette.magenta);
    text('Save time by catching more errors', 24, 121, 19, palette.white);
    text('before even ', 24, 144, 19, palette.white);
    const highlightX = 24 + measure('before even ', 19);
    text('running the game', highlightX, 144, 19, palette.magenta);
    text('.', highlightX + measure('running the game', 19), 144, 19, palette.white);
    text('→', 24, 183, 18, palette.magenta);
    text('Exhaustive matching for', 47, 183, 18, palette.white);
    text('enums/classes/interfaces', 47, 205, 18, palette.white);
    text('→', 24, 233, 18, palette.magenta);
    text('Lightweight Discriminated Unions', 47, 233, 18, palette.white);
    text('→', 24, 261, 18, palette.magenta);
    text('Performance without sacrificing ergonomics', 47, 261, 18, palette.white);
} else {
    text('Spire', 200, 52, 30, palette.white, true);
    text('Save time by catching', 200, 94, 19, palette.white);
    text('more errors before even', 200, 116, 19, palette.white);
    text('running the game', 200, 138, 19, palette.magenta);
    text('.', 200 + measure('running the game', 19), 138, 19, palette.white);
    for (const [index, lines] of [
        ['Exhaustive matching for', 'enums/classes/interfaces'],
        ['Lightweight', 'Discriminated Unions'],
        ['Performance without', 'sacrificing ergonomics'],
    ].entries()) {
        const y = 169 + index * 38;
        text('→', 200, y, 16, palette.magenta);
        lines.forEach((line, lineIndex) => text(line, 217, y + lineIndex * 18, 16, palette.muted));
    }
}
svg.push('</svg>');

fs.writeFileSync(path.join(root, basename + '.svg'), svg.join('\n') + '\n');
args.push('-alpha', 'off', '-depth', '8', '-strip', `PNG24:${path.join(root, basename + '.png')}`);
const result = spawnSync('magick', args, { maxBuffer: 4 * 1024 * 1024 });
if (result.error) throw result.error;
if (result.status !== 0) throw new Error(result.stderr.toString());
if (logoHash() !== sourceHash) throw new Error('Source logo was modified');
console.log(JSON.stringify({ png: basename + '.png', svg: basename + '.svg', dimensions: [420, 280], sourceLogoSha256: sourceHash }, null, 2));
