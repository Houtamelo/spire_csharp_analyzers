# Unity Discussions

Destination: https://discussions.unity.com/c/community-showcases/47

Create one topic in Community Showcases with the Asset-Store-Assets tag. Keep
replies and product updates in that topic. Copy the title and the body matching
the product's publication state.

Attachments:
[missing cases](../storefront/submission/images/gallery/01-exhaustive-matching.png),
[quick fix](../storefront/submission/images/gallery/02-generate-missing-branches.png),
and [Inspector](../storefront/submission/images/gallery/05-unity-inspector.png).
The post remains complete without the video.

## Title

~~~~text
Spire C#: exhaustive matching and discriminated unions for Unity
~~~~

## During vetting — body

~~~~markdown
I'm Houtamelo, a C# and Rust developer. I built Spire to save time by catching forgotten cases at compile time.

Add an enum member, a union variant, or a concrete type in an enforced class/interface hierarchy, and Spire reports the switches that need updating. Its IDE quick fixes generate the missing branches so you can fill in their behavior.

Both switch statements and switch expressions are supported. Use [EnforceExhaustiveness] on existing enums and class/interface hierarchies; generated discriminated unions have their own exhaustiveness checks. You can cover every case explicitly without adding a default branch.

Spire also gives each union variant its own data. For example, a gameplay command can be Move(destination, speed), Attack(target, damage), or Wait(duration), with typed payloads for each case.

The Unity package includes:

- Exhaustive matching and IDE quick fixes.
- Struct-based discriminated unions with generated factories and typed payloads.
- Unity serialization and Inspector drawers, including variant fields inside lists.
- Generated JSON converters for System.Text.Json and Newtonsoft.Json.
- Burst/DOTS-compatible runtime unions when their payloads meet Burst's restrictions.
- Offline documentation and optional Quick Start and Unity Showcase samples.

Rider and Visual Studio Community workflows have been verified. The minimum Unity version is 2022.3.62f3; the validated Editors are 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1. On Unity 2022, use non-generic unions for native Unity serialization.

I've submitted the package to the Asset Store for vetting. Approval and publication are pending. The regular price will be US$20.

Getting Started:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity-package/Documentation%7E/GettingStarted.md

Compatibility and limitations:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity.md

I'm available here to answer questions about using it with gameplay commands, states, results, and existing code.
~~~~

## After publication — body

~~~~markdown
I'm Houtamelo, a C# and Rust developer. Spire is now available on the Unity Asset Store.

I built it to save time by catching forgotten cases at compile time. Add an enum member, a union variant, or a concrete type in an enforced class/interface hierarchy, and Spire reports the switches that need updating. Its IDE quick fixes generate the missing branches so you can fill in their behavior.

Both switch statements and switch expressions are supported. Use [EnforceExhaustiveness] on existing enums and class/interface hierarchies; generated discriminated unions have their own exhaustiveness checks. You can cover every case explicitly without adding a default branch.

Spire also gives each union variant its own data. For example, a gameplay command can be Move(destination, speed), Attack(target, damage), or Wait(duration), with typed payloads for each case.

The Unity package includes:

- Exhaustive matching and IDE quick fixes.
- Struct-based discriminated unions with generated factories and typed payloads.
- Unity serialization and Inspector drawers, including variant fields inside lists.
- Generated JSON converters for System.Text.Json and Newtonsoft.Json.
- Burst/DOTS-compatible runtime unions when their payloads meet Burst's restrictions.
- Offline documentation and optional Quick Start and Unity Showcase samples.

Rider and Visual Studio Community workflows have been verified. The minimum Unity version is 2022.3.62f3; the validated Editors are 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1. On Unity 2022, use non-generic unions for native Unity serialization.

Spire on the Asset Store — regular price US$20:
https://assetstore.unity.com/packages/package/6893155

Getting Started:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity-package/Documentation%7E/GettingStarted.md

Compatibility and limitations:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity.md

I'm available here to answer questions about using it with gameplay commands, states, results, and existing code.
~~~~

## Reply — do I have to replace existing enums or classes?

~~~~text
You can keep them. Apply [EnforceExhaustiveness] to an existing enum, abstract class, or interface to check its switches. Use [DiscriminatedUnion] when you want a new type whose variants carry different payloads. The two features can be used independently.
~~~~

## Reply — what happens with default or a catch-all?

~~~~text
An unguarded catch-all opts out of the exhaustiveness check. Leave it out when you want additions to the type to surface as missing-case diagnostics. A guarded catch-all does not necessarily cover every remaining value.
~~~~

## Reply — can one union support the Inspector and Burst?

~~~~text
Generated Unity serialization adds a managed cache reference. For Burst jobs and unmanaged ECS components, use a runtime union without [Serializable], with Burst-supported unmanaged payloads. If you need Inspector authoring too, declare a separate serializable authoring union and convert or bake it into the runtime union.
~~~~

## Reply — bug report

~~~~text
Please open an issue with your exact Unity version, operating system, IDE, and a minimal example of the union or switch. For a player-only issue, include the target platform, Mono/IL2CPP backend, and managed stripping level:
https://github.com/Houtamelo/spire_csharp_analyzers/issues
~~~~

Publication guidance checked November 7, 2026:
[category description](https://discussions.unity.com/t/about-the-community-showcases-category/1482328)
and [existing asset topics](https://discussions.unity.com/c/community-showcases/47).
