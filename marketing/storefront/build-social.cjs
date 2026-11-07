const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');

const root = __dirname;
const logo = path.resolve(root, '../brand/logo.png');
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const protectedFiles = [logo, path.join(root, 'cover-mockup-v2.png'), path.join(root, 'card-mockup-v3.png')];
const before = protectedFiles.map(hash);
const palette = { background: '#07050A', panel: '#160B1C', purple: '#3E1149', magenta: '#C80BFB', white: '#F5F0F7', muted: '#BDB1C5' };
const titleFont = '/usr/share/fonts/truetype/Poppins-SemiBold.ttf';
const bodyFont = '/usr/share/fonts/truetype/SourceSans3-Regular.otf';
const xml = value => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
const svg = [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="1200" height="630" viewBox="0 0 1200 630" role="img" aria-labelledby="title description">',
    '  <title id="title">Spire C# — optional social media image draft</title>',
    '  <desc id="description">Save time by catching more errors before even running the game. Exhaustive matching, IDE quick fixes, and lightweight discriminated unions.</desc>',
    `  <metadata>1200 x 630 draft. Reuses the existing logo, palette, and fonts; approved cover/card are preserved. Source logo SHA-256: ${before[0]}.</metadata>`,
    `  <rect width="1200" height="630" fill="${palette.background}"/>`,
    `  <path d="M0 0 H485 L445 630 H0 Z" fill="${palette.panel}"/>`,
    `  <path d="M485 0 L445 630" stroke="${palette.purple}" stroke-width="2"/>`,
    `  <image x="20" y="115" width="400" height="400" xlink:href="data:image/png;base64,${fs.readFileSync(logo).toString('base64')}"/>`,
];
const args = ['-size', '1200x630', `xc:${palette.background}`,
    '-fill', palette.panel, '-draw', 'polygon 0,0 485,0 445,630 0,630',
    '-stroke', palette.purple, '-strokewidth', '2', '-draw', 'line 485,0 445,630', '-stroke', 'none',
    '(', logo, '-filter', 'Lanczos', '-resize', '400x400', ')', '-geometry', '+20+115', '-compose', 'over', '-composite'];
function run(values) {
    const result = spawnSync('magick', values, { maxBuffer: 4 * 1024 * 1024 });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(result.stderr.toString());
    return result.stdout.toString();
}
function text(copy, x, y, size, color, title = false) {
    const font = title ? titleFont : bodyFont;
    const width = Number(run(['-font', font, '-pointsize', String(size), `label:${copy}`, '-format', '%w', 'info:']));
    if (x + width > 1150) throw new Error('Text exceeds the right margin: ' + copy);
    svg.push(`  <text x="${x}" y="${y}" font-family="${title ? 'Poppins' : 'Source Sans 3'}" font-weight="${title ? 600 : 400}" font-size="${size}" fill="${color}">${xml(copy)}</text>`);
    args.push('-font', font, '-pointsize', String(size), '-fill', color, '-draw', `text ${x},${y} "${copy}"`);
}
text('Spire C#', 520, 170, 80, palette.white, true);
text('Save time by catching', 520, 286, 43, palette.white);
text('more errors before even', 520, 342, 43, palette.white);
text('running the game.', 520, 398, 43, palette.magenta);
text('Exhaustive matching + IDE quick fixes', 520, 505, 27, palette.muted);
text('Lightweight discriminated unions', 520, 548, 27, palette.muted);
svg.push('</svg>');
fs.writeFileSync(path.join(root, 'social-card-v1.svg'), svg.join('\n') + '\n');
args.push('-alpha', 'off', '-depth', '8', '-strip', `PNG24:${path.join(root, 'social-card-v1.png')}`);
run(args);
protectedFiles.forEach((file, index) => { if (hash(file) !== before[index]) throw new Error('Source artwork changed: ' + file); });
console.log(JSON.stringify({ file: 'social-card-v1.png', dimensions: [1200, 630], status: 'optional draft', approvedArtworkPreserved: true }));
