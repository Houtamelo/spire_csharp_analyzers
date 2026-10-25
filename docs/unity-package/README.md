# Houtamelo.Spire for Unity

Discriminated unions, generated Inspector drawers, compiler diagnostics, and Rider
code fixes. This bundle contains the runtime DLL, three compiler extensions,
offline guides, API tooltips, and an optional URP showcase.

Validated on Unity 6000.5.6f1, Linux x64, with Editor Mono and IL2CPP players.
Rider is the supported IDE. Unity 6000.6.5f1 has a reported engine crash in
generic struct serialization callbacks, reproduced without Spire; native serialization
on that version is not supported. Other Editor versions and platforms need
separate validation.
The compiler extensions use a Roslyn 4.3 baseline and load in Unity 6000.3.26f1
LTS. Complete 6.3 support is still being evaluated; the showcase dependencies
remain those of the verified 6.5 environment. The current payload representation
does not require unsafe code or an additional runtime Unsafe DLL.

1. Add this folder using **Window → Package Manager → + → Install package from disk**
   and select `package.json`, or copy it to `Packages/com.houtamelo.spire`.
2. Follow [Getting started](Documentation~/GettingStarted.md) to enable Rider's
   Roslyn analyzers and compile your first union.
3. Read [Usage](Documentation~/Usage.md), [Serialization](Documentation~/Serialization.md),
   [Troubleshooting](Documentation~/Troubleshooting.md), and
   [Upgrading](Documentation~/Upgrading.md) as needed.

The optional **Unity Showcase (URP)** sample requires URP 17.5.0 and Unity Test
Framework 1.7.0. Install those dependencies before importing it from Package
Manager's Samples tab. Its [instructions](Samples~/Showcase/README.md) cover scene
setup and tests. Spire itself does not require URP.

The [MIT license](LICENSE) applies to Spire. Included third-party material is
listed in [Third Party Notices](Third%20Party%20Notices.md). Compiler-host DLLs
are supplied by Unity/Rider; this package does not install a compiler.
