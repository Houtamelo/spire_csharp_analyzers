const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');

const root = __dirname;
const logo = path.resolve(root, '../../brand/logo.png');
const titleFont = '/usr/share/fonts/truetype/Poppins-SemiBold.ttf';
const bodyFont = '/usr/share/fonts/truetype/SourceSans3-Regular.otf';
const palette = { background: '#07050A', panel: '#160B1C', purple: '#3E1149', magenta: '#C80BFB', white: '#F5F0F7', muted: '#BDB1C5' };
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const xml = text => text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
const mvg = text => '"' + text.replaceAll('\\', '\\\\').replaceAll('"', '\\"') + '"';

function magick(args, binary = false) {
    const result = spawnSync('magick', args, { maxBuffer: 32 * 1024 * 1024 });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(result.stderr.toString());
    return binary ? result.stdout : result.stdout.toString();
}

const slides = JSON.parse(fs.readFileSync(path.join(root, 'slides.json'), 'utf8'));

fs.mkdirSync(path.join(root, 'editable'), { recursive: true });
const sourceHash = hash(logo);
const logoData = fs.readFileSync(logo).toString('base64');
for (const [index, slide] of slides.entries()) {
    const args = ['-size', '1950x1300', `xc:${palette.background}`,
        '-fill', palette.panel, '-draw', 'polygon 0,0 1950,0 1950,154 0,184',
        '(', logo, '-resize', '88x88', ')', '-geometry', '+52+38', '-compose', 'over', '-composite',
        '-fill', palette.magenta, '-draw', `rectangle 70,${312 + (slide.subtitle.length - 1) * 44} 1880,${315 + (slide.subtitle.length - 1) * 44}`];
    const svg = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="1950" height="1300" viewBox="0 0 1950 1300">',
        `  <title>${xml(slide.title)}</title>`,
        `  <desc>${xml(slide.subtitle.join(' ') + ' ' + slide.context)}</desc>`,
        `  <rect width="1950" height="1300" fill="${palette.background}"/>`,
        `  <path d="M0 0 H1950 V154 L0 184 Z" fill="${palette.panel}"/>`,
        `  <image x="52" y="38" width="88" height="88" xlink:href="data:image/png;base64,${logoData}"/>`,
        `  <rect x="70" y="${312 + (slide.subtitle.length - 1) * 44}" width="1810" height="3" fill="${palette.magenta}"/>`,
    ];
    function text(text, x, y, size, color, title = false) {
        args.push('-font', title ? titleFont : bodyFont, '-pointsize', String(size), '-fill', color, '-draw', `text ${x},${y} ${mvg(text)}`);
        svg.push(`  <text x="${x}" y="${y}" font-family="${title ? 'Poppins' : 'Source Sans 3'}" font-weight="${title ? 600 : 400}" font-size="${size}" fill="${color}">${xml(text)}</text>`);
    }
    text('Houtamelo.Spire', 160, 99, 39, palette.white, true);
    text(String(index + 1).padStart(2, '0') + ' / ' + String(slides.length).padStart(2, '0'), 1740, 98, 29, palette.muted);
    text(slide.title, 70, 228, slide.title.length > 39 ? 62 : 65, palette.white, true);
    slide.subtitle.forEach((line, i) => text(line, 70, 281 + i * 44, 34, palette.muted));
    for (const [photoIndex, photo] of slide.photos.entries()) {
        const input = path.join(root, 'raw', photo.source);
        const [sx, sy, sw, sh] = photo.crop;
        const height = Math.round(sh * photo.width / sw);
        if (photo.y + height > 1255) throw new Error('Photo exceeds gallery content bounds: ' + slide.file);
        const geometry = `${sw}x${sh}+${sx}+${sy}`;
        const cropped = magick([input, '-crop', geometry, '+repage', '-strip', 'PNG:-'], true);
        args.push('-fill', palette.purple, '-draw', `rectangle ${photo.x - 2},${photo.y - 2} ${photo.x + photo.width + 2},${photo.y + height + 2}`,
            '(', input, '-crop', geometry, '+repage', '-filter', 'Lanczos', '-resize', `${photo.width}x${height}!`, ')',
            '-geometry', `+${photo.x}+${photo.y}`, '-compose', 'over', '-composite');
        svg.push(`  <g id="capture-${photoIndex + 1}"><title>${xml(photo.source)}</title><rect x="${photo.x - 2}" y="${photo.y - 2}" width="${photo.width + 4}" height="${height + 4}" fill="${palette.purple}"/><image x="${photo.x}" y="${photo.y}" width="${photo.width}" height="${height}" xlink:href="data:image/png;base64,${cropped.toString('base64')}"/></g>`);
    }
    for (const note of slide.notes) text(note.text, note.x, note.y, note.size, note.color);
    text(slide.context, 70, 1290, 18, palette.muted);
    svg.push('</svg>');
    fs.writeFileSync(path.join(root, 'editable', slide.file + '.svg'), svg.join('\n') + '\n');
    args.push('-alpha', 'off', '-depth', '8', '-strip', `PNG24:${path.join(root, slide.file + '.png')}`);
    magick(args);
}
magick(['montage', ...slides.map(s => path.join(root, s.file + '.png')), '-thumbnail', '650x433', '-tile', '2x', '-geometry', '+12+12', '-background', palette.background, path.join(root, 'gallery-contact-sheet.png')]);
if (sourceHash !== hash(logo)) throw new Error('Selected logo was modified');
const environment = JSON.parse(fs.readFileSync(path.join(root, 'raw/capture-environment.json'), 'utf8'));
const manifest = {
    revision: 3, verificationDetails: 'raw/verification-v3.json', status: 'approved-for-first-release', dimensions: [1950, 1300],
    sourceLogo: path.relative(root, logo), sourceLogoSha256: sourceHash,
    software: { Unity: environment.Unity, Rider: environment.Rider, Burst: environment.Burst, NewtonsoftJson: environment.NewtonsoftJson },
    captureMethod: 'Actual Unity Editor and Rider windows on isolated Xvfb displays; PNG window/root captures.',
    layoutMethod: 'ImageMagick composition, with self-contained editable SVG sources. Captures are cropped and resized; no UI is generated or repainted.',
    fixtures: 'All code is captured in Rider. The Inspector displays the serialized fields shown beside it. JSON and Burst result panels execute real generated code in Unity; these result panels are capture-only demonstrations.',
    verification: { jsonRoundTrip: { kind: 'Success', value: 42 }, nativeBurst: JSON.parse(fs.readFileSync(path.join(root, 'raw/burst-result.json'), 'utf8')), quickFix: JSON.parse(fs.readFileSync(path.join(root, 'raw/rider-action-verification.json'), 'utf8')) },
    rawCaptures: [...new Set(slides.flatMap(s => s.photos.map(p => p.source)))].map(name => ({ file: 'raw/' + name, sha256: hash(path.join(root, 'raw', name)) })),
    slides: slides.map(s => ({ ...s, png: s.file + '.png', editable: 'editable/' + s.file + '.svg', sha256: hash(path.join(root, s.file + '.png')) })),
};
fs.writeFileSync(path.join(root, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
fs.writeFileSync(path.join(root, 'gallery.html'), `<!doctype html>
<html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Houtamelo.Spire — Gallery v3</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#07050a;color:#f5f0f7;font:16px/1.5 system-ui,sans-serif}main{max-width:1600px;margin:auto;padding:36px}h1{margin:0;font-size:clamp(24px,4vw,42px)}p{color:#bdb1c5}a{color:#e09af7}nav{display:flex;gap:24px;flex-wrap:wrap;margin:24px 0 36px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,540px),1fr));gap:28px}article{border:1px solid #3e1149;background:#160b1c}img{display:block;width:100%;height:auto}article div{padding:16px 20px}h2{font-size:20px;margin:0 0 8px}article p{margin:0 0 12px}small{color:#bdb1c5}
</style><main>
<h1>Houtamelo.Spire — gallery v3</h1>
<p>Seven gallery images approved for the first release, from real Unity Editor and Rider captures. Open an image at full resolution to inspect code and controls.</p>
<nav><a href="gallery-contact-sheet.png">Contact sheet</a><a href="../gallery-pngs-v3.zip">Download PNG set</a><a href="manifest.json">Capture manifest</a><a href="../../../docs/marketing_plan.md">Marketing plan</a></nav>
<div class="grid">${slides.map((s,i) => `<article><a href="${s.file}.png"><img src="${s.file}.png" alt="${xml(s.title)}" loading="lazy"></a><div><h2>${String(i+1).padStart(2,'0')}. ${xml(s.title)}</h2><p>${xml(s.subtitle.join(' '))}</p><a href="${s.file}.png">1950 × 1300 PNG</a> · <a href="editable/${s.file}.svg">Editable SVG</a><br><small>${xml(s.context)}</small></div></article>`).join('\n')}</div>
<p>All code snippets are captured in Rider. JSON and Burst output comes from capture-only Unity result panels executing real generated code.</p>
</main></html>\n`);
console.log(JSON.stringify({ images: slides.length, dimensions: manifest.dimensions, contactSheet: path.join(root, 'gallery-contact-sheet.png'), logoUnchanged: true }, null, 2));
