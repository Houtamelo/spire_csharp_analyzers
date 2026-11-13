# r/Unity3D

Destination: https://www.reddit.com/r/Unity3D/submit

Flair: Show-Off. Use the Markdown editor for a text post. The title and body
below stand alone; the
[social card](../storefront/social-card-v1.png) or completed video can accompany
them without changing the text.

The current rules require written context and engagement, and limit a user or
project to two posts per seven days. This document supplies one announcement.

## Title

~~~~text
I built a Unity C# tool that reports forgotten switch cases at compile time
~~~~

## During vetting — body

~~~~markdown
Adding a new gameplay state or command can mean checking several systems for switches that need another branch. I built Spire to make those missing cases show up as compiler diagnostics.

It checks switch statements and switch expressions over discriminated unions, plus enums and class/interface hierarchies marked with [EnforceExhaustiveness]. When a case is missing, the diagnostic tells you which one. An IDE quick fix generates the missing branches.

The other half is discriminated unions: each variant has its own data. A command can be Move(destination, speed), Attack(target, damage), or Wait(duration). Spire generates the factories and typed payloads, and you match the variants where you consume them.

The Unity integration adds serialization and Inspector drawers. There are also generated JSON converters and support for runtime unions with Burst-compatible unmanaged payloads.

I'm the developer. The Asset Store package is currently undergoing vetting; approval and publication are pending. Its regular price will be US$20. The source repository is public and MIT-licensed.

Minimum Unity version: 2022.3.62f3. Validated Editors: 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1. Rider and Visual Studio Community workflows have been verified.

Getting Started:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity-package/Documentation%7E/GettingStarted.md

I'm happy to answer questions here about the matching checks, generated code, or Unity integration.
~~~~

## After publication — body

~~~~markdown
Adding a new gameplay state or command can mean checking several systems for switches that need another branch. I built Spire to make those missing cases show up as compiler diagnostics.

It checks switch statements and switch expressions over discriminated unions, plus enums and class/interface hierarchies marked with [EnforceExhaustiveness]. When a case is missing, the diagnostic tells you which one. An IDE quick fix generates the missing branches.

The other half is discriminated unions: each variant has its own data. A command can be Move(destination, speed), Attack(target, damage), or Wait(duration). Spire generates the factories and typed payloads, and you match the variants where you consume them.

The Unity integration adds serialization and Inspector drawers. There are also generated JSON converters and support for runtime unions with Burst-compatible unmanaged payloads.

I'm the developer. Spire is now available on the Asset Store, with a regular price of US$20. The source repository is public and MIT-licensed.

Minimum Unity version: 2022.3.62f3. Validated Editors: 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1. Rider and Visual Studio Community workflows have been verified.

Asset Store:
https://assetstore.unity.com/packages/package/6893155

Getting Started:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity-package/Documentation%7E/GettingStarted.md

I'm happy to answer questions here about the matching checks, generated code, or Unity integration.
~~~~

## Reply — doesn't C# already check switches?

~~~~text
C# already diagnoses some incomplete switch expressions. Spire also checks switch statements and handles the known cases in annotated class/interface hierarchies and generated unions. It lists missing cases and provides IDE quick fixes to generate the branches. An unguarded default/catch-all opts out of the exhaustiveness check.
~~~~

## Reply — why use this instead of virtual methods?

~~~~text
Virtual methods work well when behavior belongs on the objects themselves. A union is useful when several systems need to interpret the same data differently: an execution system, UI preview, serializer, or replay system can each handle the variants in its own switch. Each variant keeps its own typed payload.
~~~~

## Reply — is everything allocation-free?

~~~~text
Struct unions can hold unmanaged payloads without allocating a separate object for each variant. That does not make every use allocation-free: reference payloads retain their own allocation costs, Shared storage can box structs containing managed references, and generated Unity serialization adds a managed cache. The storage choices are documented here:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/discriminated-unions.md
~~~~

## Reply — is this open source, and why is there a paid package?

~~~~text
Yes. The source repository is public and MIT-licensed. The US$20 Asset Store product is the packaged Unity distribution with the runtime, compiler extensions, IDE quick fixes, offline documentation, and samples. The source remains available here:
https://github.com/Houtamelo/spire_csharp_analyzers/tree/unity-6000
~~~~

## Reply — compatibility details

~~~~text
The package minimum is Unity 2022.3.62f3. The validated Editors are 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1, with Linux and Windows x64 Editor and Mono/IL2CPP player checks. Unity 2022's native serializer does not support generic unions. Unity 6000.6.5f1 on Linux has a documented serialization crash involving generic unions with reference-type arguments. Exact coverage and limitations:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity.md
~~~~

Rules checked November 7, 2026:
[r/Unity3D rules in the community sidebar](https://www.reddit.com/r/Unity3D/).
