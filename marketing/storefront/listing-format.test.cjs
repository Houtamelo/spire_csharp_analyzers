const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');

const ROOT = path.resolve(__dirname, '../..');
const { renderListing, pastePage } = require('./listing-format.cjs');

function decodeRenderedText(html) {
    return html
        .replace(/<br\s*\/?>/gi, '\n')
        .replace(/<\/p>/gi, '\n\n')
        .replace(/<[^>]+>/g, '')
        .replace(/&nbsp;/g, ' ')
        .replace(/&quot;/g, '"')
        .replace(/&#39;|&apos;/g, "'")
        .replace(/&lt;/g, '<')
        .replace(/&gt;/g, '>')
        .replace(/&amp;/g, '&');
}

function decodeHtmlEntities(text) {
    const entities = {
        '&amp;': '&', '&quot;': '"', '&#39;': "'", '&apos;': "'", '&lt;': '<', '&gt;': '>',
    };
    return text.replace(/&amp;|&quot;|&#39;|&apos;|&lt;|&gt;/g, entity => entities[entity]);
}

function assertConservativeAssetStoreHtml(fragment) {
    const tags = fragment.match(/<\/?[A-Za-z][^>]*>/g) ?? [];
    for (const tag of tags) {
        assert.match(
            tag,
            /^<\/?(?:p|strong|em|a(?:\s+href="https:\/\/[^"<>]+")?|br)\s*\/?\s*>$/i,
            `unsupported Asset Store HTML tag: ${tag}`,
        );
    }
    assert.doesNotMatch(fragment, /<\/?(?:pre|code|ul|ol|li|div|span|style|script)\b/i);
}

function sha256(file) {
    return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
}

test('renders prose with the documented conservative Asset Store HTML subset', () => {
    const source = [
        '# Formatting test',
        '',
        'Use **bold**, *italics*, and `inline <T>` in the description.',
        '',
        '- first feature',
        '- second feature',
        '',
        '[Read the issue tracker](https://github.com/Houtamelo/spire_csharp_analyzers/issues)',
    ].join('\n');

    const { html, plain } = renderListing(source);

    assertConservativeAssetStoreHtml(html);
    assert.match(html, /<strong>Formatting test<\/strong>/);
    assert.match(html, /Use <strong>bold<\/strong>, <em>italics<\/em>, and inline &lt;T&gt; in the description\./);
    assert.doesNotMatch(html, /<code\b/i);
    assert.match(html, /• first feature/);
    assert.match(html, /• second feature/);
    assert.match(html, /<a href="https:\/\/github\.com\/Houtamelo\/spire_csharp_analyzers\/issues">Read the issue tracker<\/a>/);
    assert.match(plain, /Formatting test/);
    assert.match(plain, /• first feature\n• second feature/);
});

test('renders fenced C# snippets with preserved lines, blank lines, indentation, and escaping', () => {
    const code = [
        'public readonly struct Box<T> where T : unmanaged',
        '{',
        '    public string Value => "<&>";',
        '',
        '    public static T operator +(T left, T right) => left;',
        '}',
    ].join('\n');
    const source = `Before \`inline <T>\`.\n\n\`\`\`csharp\n${code}\n\`\`\`\n\nAfter.`;

    const { html } = renderListing(source);
    const rendered = decodeRenderedText(html);

    assertConservativeAssetStoreHtml(html);
    assert.doesNotMatch(html, /```/);
    assert.doesNotMatch(html, /<code\b|<pre\b/i);
    assert.match(html, /Box&lt;T&gt;/);
    assert.match(html, /Value =&gt; &quot;&lt;&amp;&gt;&quot;/);
    assert.match(html, /&nbsp;&nbsp;&nbsp;&nbsp;public string/);
    assert.match(rendered, /public readonly struct Box<T> where T : unmanaged\n\{\n    public string Value => "<&>";\n\n    public static T operator \+\(T left, T right\) => left;\n\}/);
    assert.match(rendered, /Before inline <T>\.\n{2,}public readonly struct/);
    assert.match(rendered, /\}\n{2,}After\./);
});

test('normalizes tabs and preserves repeated spaces in rendered code while keeping the plain fallback exact', () => {
    const code = [
        '\tAlpha  Beta',
        '  Gamma    Delta',
        'Epsilon\t\tZeta',
    ].join('\n');
    const expectedDisplay = [
        '    Alpha  Beta',
        '  Gamma    Delta',
        'Epsilon        Zeta',
    ].join('\n');

    const { html, plain } = renderListing(`\`\`\`csharp\n${code}\n\`\`\``);
    const displayedCode = decodeRenderedText(html).replace(/\n{2,}$/, '');

    assert.equal(displayedCode, expectedDisplay);
    assert.equal(plain, code + '\n');
    assert.match(html, /&nbsp;&nbsp;&nbsp;&nbsp;Alpha &nbsp;Beta/);
    assert.match(html, /&nbsp;&nbsp;Gamma &nbsp;&nbsp;&nbsp;Delta/);
    assert.match(html, /Epsilon &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;Zeta/);
});

test('rejects an unclosed fenced snippet before returning partial output', () => {
    assert.throws(
        () => renderListing('Text before the fence.\n\n```csharp\nclass Broken {'),
        /Unclosed listing code fence/,
    );
});

test('pastePage embeds the rendered fragment in a copyable description page', () => {
    const fragment = '<p><strong>Title</strong></p><p>Body<br>line</p>';
    const page = pastePage(fragment);

    assert.match(page, /<article id="description">\s*<p><strong>Title<\/strong><\/p><p>Body<br>line<\/p>\s*<\/article>/);
    assert.match(page, /Select description/);
    assert.match(page, /selectNodeContents\(document\.getElementById\('description'\)\)/);
});

test('pastePage exposes a source-selection control whose textarea decodes to the exact fragment', () => {
    const fragment = '<p><strong>Code</strong> &amp; &lt;T&gt;</p><p>line<br>next</p>';
    const page = pastePage(fragment);
    const source = page.match(/<textarea id="source"[^>]*>([\s\S]*?)<\/textarea>/);

    assert.ok(source, 'the copy page must expose an HTML source textarea');
    assert.equal(decodeHtmlEntities(source[1]), fragment);
    assert.match(page, /id="select-source">Select HTML source<\/button>/);
    assert.match(page, /document\.getElementById\('select-source'\)/);
});

test('submission builder produces a conservative fragment and records it in the handoff', { timeout: 120_000 }, () => {
    const result = spawnSync(process.execPath, ['marketing/storefront/build-submission.cjs'], {
        cwd: ROOT,
        encoding: 'utf8',
        maxBuffer: 4 * 1024 * 1024,
        timeout: 120_000,
    });
    assert.equal(result.status, 0, `${result.error?.message ?? ''}\n${result.stdout}\n${result.stderr}`);

    const submission = path.join(ROOT, 'marketing/storefront/submission');
    const fragmentPath = path.join(submission, 'listing/upm-description-fragment.html');
    const renderedPagePath = path.join(submission, 'listing/upm-description.html');
    const plainPath = path.join(submission, 'listing/upm-description.txt');
    const fieldsPath = path.join(submission, 'portal-fields.json');
    const manifestPath = path.join(submission, 'manifest.json');
    const readmePath = path.join(submission, 'README.md');
    const draftPath = path.join(ROOT, 'marketing/storefront/portal-draft.json');
    const archivePath = path.join(ROOT, 'marketing/storefront/submission-handoff.zip');

    const fragment = fs.readFileSync(fragmentPath, 'utf8');
    const renderedPage = fs.readFileSync(renderedPagePath, 'utf8');
    const plain = fs.readFileSync(plainPath, 'utf8');
    const fields = JSON.parse(fs.readFileSync(fieldsPath, 'utf8'));
    const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
    const readme = fs.readFileSync(readmePath, 'utf8');
    const draft = JSON.parse(fs.readFileSync(draftPath, 'utf8'));

    assertConservativeAssetStoreHtml(fragment);
    assert.doesNotMatch(fragment, /```/);
    assert.ok(renderedPage.includes(fragment), 'the rendered copy page must embed the exact fragment');
    assert.match(plain, /Support and bug reports:/);

    const displayed = decodeRenderedText(fragment);
    const enumSnippet = [
        'enum Command {',
        '    Move(Vector2 destination),',
        '    Patrol(List<Vector2> waypoints, int current),',
        '    Attack(Actor target),',
        '}',
        '',
        'Command command = Command.Move(new Vector2(6.9f, 4.20f));',
        '',
        'switch (command) {',
        '    case Command.Move(Vector2 destination): {',
        '        actor.position = destination;',
        '        break;',
        '    }',
        '    case Command.Patrol(List<Vector2> waypoints, int current):',
        '    ...',
        '}',
    ].join('\n');
    const unionSnippet = [
        "[DiscriminatedUnion] // Spire's source generator takes care of all the plumbing",
        'public partial struct Command {',
        '    [Variant] public static partial Command Move(Vector2 destination);',
        '    [Variant] public static partial Command Attack(Actor target);',
        '    [Variant] public static partial Command Patrol(List<Vector2> waypoints, int current);',
        '}',
        '',
        'Command command = Command.Move(new Vector2(6.9f, 4.20f));',
        '',
        'switch (command) {',
        '    case { IsMove: true, move: var (destination) }: {',
        '        actor.position = destination;',
        '        break;',
        '    }',
        '    case { IsPatrol: true, patrol: var (waypoints, current) }:',
        '    ...',
        '}',
    ].join('\n');
    assert.ok(displayed.includes(enumSnippet), 'the enum example must retain its complete source text');
    assert.ok(displayed.includes(unionSnippet), 'the generated-union example must retain its complete source text');
    assert.match(displayed, /Validated Unity versions: 2022\.3\.62f3, 2023\.2\.22f1, and 6000\.5\.6f1\./);
    assert.match(displayed, /Support and bug reports:/);
    assert.doesNotMatch(displayed, /Publisher notes/);

    assert.equal(fields.descriptionFile, 'listing/upm-description.html');
    assert.equal(fields.descriptionHtmlSourceFile, 'listing/upm-description-fragment.html');
    assert.equal(fields.descriptionPlainTextFile, 'listing/upm-description.txt');
    assert.match(fields.descriptionFormat, /Asset Store HTML|conservative.*HTML/i);
    assert.doesNotMatch(fields.descriptionFormat, /portal.*tested|tested.*portal|portal.*verified|verified.*portal/i);

    assert.equal(draft.submission.status, 'submitted-for-vetting');
    assert.equal(draft.submission.source, 'publisher-confirmed');
    assert.equal(draft.submission.submittedBy, 'publisher');
    assert.equal(draft.submission.confirmedDate, draft.confirmedDate);
    assert.equal(draft.submission.version, draft.upload.version);
    assert.deepEqual(fields.productSubmission, draft.submission);
    assert.equal(manifest.status, 'submitted-for-vetting; awaiting Unity review');
    assert.match(readme, /submitted/i);
    assert.match(readme, /vetting/i);
    assert.doesNotMatch(readme, /complete the Portal submission/i);

    const listedFragment = manifest.files.find(file => file.file === 'listing/upm-description-fragment.html');
    assert.ok(listedFragment, 'manifest must include the conservative fragment');
    assert.equal(listedFragment.sha256, sha256(fragmentPath));
    const archiveList = spawnSync('unzip', ['-Z1', archivePath], { cwd: ROOT, encoding: 'utf8' });
    assert.equal(archiveList.status, 0, archiveList.stderr);
    assert.match(archiveList.stdout, /submission\/listing\/upm-description-fragment\.html/);
});
