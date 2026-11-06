# Troubleshooting

| Symptom | Check |
|---|---|
| Unity 2020 cannot load Spire or generate factories | Unity 2020 will not work. Its compiler lacks the source-generator support and C# 9 features required by Spire. |
| Unity 2022 loses serialized union payloads | Generic unions are unsupported by this Editor's serializer. Non-generic unions use concrete wrappers. For assets written with the previous generic wrapper, update and resave in a newer supported Editor before moving them to Unity 2022. |
| Partial factories have no implementation | Start with Unity's first compiler/generator error. Confirm `partial` on the union and all containing types, all four DLLs, and `RoslynAnalyzer` labels in their metadata. |
| Unity compiles but Rider lacks generated members or fixes | Enable Rider's Roslyn Analyzers, refresh the Unity project model, and check the IDE's analyzer errors. |
| Analyzer works in one assembly only | Keep analyzer `.meta` files with the DLLs; inspect the owning asmdef and compiler response file for analyzer loading. |
| SPIRE_DU018 for a nested union | Move the union outside its generic containing type. The union itself may be generic. |
| A field is not persisted | Check Unity's serialization eligibility for that field type and whether it needs `[UnitySerializeReference]`. |
| Managed-reference field lacks a subtype picker | Spire's picker selects union variants. Instantiate field subtypes in C# or use a separate subtype drawer. |
| Showcase does not compile after import | Install URP 17.5.0 and Unity Test Framework 1.7.0 before importing the sample. |
| Showcase's URP test fails | Invoke Tools → Spire → Configure Showcase URP in a disposable sample project. That action changes render/quality/color-space settings. |
| Missing-payload warning after a schema edit | Restore the asset and use an explicit data migration; the one-name variant alias does not migrate arbitrary schema changes. |

The verified environment is Unity **6000.5.6f1**, Linux x64, Editor Mono and
IL2CPP players with High managed stripping. Other Editor versions and platforms
need separate validation. On Unity 6000.6.5f1 (Linux), generic unions instantiated with
reference-type arguments, such as `Command<string>`, still trigger an engine
serialization crash. Native Editor write/read checks pass for non-generic unions
and tested unmanaged generic unions. Use 6000.5.6f1 for serialized reference-type
generic instantiations; see [Serialization](Serialization.md).
The compiler extensions now target Roslyn 4.3 and load in Unity 6000.3.26f1 LTS.
Complete support for that Editor remains under evaluation. Historical probes
used the removed `UnsafeOverlap` mode; the current payload representation does
not require an Unsafe DLL. The showcase's URP prerequisites remain those of the
verified 6.5 environment.

For a reproducible Spire issue, record the Unity and Rider versions, platform,
backend, reference-storage mode, containing asmdef, the smallest union declaration, and the first
compiler/runtime error. Include whether the failure occurs on a fresh project.
Use the project's issue tracker: https://github.com/Houtamelo/spire_csharp_analyzers/issues.

The consumer package does not ship Microsoft.CodeAnalysis/Workspaces DLLs or
upgrade Unity's compiler. Avoid copying those build dependencies into your
project. See [Getting started](GettingStarted.md) and [Serialization](Serialization.md).
