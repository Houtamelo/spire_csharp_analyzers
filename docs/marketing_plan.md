# Houtamelo.Spire marketing plan

Decision record for the Unity Asset Store launch. Keep storefront material focused
on developer benefits; explain implementation details in user-facing documentation.

## Positioning

Primary promise: **Save time by catching errors at compile time.**

Lead with exhaustive matching and faster maintenance, then introduce discriminated
unions as a way to express gameplay data. Unity Inspector integration, JSON
serialization, and Burst/DOTS compatibility support that story.

Avoid positioning Spire as "safer C#": the intended message is compile-time
correctness and time saved, rather than memory safety.

## Storefront messages

### Exhaustive matching

- Exhaustive matching for enums, discriminated unions, and class/interface
  hierarchies, without requiring a `default:` or catch-all case.
- Missing cases produce a compile-time error that lists what is missing.
- Adding an enum member, union variant, derived class, or interface implementation
  exposes the switches that need updating, saving manual searches for consumers.
- IDE quick fixes generate the missing cases for you.

Describe the feature as "IDE quick fixes." Spire uses standard Roslyn code-fix
providers, which Visual Studio Community also supports. Rider is the verified
workflow and gallery capture host. Windows 10 and end-to-end Unity integration in
Visual Studio Community are being verified in a separate session. The listing
is being drafted assuming those checks succeed; results are still pending here.

Suggested demonstration: add a new interface implementation, show the resulting
missing-case error, generate the missing branch through the IDE context menu,
then implement its behavior.

### Discriminated/tagged unions

Introductory copy:

> Give each variant its own data.
>
> Represent commands, states, and results with distinct payloads. Let each
> consuming system decide how to handle them through exhaustive pattern matching.

Explain unions as enum variants with associated data. A concrete example is
`Move(destination)`, `Attack(target, damage)`, and `Wait(duration)`. Emphasize that
each consumer can interpret the same data without putting every operation on the
data type through interfaces or abstract methods.

Storefront feature bullets:

- Struct-based unions with allocation-free typed storage.
- Unity serialization with generated Inspector drawers.
- Automatically generated JSON serialization and deserialization, supporting
  System.Text.Json and Newtonsoft.Json.
- Burst/DOTS compatibility for data-oriented gameplay code.

## Storefront detail policy

Keep the storefront copy simple. Explain storage modes, boxing, serialization
cache lifecycles, supported payload types, and Burst field constraints in the
user-facing documentation rather than expanding every storefront bullet into
implementation caveats.

Use "Unity serialization with generated Inspector drawers" for Inspector support.
Keep the Unity 6000.6.5f1 exception specific to generic unions with reference-type
arguments. Current native Editor write/read checks pass for non-generic unions
and tested unmanaged generic unions; `UnityGenericUnion<string>` still crashes.
See the [November 2 recheck](reviews/2026-11-02-unity-6.6-serialization-recheck.md).

The November 3 follow-up passes the engine repro and the preserved Spire
generic-reference-union probe on `7000.0.0a12`, including fresh-process asset
reads. The same inputs still reproduce the 6000.6.5f1 serialization crash.
Unity 7 uses CoreCLR here; 6.6 uses Mono. Per the publisher's decision, treat
Unity issue `25665`, reported fixed in `7000.0.0a5`, as a shared-root-cause working
assumption. State on the storefront that the crash is fixed in Unity 7000,
verified on `7000.0.0a12`, and that a `6000.6.x` backport is expected soon.
Exact attribution and the backport's availability and timing remain unconfirmed.
Retain the measured 6000.6.5f1 limitation until a relevant 6.x version passes the
repro. See the [Unity 7 comparison](reviews/2026-11-03-unity-7-serialization-recheck.md).

Unity `7000.0.0a12` Linux IL2CPP follow-up passes all 100 original behavioral tests
and 60 native runtime/serialization checks with High stripping. The normal NUnit
run passes all 107 cases without filtering, including seven bridge contracts.
An unrelated subprocess capability probe was removed from the harness.
See the [IL2CPP validation](reviews/2026-11-03-unity-7-il2cpp-validation.md).

