// Asset Store's documented subset omits code/pre and lists. Preserve layout with p/br.
const escape = value => value.replace(/&/g, '&amp;').replace(/</g, '&lt;')
    .replace(/>/g, '&gt;').replace(/"/g, '&quot;');

function inline(text) {
    const pattern = /`([^`]+)`|\*\*([^*]+)\*\*|\*([^*]+)\*|\[([^\]]+)\]\((https:\/\/[^\s)]+)\)/g;
    let output = '', start = 0;
    for (const match of text.matchAll(pattern)) {
        output += escape(text.slice(start, match.index));
        if (match[1]) output += escape(match[1]);
        else if (match[2]) output += '<strong>' + escape(match[2]) + '</strong>';
        else if (match[3]) output += '<em>' + escape(match[3]) + '</em>';
        else output += '<a href="' + escape(match[5]) + '">' + escape(match[4]) + '</a>';
        start = match.index + match[0].length;
    }
    return output + escape(text.slice(start));
}

function renderListing(markdown) {
    const lines = markdown.trim().split(/\r?\n/), html = [], plain = [];
    const textOnly = text => text.replace(/`([^`]+)`|\*\*([^*]+)\*\*|\*([^*]+)\*|\[([^\]]+)\]\((https:\/\/[^\s)]+)\)/g,
        (_, code, bold, italic, label, url) => label ? label + ' (' + url + ')' : code ?? bold ?? italic);
    for (let i = 0; i < lines.length;) {
        const line = lines[i];
        if (!line.trim()) { i++; continue; }
        if (/^```\w*$/.test(line)) {
            const code = [];
            i++;
            while (i < lines.length && lines[i] !== '```') code.push(lines[i++]);
            if (i === lines.length) throw new Error('Unclosed listing code fence');
            i++;
            const formatted = code.map(line => escape(line.replace(/\t/g, '    '))
                .replace(/^ +| {2,}/g, (spaces, offset) => offset === 0
                    ? '&nbsp;'.repeat(spaces.length)
                    : ' ' + '&nbsp;'.repeat(spaces.length - 1)));
            html.push('<p>' + formatted.join('<br>') + '</p>');
            plain.push(code.join('\n'));
        } else if (/^#{1,6} /.test(line)) {
            const title = line.replace(/^#{1,6} /, '');
            html.push('<p><strong>' + inline(title) + '</strong></p>');
            plain.push(textOnly(title));
            i++;
        } else if (line.startsWith('- ')) {
            const items = [];
            while (i < lines.length && lines[i].startsWith('- ')) {
                let item = lines[i++].slice(2);
                while (i < lines.length && /^ {2}\S/.test(lines[i])) item += ' ' + lines[i++].trim();
                items.push(item);
            }
            html.push('<p>' + items.map(item => '• ' + inline(item)).join('<br>') + '</p>');
            plain.push(items.map(item => '• ' + textOnly(item)).join('\n'));
        } else {
            const paragraph = [lines[i++]];
            while (i < lines.length && lines[i].trim() && !/^(?:#{1,6} |```|- )/.test(lines[i]))
                paragraph.push(lines[i++].trim());
            const text = paragraph.join(' ');
            html.push('<p>' + inline(text) + '</p>');
            plain.push(textOnly(text));
        }
    }
    return { html: html.join('\n'), plain: plain.join('\n\n') + '\n' };
}

function pastePage(html) {
    return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Spire — description to copy</title>
<meta name="viewport" content="width=device-width, initial-scale=1">
<style>
body { max-width: 860px; margin: 32px auto; padding: 0 24px; font: 16px/1.5 sans-serif; color: #222; }
aside { padding: 16px; margin-bottom: 24px; background: #f0edf8; border: 1px solid #d2c6ee; }
aside p { margin-top: 0; } button { padding: 8px 14px; font: inherit; cursor: pointer; }
textarea { box-sizing: border-box; width: 100%; height: 220px; font: 13px/1.4 monospace; }
</style></head><body>
<aside><p>Click <strong>Select description</strong>, press Ctrl+C, then replace the Publisher Portal's
Description using Ctrl+V (normal paste, with formatting). For a rich-text editor, copy the rendered
description below. Only paste HTML source if the editor has an explicit HTML/source mode.
Raw Markdown will not format. This page contains the repository's listing copy;
retain any additional wording edits you made in the Portal and review its preview after pasting.</p>
<p>Code examples use explicit line breaks and non-breaking spaces for indentation.
They do not rely on code-block tags, custom fonts, or syntax highlighting.
This local preview does not verify the Portal's rendering.</p>
<button type="button" id="select">Select description</button>
<details><summary>HTML source (for an explicit HTML/source mode only)</summary>
<p>The same fragment is available in <a href="upm-description-fragment.html">upm-description-fragment.html</a>.</p>
<button type="button" id="select-source">Select HTML source</button>
<textarea id="source" aria-label="Description HTML source" readonly>${escape(html)}</textarea>
</details></aside>
<article id="description">
${html}
</article>
<script>
document.getElementById('select').addEventListener('click', () => {
    const range = document.createRange();
    range.selectNodeContents(document.getElementById('description'));
    const selection = window.getSelection();
    selection.removeAllRanges();
    selection.addRange(range);
});
document.getElementById('select-source').addEventListener('click', () => {
    document.getElementById('source').select();
});
</script></body></html>
`;
}

module.exports = { renderListing, pastePage };
