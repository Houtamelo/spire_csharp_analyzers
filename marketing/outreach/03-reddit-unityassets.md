# r/UnityAssets

Destination: https://www.reddit.com/r/UnityAssets/submit

Post flair/category: Editor Extensions. Set the price/category flair to the
text below where editable. Keep the price out of the title.

Attachment: [social card](../storefront/social-card-v1.png).
Copy the body matching the publication state into the post text.

## Title

~~~~text
Spire C#: Exhaustive matching, IDE quick fixes, and discriminated unions for Unity
~~~~

## During vetting — price/category flair

~~~~text
Editor Extensions | US$20 regular price | Pending vetting
~~~~

## During vetting — body

~~~~markdown
I'm Houtamelo, the developer of Spire. It helps you catch missing switch cases at compile time and generate the missing branches with IDE quick fixes.

Spire supports exhaustive matching for discriminated unions and annotated enums, classes, and interfaces. Its struct-based unions give each variant its own typed payload, so commands, states, and results can carry the data they need.

The Unity package includes:

- Generated factories and typed variant payloads.
- Unity serialization and Inspector drawers.
- Generated JSON converters for System.Text.Json and Newtonsoft.Json.
- Runtime unions compatible with Burst/DOTS when their payloads meet Burst's restrictions.
- Offline guides and optional samples.

Minimum Unity version: 2022.3.62f3. Validated Editors: 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1. Verified IDE workflows: Rider and Visual Studio Community.

The package has been submitted for Asset Store vetting. Approval and publication are pending. The regular price will be US$20.

Documentation:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity-package/Documentation%7E/GettingStarted.md

I'm available in the comments for technical questions.
~~~~

## After publication — price/category flair

~~~~text
Editor Extensions | US$20 regular price
~~~~

## After publication — body

~~~~markdown
Spire is now available on the Unity Asset Store. I'm Houtamelo, its developer.

It helps you catch missing switch cases at compile time and generate the missing branches with IDE quick fixes. Exhaustive matching covers discriminated unions and annotated enums, classes, and interfaces.

Its struct-based unions give each variant its own typed payload, so commands, states, and results can carry the data they need.

The Unity package includes:

- Generated factories and typed variant payloads.
- Unity serialization and Inspector drawers.
- Generated JSON converters for System.Text.Json and Newtonsoft.Json.
- Runtime unions compatible with Burst/DOTS when their payloads meet Burst's restrictions.
- Offline guides and optional samples.

Regular price: US$20.
https://assetstore.unity.com/packages/package/6893155

Minimum Unity version: 2022.3.62f3. Validated Editors: 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1. Verified IDE workflows: Rider and Visual Studio Community.

Documentation:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity-package/Documentation%7E/GettingStarted.md

I'm available in the comments for technical questions.
~~~~

## Reply — what is included, and is Odin required?

~~~~text
The package includes the runtime library, analyzers, source generator, IDE quick fixes, generated Inspector integration, offline documentation, and optional samples. Odin Inspector is compatible but optional. The Quick Start sample has no additional package dependencies; the optional URP Showcase targets Unity 6000.5.6f1 with URP 17.5.0 and Unity Test Framework 1.7.0.
~~~~

## Reply — are JSON libraries included?

~~~~text
Spire generates the converter code. Install the JSON library you select in your Unity project: System.Text.Json, Newtonsoft.Json, or both. Spire does not bundle those libraries.
~~~~

## Reply — can I inspect the source?

~~~~text
Yes. The Unity branch is public and the source is MIT-licensed:
https://github.com/Houtamelo/spire_csharp_analyzers/tree/unity-6000
~~~~

Rules checked November 7, 2026:
[r/UnityAssets sidebar](https://www.reddit.com/r/UnityAssets/).
The title follows the required product-name/description format, contains no
price, and is below the 300-character limit. Identical reposts are prohibited.