Do not pursue Unity 2020 compatibility. The public wording is **"Unity 2020 will
not work."** Non-extended Unity 2021/2022 LTS verification is complete to the
documented boundaries. Unity 2021 cannot load Spire's Roslyn 4.3 baseline.
Unity 2022 passes the Editor harness. The concrete-wrapper follow-up passes native
serialization write/read checks for non-generic unions; generic unions remain
unsupported by its serializer. Newer Editors retain existing generic-union
support, and wrappers use the same generation path on all Unity versions.
The current full Editor harness passes 2,689 cases on both Unity 2022 and 2023.
Both also pass applicable native serialization and actual Mono/IL2CPP player
checks; native generic serialization is excluded from the Unity 2022 fixture.
The separate rendered Inspector suite now passes on 2022.3.62f3, 2023.2.22f1,
and 6000.5.6f1 after two Editor drawer corrections. Unity 2022's unsupported
closed-generic Inspector serialization case is explicitly not applicable.
See the [rendered validation](reviews/2026-11-05-rendered-inspector-validation.md)
for the later DLL and exact revalidation scope. Keep the published minimum
unchanged. See the
[current-package recheck](reviews/2026-11-05-unity-2022-2023-current-validation.md).

Use the simple Burst/DOTS compatibility message on the storefront; document that
a union cannot simultaneously use generated Unity serialization and be unmanaged
Burst/ECS data. Users needing both representations declare separate authoring and
runtime unions and convert or bake between them.

Technical references:

- [Exhaustive matching](exhaustiveness.md)
- [Union storage and generated JSON support](discriminated-unions.md)
- [Unity integration and field support](unity.md)
- [Customer serialization guide](unity-package/Documentation~/Serialization.md)

## Visual identity

The selected logo is the standalone slime-covered capital S, using the user's
edited image with slight blur:
[selected logo](../marketing/brand/logo.png).
Use this exact image as the source for marketing material.

The publisher supplied small-format exports:
[160 x 160 icon](../marketing/brand/logo_160x160.png) and
[256 x 256 logo](../marketing/brand/logo_256x256.png). Preserve these exports; the
approved cover and card layouts retain the full-size source artwork.

The palette is magenta, deep purple, and near-black. The source palette used
during logo cleanup was:

| Color | RGB |
|---|---|
| Purple | 62, 17, 73 |
| Magenta | 200, 11, 251 |
| Black | 7, 5, 10 |

The selected edited/blurred image remains authoritative; these palette values do
not require re-quantizing it. Keep the logo's shading simple and its silhouette
readable at small icon sizes.

The circular circuit badge and separate circuit backgrounds are exploratory
assets. The selected logo consists only of the standalone S.

## Planned launch material

The publisher prefers the original cover layout: the selected logo on the left
and the product name and benefit-focused headline on the right. Revision 2
retains its artwork, split background, Poppins SemiBold and Source Sans 3 Regular
fonts, and accent bars, with the revised text below. Card v3 retains this visual
design and adapts its typography for the smaller size: remove both accent bars,
move the title and headline up, and use 16 px bullets wrapped onto two lines each.
Cover revision 2 and card revision 3 are approved for the first release.

- [Cover mockup v2 PNG, 1950 x 1300](../marketing/storefront/cover-mockup-v2.png)
- [Card v3 PNG, 420 x 280](../marketing/storefront/card-mockup-v3.png)
- [Editable card v3 SVG](../marketing/storefront/card-mockup-v3.svg)
- [Earlier directly resized card](../marketing/storefront/cover-mockup-v2-card.png)
- [Editable v2 SVG with embedded logo](../marketing/storefront/cover-mockup-v2.svg)
- [Shared cover/card rebuild script](../marketing/storefront/build-cover.cjs)
- [Cover mockup v1 PNG](../marketing/storefront/cover-mockup-v1.png)
- [Editable SVG with embedded logo](../marketing/storefront/cover-mockup-v1.svg)
- [420 x 280 readability preview](../marketing/storefront/cover-mockup-v1-thumbnail.png)

