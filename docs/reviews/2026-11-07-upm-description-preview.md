# Cloud UPM Publisher Portal renders description HTML as text

Resolved by the publisher on November 7, 2026. The cause and resolution were not
provided; retain the following reproduction as history, not a current blocker.

Publisher-reported on November 7, 2026. Not independently reproduced by the
agent: no signed-in browser is connected. This was a Portal issue reported by the publisher,
not a validated defect in Spire's package or a confirmed public Unity issue.

## Support request draft

Subject: UPM Publisher Portal description Preview displays literal HTML tags

Product draft: <https://cloud.unity.com/assetstore/publisher/products/6893155>

Product: Spire C# - Exhaustive Matching & Discriminated Unions

Technical package name:
`individual.antonio-pedro-goncalves-ferreira.spire`

In the description editor, Preview displays paragraph markup as text, such as
`<p><span>Save time by catching errors at compile time.</span></p>`.

Steps observed by the publisher:

1. Paste a description into the product draft's rich-text description editor.
2. Select Preview. HTML paragraph/span tags display literally.
3. Repeat using rendered browser content rather than Markdown or HTML source.
   The same issue persists.
4. Type a new paragraph containing `Formatting test` directly into the editor.
   Preview also displays literal HTML tags for this manually typed paragraph.

Expected: Preview displays the description's readable, formatted text.

Actual: Preview displays HTML markup. Directly typed text reproduces the
problem independently of the pasted description's format.

Please confirm whether this is a Preview-only defect or also affects saved
product descriptions and published storefront pages, and provide a supported
workaround.

Browser/version, save/reload behavior, and reproduction in a second browser
have not yet been recorded. No request has been sent.

## Next checks

Save the draft, reload its page, and reopen Preview. If it persists, repeat the
typed-paragraph check in another browser or a clean browser profile. These
checks distinguish transient editor state from persistent data/preview behavior
and browser-specific interference. Keep a copy of the description before edits.

Local HTML rendering passes, but is not evidence that the Portal accepts it.
The HTML and plain-text handoff files are copy sources, not verified workarounds
for this failure. Do not infer the published storefront result from this preview.

Official [UPM Portal entry instructions](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/create-draft)
identify the Cloud Dashboard workflow. Unity's
[support form](https://support.unity.com/hc/en-us/requests/new) is the escalation
route if the issue persists.
