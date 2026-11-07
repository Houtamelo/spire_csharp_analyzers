# Confirmed Portal identity and upload media

The publisher supplied the product draft identity on November 7, 2026:

- Title: **Spire C# - Exhaustive Matching & Discriminated Unions**.
- Structure: **Single package**; product namespace `spire`.
- Package name: `individual.antonio-pedro-goncalves-ferreira.spire`.

The namespace is visible during product draft creation. At this identity-handoff
stage, the agent had not independently inspected the Portal or uploaded content.
The later official SDK upload is recorded in the
[validation report](2026-11-07-official-upm-validation.md). The publisher confirmed
submission for vetting on November 7; Unity approval remains pending. The exact
values are retained in [portal-draft.json](../../marketing/storefront/portal-draft.json).

## Ready for the Portal

The [submission handoff](../../marketing/storefront/submission/README.md) and
ZIP now match the chosen title. Summary is 195 characters. A combined
`listing/upm-description.md` contains the feature description, technical
requirements/limitations, and support information for the UPM description field;
internal publisher notes are excluded.

After the publisher observed literal Markdown and HTML tags in the Portal's
preview, the handoff also gained `listing/upm-description.html`: open it in a
browser and copy its rendered description into the rich-text editor. A `.txt`
fallback is included. Earlier Chromium checks of that renderer preserved both
code examples, rendered all 17 list items, escaped generic type brackets, and
selected only the description. That historical evidence is under
`tmp/portal-description-20261107/`.

The current renderer uses only p, strong, em, a, and br tags, preserving code
indentation with non-breaking spaces. The HTML/source fragment is available as
`listing/upm-description-fragment.html`; the copy page offers separate rendered
and source selections. Seven Node tests cover escaping, exact code layout,
unclosed-fence rejection, source copying, and the real handoff build/archive.
These checks do not establish the Cloud Portal's rendering of the current copy.

The publisher subsequently confirmed that Preview still displays literal
`<p><span>` tags after this approach. A paragraph typed directly into the editor
also displays HTML tags, isolating the issue from Markdown conversion and our
clipboard content. The affected draft is
<https://cloud.unity.com/assetstore/publisher/products/6893155>, in the newer
Cloud UPM Publisher Portal. The older publisher.unity.com formatting guidance
does not establish the Cloud editor's behavior. Exact failure location
(editor, save/load, or preview) and impact on a published listing remain unknown.
No working workaround or matching official issue report has been confirmed.
See the [support reproduction](2026-11-07-upm-description-preview.md).
The publisher later resolved the preview issue; its cause and resolution were
not supplied. It is no longer a submission blocker.

The existing approved cover, card, icon, and seven gallery slides are preserved
byte-for-byte. The new optional social image is a **draft**, composed from the
existing logo, colors, fonts, and headline with an editable SVG and reproducible
`build-social.cjs`. Dimensions are checked from actual PNG headers:

| Portal field | File in submission | Dimensions |
| --- | --- | --- |
| Marketing image | `images/cover.png` | 1950 x 1300 |
| Product card | `images/card.png` | 420 x 280 |
| Icon | `images/icon.png` | 160 x 160 |
| Gallery | `images/gallery/01` through `07` | 1950 x 1300 each |
| Social media, optional draft | `images/social.png` | 1200 x 630 |

The handoff manifest records its files and hashes. Every manifest hash and ZIP member is verified;
the seven standalone gallery ZIP images match their approved capture manifest.
Media evidence is under `tmp/upm-portal-media-20261107/`.

## Matching package candidate

The previous local package name was `com.houtamelo.spire`. Export, signing,
installation helpers, disposable projects, current installation docs, and their
regression assertions now use the confirmed Portal name. C# namespaces, assembly
names, public API, and authored/generated asset GUID policy remain unchanged.
Captured historical compiler fixtures and player application identifiers retain
their original values: neither is a current UPM package identity declaration.

The signed candidate is
`tmp/upm-identity-20261107/signed/individual.antonio-pedro-goncalves-ferreira.spire-4.7.6.tgz`,
SHA-256 `a16057e5bc7873d11568a99093730c8e81490c2488d53e2d3cb02645e38075de`.
All four DLL hashes match the previously validated artifact.

- Unity 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1 on Linux and Windows: the
  exact signed artifact passes installation, 115-file payload verification,
  compiler import settings/labels, actual Quick Start import, generated APIs,
  the negative accessor assertion, and native JSON persistence.
- Unity 6000 reports **Valid** on both platforms. Older Editors retain their
  explicitly accepted **Unsigned/Unchecked** limitation; they are not reported
  as signature-verified.
- The new-name artifact with one changed README byte reports **Invalid** and
  fails Linux Unity 6000 acceptance. The complete original tar/signing stream is
  preserved by that control.
- Linux DevTools passes **179 tests** with **8 Windows exclusions**, including a
  new control rejecting archives using the former local package name.
- The existing Unity 6000 customer workflow passes all **16 Showcase tests**.

The first tooling run exposed a stale Debug CLI: customer tests hardcoded that
launcher even when running Release tests. Their launcher now follows the test
configuration, including the Editor harness override; success/failure assertions
remain intact. The failed attempt is retained. The first guest script dispatch
could not access the transient share during boot; its retry ran all three
Windows Editors successfully.

Evidence is under `tmp/upm-identity-20261107/`, with Windows results under
`~/.local/share/spire-windows/virtualbox/results/upm-identity-20261107-accepted/`.
Fixtures remain under `tests/`; live examples were not validation inputs or
outputs. That identity-validation artifact retained version **4.7.6** and minimum
**6000.5.6f1**. It has since been superseded by the publisher-requested
[Unity 2022 minimum candidate](2026-11-07-upm-unity-2022-minimum.md).
The full compiler/Inspector/player/browser/IDE matrices were not rerun for this
identity change; their four shipped DLLs are unchanged.

Official UPM validation and upload are complete with Publishing Tools 0.3.3 for
version 4.7.6, minimum 2022.3.62f3. The publisher has submitted the product for
vetting. Unity approval, publication, and customer Store acquisition remain
separate pending gates; see the linked validation report for current evidence.