The cover is 1950 x 1300, following [Unity's cover image template](https://assetstore.unity.com/publishing/image-template).
The 420 x 280 card uses the same artwork with larger bullet text for readability.
The 160 x 160 icon is supplied above. Earlier dedicated card layouts are retained
as explorations:

- [Card mockup v2 PNG](../marketing/storefront/card-mockup-v2.png)
- [Editable card v2 SVG with embedded logo](../marketing/storefront/card-mockup-v2.svg)
- [Card mockup v1 PNG](../marketing/storefront/card-mockup-v1.png)
- [Editable card SVG with embedded logo](../marketing/storefront/card-mockup-v1.svg)
- [Rebuild script](../marketing/storefront/build-card.cjs)

The revised cover and card use the display title "Spire" and the publisher's copy:

> Save time by catching more errors before even running the game.

Highlight "running the game" in the bright brand purple. Use arrow bullets for:

- Exhaustive matching for enums/classes/interfaces
- Lightweight Discriminated Unions
- Performance without sacrificing ergonomics

Rebuild the cover and its earlier directly resized card with
`node marketing/storefront/build-cover.cjs`. Rebuild the current card with
`node marketing/storefront/build-card.cjs v3`. Earlier dedicated cards use the
`v2` or `v1` argument.

The third screenshot gallery revision is approved for the first release. Each
image is 1950 x 1300 and uses real Unity Editor or Rider captures with the selected
logo and palette.

- [Gallery review page](../marketing/storefront/gallery/gallery.html)
- [Contact sheet](../marketing/storefront/gallery/gallery-contact-sheet.png)
- [Seven gallery PNGs, revision 3](../marketing/storefront/gallery-pngs-v3.zip)
- [Capture sources, layout details, and verification](../marketing/storefront/gallery/manifest.json)

The gallery opens with a missing-case diagnostic and the IDE quick fix, then
shows variant payloads, exhaustive union switch statements, generated Inspector
drawers, generated JSON converters, and native Burst jobs. Editable SVGs and raw
captures accompany the final PNGs. Earlier PNG archives are retained.

All code snippets come from Rider. The JSON and Burst results come from
capture-only Unity example panels executing real generated code; these panels
are demonstrations, not additional shipped product windows. The captures
verified a JSON round trip, native Burst execution, and the saved quick-fix
transformation. Houtamelo approved this revision for the first release; it has
not been published.

The [storefront listing copy](asset_store_listing.md) has been reviewed and is
good enough for the first release. It includes the package title, short summary,
full description, compatibility and installation notes, documentation/support
information, and keywords. It leads with the compile-time error detection
promise and follows the gallery's feature order. Its publisher notes track the
remaining validation and delivery decisions.

The cover and card are ready for the first release. The publisher is handling
the demonstration video in a separate session. Use the exhaustive-matching
workflow above as the opening video demonstration.

## Submission handoff

The publisher chose a regular price of **20 USD**. A publisher profile already
exists. UPM enrollment was submitted on November 3, 2026 and is awaiting approval;
Unity quoted an estimated 5–7 business days. UPM is the planned delivery route,
pending enrollment approval, and the existing package builder produces that format.

The [submission handoff](../marketing/storefront/submission/README.md) collects
the approved cover v2, card v3, 160 x 160 icon, seven gallery v3 images, and the
listing fields. Its manifest records image dimensions and SHA-256 hashes; the
gallery copies match the approved capture manifest. The regular price is stored
in its portal-fields.json. [Download the handoff ZIP](../marketing/storefront/submission-handoff.zip).

The next account milestone is approval of the submitted
[UPM enrollment](https://cloud.unity.com/assetstore/publisher/enrollment/).
Unity documents **Active** as the successful admittance status. Once active,
create a single-package product draft and reserve the technical name before
validating and uploading the software bundle. See the
[current enrollment guide](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/apply)
and [product draft guide](https://docs.unity.com/en-us/asset-store/publishing/upm-packages/create-draft).

No Portal draft or software upload has been created in this session. The handoff
contains marketing material; the release bundle, final version, and compatibility
results remain separate. Rebuild the handoff with
`node marketing/storefront/build-submission.cjs`.

## Gallery revision decisions

- Keep Rider's default theme. Capture every code snippet inside Rider.
- Show data-bearing, unsealed interface implementors and the entire incomplete
  switch. Suppress unrelated IDE inspections in the capture project.
- Use "Spire immediately reports all switches that need updating" and include
  enums, classes, interfaces, structs, and discriminated unions in the scope line.
- Highlight the branch created by the IDE quick fix.
- Describe unions as struct-based, lightweight data with compact storage and no
  additional heap allocation; keep storage qualifications in the documentation.
- Place a union switch-statement demonstration directly after the union
  declaration image. Remove the Play Mode image from this gallery.
- Match the code fix's pattern style: deconstruct variant payloads, for example
  `case { IsMove: true, move: var (destination, speed) }:`. Mention that the quick
  fix menu can generate missing cases in switch statements too.
- Pair serialized field declarations with a narrow Inspector capture on the
  right. Mention Odin Inspector compatibility.
- Show JSON and Burst code in Rider, with actual Unity results alongside it.
- Emphasize polymorphism within Burst's type restrictions and DOTS compatibility.
- On the Burst image, use half the former width for the union/job declaration
  capture and show the complete `Execute` method beside it, including its switch
  over commands read from `NativeArray<MotionCommand>`.
