# Discord communities

Destination: the server's designated promotion, tools, or showcase channel.
No server names or channel permissions have been supplied. Use the moderator
message below if the server does not identify a permitted channel. Having
Discord does not establish that the publisher operates a support server.

Each message and reply is a separate copy block under 2,000 characters.
The announcement works as text alone; attach the completed video separately
when available. Do not combine all the replies into the announcement.

## During vetting — announcement

~~~~text
I built **Spire**, a Unity C# package that catches forgotten switch cases at compile time.

Add a case to a discriminated union or a type marked with [EnforceExhaustiveness], and Spire reports the switches that need another branch. IDE quick fixes generate the missing branches.

Its discriminated unions also give each variant its own typed data: Move(destination, speed), Attack(target, damage), or Wait(duration). The Unity package includes generated Inspector drawers and JSON converters.

I'm Houtamelo, the developer. The package is currently undergoing Asset Store vetting; approval and publication are pending. The regular price will be US$20.

Minimum Unity: 2022.3.62f3. Verified IDE workflows: Rider and Visual Studio Community.

Getting Started:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity-package/Documentation%7E/GettingStarted.md

I'm happy to answer technical questions here.
~~~~

## After publication — announcement

~~~~text
**Spire is now on the Unity Asset Store.** I built it to catch forgotten switch cases at compile time.

It checks discriminated unions and annotated enums, classes, and interfaces, then provides IDE quick fixes to generate missing branches.

Its unions give each variant its own typed data: Move(destination, speed), Attack(target, damage), or Wait(duration). The Unity package includes generated Inspector drawers, JSON converters, offline guides, and optional samples.

I'm Houtamelo, the developer. Regular price: US$20.
https://assetstore.unity.com/packages/package/6893155

Minimum Unity: 2022.3.62f3. Verified IDE workflows: Rider and Visual Studio Community.

Getting Started:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity-package/Documentation%7E/GettingStarted.md

I'm happy to answer technical questions here.
~~~~

## Moderator inquiry — when the server has no clear promotion channel

~~~~text
Hi! I develop Spire, a Unity C# tool for exhaustive matching, IDE quick fixes, and discriminated unions. It has been submitted for Asset Store vetting, with a regular price of US$20.

May I share a short demonstration and documentation link in this server? Which channel should I use? I'll identify myself as the developer and stay available for technical questions.
~~~~

## Reply — how do I enable the checks?

~~~~text
Use [EnforceExhaustiveness] on an existing enum, abstract class, or interface. Generated discriminated unions have their own checks. The analyzers run during compilation; a supported IDE can also show the diagnostics and offer quick fixes. In Rider, enable Settings → Editor → Inspection Settings → Roslyn Analyzers.
~~~~

## Reply — do I need to stop using classes?

~~~~text
You can keep your class/interface hierarchy and add exhaustiveness checking to it. Unions are a separate option for commands, states, or results whose variants carry different data. Several systems can interpret the same union with their own exhaustive switches.
~~~~

## Reply — Unity compatibility

~~~~text
The package minimum is Unity 2022.3.62f3. Validated Editors: 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1. Unity 2022 can serialize non-generic unions; generic unions remain usable for runtime code but cannot use its native serializer.

There is also a documented Linux serialization crash in Unity 6000.6.5f1 involving generic unions with reference-type arguments. Exact platform coverage and limitations:
https://github.com/Houtamelo/spire_csharp_analyzers/blob/unity-6000/docs/unity.md
~~~~

## Reply — Burst and Inspector authoring

~~~~text
For Burst, use a runtime union without [Serializable] and with Burst-supported unmanaged payloads. Generated Unity serialization adds a managed cache reference. When you need Inspector authoring and Burst runtime data, declare separate unions and convert or bake the authoring data into the runtime form.
~~~~

## Reply — source and price

~~~~text
The source is public and MIT-licensed. The US$20 Asset Store product is the packaged Unity distribution, including compiler extensions, IDE quick fixes, offline guides, and samples:
https://github.com/Houtamelo/spire_csharp_analyzers/tree/unity-6000
~~~~

## Reply — issue report

~~~~text
Please open an issue with your Unity version, OS, IDE, and a minimal reproduction. If it happens only in a build, include the target platform, Mono/IL2CPP backend, and managed stripping level:
https://github.com/Houtamelo/spire_csharp_analyzers/issues
~~~~
