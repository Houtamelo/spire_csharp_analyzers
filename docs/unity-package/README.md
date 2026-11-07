# Houtamelo.Spire for Unity

Discriminated unions, generated Inspector drawers, compiler diagnostics, and IDE
code fixes. This bundle contains the runtime DLL, three compiler extensions,
offline guides, API tooltips, and an optional URP showcase.

**Minimum Unity version: 2022.3.62f3.**
**Unity 2020 will not work.** Its compiler lacks features required by Spire.
Concrete wrappers pass native serialization write/read checks for non-generic
unions on Unity `2022.3.62f3`. Generic unions cannot use native serialization on
Unity 2022. See [Serialization](Documentation~/Serialization.md).

Validated on Unity 2022.3.62f3, 2023.2.22f1, and 6000.5.6f1 on Linux and Windows
x64, with Editor Mono, applicable native serialization, rendered Inspector
interaction, and standalone Mono/IL2CPP players. WebGL2/IL2CPP runtime checks also
pass on all three in Chrome and Firefox on desktop Linux, with High managed
stripping and `FullWithStacktrace` exception support. Unity's default Web exception
setting does not preserve all tested exception behavior; Safari, mobile browsers,
and WebGPU remain unverified. Rider and Visual Studio Community have
verified diagnostics, generated symbols, and a representative IDE Quick Action.
On Unity 6000.6.5f1 (Linux), an engine bug still affects
serialization of generic unions with reference-type arguments, such as
`Command<string>`. Native Editor write/read checks pass for non-generic unions
and tested unmanaged generic unions. See [Serialization](Documentation~/Serialization.md).
Other Editor versions, platforms, and 6.6 Player/Inspector workflows need separate validation.
The compiler extensions use a Roslyn 4.3 baseline and load in Unity 6000.3.26f1
LTS. Complete 6.3 support is still being evaluated; the showcase dependencies
remain those of the verified 6.5 environment. The current payload representation
does not require unsafe code or an additional runtime Unsafe DLL.

1. Add this folder using **Window → Package Manager → + → Install package from disk**
   and select `package.json`, or copy it to `Packages/individual.antonio-pedro-goncalves-ferreira.spire`.
2. Follow [Getting started](Documentation~/GettingStarted.md) to enable Rider's
   Roslyn analyzers and compile your first union.
3. Read [Usage](Documentation~/Usage.md), [Serialization](Documentation~/Serialization.md),
   [Troubleshooting](Documentation~/Troubleshooting.md), and
   [Upgrading](Documentation~/Upgrading.md) as needed.

The optional **Quick Start** sample demonstrates a union, its generated accessors,
and a component Inspector without additional dependencies or a specific render
pipeline. Import it from Package Manager's Samples tab.

The optional **Unity Showcase (URP)** sample requires Unity 6000.5.6f1, URP 17.5.0,
and Unity Test Framework 1.7.0. Install those dependencies before importing it from Package
Manager's Samples tab. Its [instructions](Samples~/Showcase/README.md) cover scene
setup and tests. Spire itself does not require URP.

See the [changelog](CHANGELOG.md) for the current release candidate.
The [MIT license](LICENSE.md) applies to Spire. Included third-party material is
listed in [Third Party Notices](Third%20Party%20Notices.md). Compiler-host DLLs
are supplied by Unity/Rider; this package does not install a compiler.
