# Houtamelo.Spire Asset Store listing

Draft v1 for review. The publisher notes at the end track remaining publication
decisions.

## Title

Houtamelo.Spire - Exhaustive Matching & Discriminated Unions

## Summary

Save time by catching errors at compile time. Exhaustive matching, IDE quick fixes, and lightweight discriminated unions with Inspector drawers, generated JSON converters, and Burst/DOTS support.

## Description

**Save time by catching errors at compile time.**

Houtamelo.Spire brings exhaustive matching and discriminated unions to Unity.
Catch forgotten cases as your game evolves, generate missing branches in your
IDE, and give each variant the data it needs.

### Catch missing cases before you press Play

Use exhaustive matching with enums, classes, interfaces, structs, and
discriminated unions. Spire checks switch statements/expressions,
reports a compile-time error when cases are missing, and lists exactly which
cases you forgot. No `default:` or catch-all case is required.

Added a new class implementing an interface? `Spire` immediately reports the
switches that need updating. The same applies when you add an enum
member or a union variant.

Follow the diagnostics straight to the affected code. Use the IDE quick fix to generate the unhandled cases, then fill in their behavior.

### Give each variant its own data

Have you ever looked at an enum and wished you could attach different data to each variant?

*C# Utopia*:
```cs
enum Command {
    Move(Vector2 destination),
    Patrol(List<Vector2> waypoints, int current),
    Attack(Actor target),
}

Command command = Command.Move(new Vector2(6.9f, 4.20f));

switch (command) {
    case Command.Move(Vector2 destination): {
        actor.position = destination;
        break;
    }
    case Command.Patrol(List<Vector2> waypoints, int current):
    ...
}
```

With Spire:
```cs
[DiscriminatedUnion] // Spire's source generator takes care of all the plumbing
public partial struct Command {
    [Variant] public static partial Command Move(Vector2 destination);
    [Variant] public static partial Command Attack(Actor target);
    [Variant] public static partial Command Patrol(List<Vector2> waypoints, int current);
}

Command command = Command.Move(new Vector2(6.9f, 4.20f));

switch (command) {
    case { IsMove: true, move: var (destination) }: {
        actor.position = destination;
        break;
    }
    case { IsPatrol: true, patrol: var (waypoints, current) }:
    ...
}
```

Spire generates the factories and typed payloads at compile time. Match the variant and
deconstruct its data where you consume it. Different systems can react to the
same command, state, or result in their own way, without putting every operation
on the data type itself.

Spire's struct-based unions are lightweight, with compact, allocation-free type-safe
storage (No unsafe code!).

### Author unions in the Unity Inspector

Add `[Serializable]` to your union and use it in serialized fields. Spire
generates the variant picker and editable fields for the selected variant,
including inside lists.

Compatible with Odin Inspector. Odin is optional.

### Generate JSON serialization and deserialization

Generate converters for System.Text.Json, Newtonsoft.Json, or both. Declare
your variants and their data once; Spire generates the JSON serialization and
deserialization code for you.

### Use typed polymorphic data in Burst and DOTS

Bring struct-based discriminated unions into Burst jobs and DOTS. Store your polymorphic data in `NativeArray<T>`, switch on their variants, and deconstruct their payloads
inside your job.

### Included

- Runtime library, analyzers, source generator, and IDE quick fixes.
- Generated Unity serialization and Inspector integration.
- Offline documentation and runtime API tooltips.
- Optional Unity Showcase sample with a runnable scene, Inspector-editable
  presets, and example tests.

### Installation

Standard installation via Unity's Package Manager.

For Rider support, make sure that Roslyn Analyzers are enabled: **Editor > Inspection
Settings > Roslyn Analyzers**.

## Technical details

- **Validated Unity versions:** 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1.
- **Unity 2020:** will not work.
- **Unity 2021:** cannot load the current compiler extensions.
- **IDE support:** Spire uses Roslyn's standard analysis/code-fix providers, any IDE that supports that should also support Spire. Verified in JetBrains Rider and Visual Studio Community.
- **Tested desktop platforms and backends:** Linux x64 and Windows 10 x64 on all
  three versions, including Editor Mono, native serialization, rendered Inspector
  interaction, and standalone Mono/IL2CPP players.
- **Tested Web builds:** WebGL2/IL2CPP on all three versions in Chrome and Firefox
  on desktop Linux, with High managed stripping and `FullWithStacktrace` exception
  support. Unity's default Web exception setting does not preserve all tested
  exception behavior. Other browsers and platforms require separate validation.
- **Unity 2022 serialization:** non-generic unions pass native serialization;
  generic unions cannot use native Unity serialization on this version.
- **Package minimum:** the manifest still declares 6000.5.6f1. Older-version
  verification does not lower the package minimum or Showcase requirements.
- **JSON:** install the JSON library you select, System.Text.Json or
  Newtonsoft.Json, in your project.
