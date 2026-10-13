# Unity Showcase (URP)

This sample demonstrates struct layouts, Inspector authoring, variant rename
aliases, managed-reference nulls/sharing, property patterns, checked enum
conversion, filtering, and struct inliners. It includes a saved scene, preset,
and real Unity Test Framework tests.

Before importing, install URP 17.5.0 and Unity Test Framework 1.7.0. Import the
sample once from Package Manager. Keep its asset `.meta` files and enable Rider's
Roslyn Analyzers. Its runtime asmdef already enables unsafe code for UnsafeOverlap.

In a disposable URP project:

1. Open `Scenes/Showcase.unity` inside the imported sample folder.
2. Select `Examples/ShowcasePreset.asset` and edit the generated union drawers.
3. Invoke **Tools → Spire → Configure Showcase URP** if the pipeline is not set.
   This explicitly changes Graphics/quality settings, Linear color space, and
   the sample camera. Import alone does not change project settings.
4. Enter Play Mode and execute the selected command or command queue.

**Tools → Spire → Create or Reset Showcase Examples** replaces the sample preset
and scene data. Store custom presets/scenes elsewhere. Both tools find the
sample through its authored assembly GUID and work at the imported folder path.

The ApplyEffect queue entries share a DamageEffect in one host, and another has
a null effect. The union picker selects variants; C# `ResetExamples` and the
HealingEffect button construct field subtypes. `Speak` accepts stored payloads
named `Say` through one former-name alias. `CompilerExamples/Diagnostics.cs.txt`
contains deliberately invalid snippets for trying Rider fixes without breaking
the sample compilation.

Open **Window → General → Test Runner**, select **EditMode**, and run
`Houtamelo.Spire.Showcase.Tests`. Tests cover factories/matching, persistence,
null/reference sharing, rename aliases, unchanged saves, relocated asset paths,
and URP Play Mode rendering. Temporary test assets are removed after each test.

Validated on Unity 6000.5.6f1, Linux x64. See the installed package's offline
Documentation~ guides for support, serialization, troubleshooting, and upgrade
limits. This sample does not require a source checkout or .NET SDK.
