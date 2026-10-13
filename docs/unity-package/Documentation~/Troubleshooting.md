# Troubleshooting

| Symptom | Check |
|---|---|
| Partial factories have no implementation | Start with Unity's first compiler/generator error. Confirm `partial` on the union and all containing types, all four DLLs, and `RoslynAnalyzer` labels in their metadata. |
| Unity compiles but Rider lacks generated members or fixes | Enable Rider's Roslyn Analyzers, refresh the Unity project model, and check the IDE's analyzer errors. |
| Analyzer works in one assembly only | Keep analyzer `.meta` files with the DLLs; inspect the owning asmdef and compiler response file for analyzer loading. |
| UnsafeOverlap does not compile | Enable unsafe code on the union's asmdef and ensure its compiler references provide `System.Runtime.CompilerServices.Unsafe`, or choose another layout. |
| Overlap rejects a generic union | Use Additive, BoxedFields, BoxedTuple, or UnsafeOverlap. Generic containing types also exclude Overlap. |
| A field is not persisted | Check Unity's serialization eligibility for that field type and whether it needs `[UnitySerializeReference]`. |
| Managed-reference field lacks a subtype picker | Spire's picker selects union variants. Instantiate field subtypes in C# or use a separate subtype drawer. |
| Showcase does not compile after import | Install URP 17.5.0 and Unity Test Framework 1.7.0 before importing the sample. |
| Showcase's URP test fails | Invoke Tools → Spire → Configure Showcase URP in a disposable sample project. That action changes render/quality/color-space settings. |
| Missing-payload warning after a schema edit | Restore the asset and use an explicit data migration; the one-name variant alias does not migrate arbitrary schema changes. |

The verified environment is Unity **6000.5.6f1**, Linux x64, Editor Mono and
IL2CPP players with High managed stripping. Other Editor versions and platforms
need separate validation. Unity 6000.6.5f1 has a reported engine crash in generic
struct serialization callbacks, reproduced without Spire. Native serialization
on that version is not supported.
The compiler extensions now target Roslyn 4.3 and load in Unity 6000.3.26f1 LTS.
Complete support for that Editor remains under evaluation. `UnsafeOverlap`
requires an additional `System.Runtime.CompilerServices.Unsafe.dll` there;
the package does not currently supply it. A native Inspector probe passed with
that dependency supplied separately. The showcase's URP prerequisites remain
those of the verified 6.5 environment. Ordinary `Overlap` initializes its readonly
storage in constructors and does not need the additional DLL.

For a reproducible Spire issue, record the Unity and Rider versions, platform,
backend, layout, containing asmdef, the smallest union declaration, and the first
compiler/runtime error. Include whether the failure occurs on a fresh project.
Use the project's issue tracker: https://github.com/Houtamelo/spire_csharp_analyzers/issues.

The consumer package does not ship Microsoft.CodeAnalysis/Workspaces DLLs or
upgrade Unity's compiler. Avoid copying those build dependencies into your
project. See [Getting started](GettingStarted.md) and [Serialization](Serialization.md).