- **Burst/DOTS:** Spire does not have code that specifically targets Burst/DOTS, our discriminated unions are naturally Burst/DOTS compatible since they are struct-based. Note that the fields of the variants must also comply with Burst's restrictions (Spire isn't magic!). Consult the included Usage and Serialization guides for payload and serialization choices.
- **Unity 6000.6.5f1 limitation (Linux):** an engine serialization bug affects generic
  unions with reference-type arguments, such as `Command<string>`. This crash is
  fixed in Unity 7000 (verified on 7000.0.0a12). We expect a backport to 6000.6.x
  soon, although its availability and timing remain unconfirmed.
- **Third-party notice:** includes compiler compatibility definitions generated
  by PolySharp under the MIT license. See Third Party Notices in the package for
  details.

## Documentation and support

Offline guides are included in the package: Getting Started, Usage,
Serialization, Troubleshooting, and Upgrading.

Support and bug reports: [GitHub issue tracker](https://github.com/Houtamelo/spire_csharp_analyzers/issues).

## Keywords

C#, exhaustive matching, discriminated unions, tagged unions, enum, source
generator, Roslyn analyzer, IDE quick fixes, Unity Inspector, JSON, Burst, DOTS

## Publisher notes

- Unity 2020 compatibility is ruled out; document it publicly as "will not work."
  Unity 2021 cannot load the current Roslyn API baseline. Unity 2022 passes
  native serialization write/read checks for non-generic unions with the new
  concrete wrappers; generic unions remain unsupported by its serializer.
  All three listed versions pass the full Editor harness, applicable native
  serialization, rendered Inspector interaction, and actual Mono/IL2CPP players
  on Linux and Windows. Retain the currently declared minimum until release
  metadata is finalized. See the
  [current validation](reviews/2026-11-05-unity-2022-2023-current-validation.md)
  and [rendered follow-up](reviews/2026-11-05-rendered-inspector-validation.md)
  for exact versions, DLLs, and test scope.
- Windows 10 validation is complete on all three listed versions: 2,692 complete
  Editor cases per version, applicable native/player/Inspector gates, and Visual
  Studio Community live diagnostics, generated-symbol navigation, a representative
  Quick Action, and Unity recompilation. See the
  [Windows matrix](reviews/2026-11-06-windows-unity-version-matrix.md).
- Release WebGL/IL2CPP validation passes in Chrome and Firefox on Linux for all
  three listed versions: 100 original behavioral tests per browser, plus 59
  native core checks on Unity 2022 and 60 on Unity 2023/6000. Each browser/version
  also propagates an intentional assertion failure. These runs use High stripping
  and full exception support; the initial default-exception build passes 99/100
  behavioral tests. Software rendering establishes correctness, not GPU
  performance. Safari, mobile browsers, and WebGPU are unverified. See the
  [Web results and exception-setting limitation](unity.md#web-runtime).
- Unity serialization follow-up: the engine repro and preserved Spire probe pass
  on `7000.0.0a12` with CoreCLR, including a fresh-process asset read. The same
  inputs still crash on 6000.6.5f1 with Mono. Use a shared root cause with issue
  `25665` as the working assumption. The storefront states that the crash is
  fixed in Unity 7000, verified on `7000.0.0a12`. A `6000.6.x` backport is expected
  soon; its availability and timing remain unconfirmed.
  See the [Unity 7 comparison](reviews/2026-11-03-unity-7-serialization-recheck.md).
- Unity 7 Linux IL2CPP verification: all 100 original behavioral tests and 60
  native runtime/serialization checks pass with High stripping. The normal NUnit
  run passes all 107 cases without filtering after removal of an unrelated
  subprocess capability probe.
  See the [validation report](reviews/2026-11-03-unity-7-il2cpp-validation.md).
- Confirm the final minimum Unity version and supported platforms before
  submission. This draft follows the current package documentation's validation
  matrix; the release baseline remains a separate decision.
- Confirm the Asset Store delivery format. The installation paragraph describes
  the verified UPM bundle workflow and should match the final customer download.
- Publish the Unity-specific guides at a stable public URL before adding an
  online documentation link. The proposed `unity-6000` Getting Started and Usage
  URLs returned 404 during this review; the public issue tracker is accessible.
- Keep storage modes, boxing, serialization cache behavior, and detailed Burst
  restrictions in the user guides, following the approved marketing plan.
- The short third-party notice and explicit requirements follow Unity's
  [submission guidelines, sections 1.1.c, 1.2.a, and 3.1.a](https://marketplace.unity.com/publishing/submission-guidelines).

Source references for review: [marketing decisions](marketing_plan.md),
[package overview](unity-package/README.md),
[setup](unity-package/Documentation~/GettingStarted.md),
[usage](unity-package/Documentation~/Usage.md),
[compatibility](unity.md), and
[third-party notices](unity-package/Third%20Party%20Notices.md).
