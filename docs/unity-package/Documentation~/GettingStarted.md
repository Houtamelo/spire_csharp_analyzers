# Getting started

The minimum Unity version is **2022.3.62f3**. On Unity 2022, use non-generic
unions for native Unity serialization; generic unions remain usable for runtime
code. The optional Quick Start sample works on this baseline without URP.

Install Spire using Package Manager's **Install package from disk** action and
select the bundle's `package.json`. Keep all five DLLs and their `.meta` files
together: the core and Functional runtimes belong to player builds, while the three compiler DLLs
are labeled `RoslynAnalyzer` and excluded from runtime plugin imports.

The included [Functional library](Functional.md) provides `Result`, `Result<T>`,
and nullable option operations under `Houtamelo.Spire.Functional`.

In Rider, enable **Settings → Editor → Inspection Settings → Roslyn Analyzers**.
Allow Unity to compile and Rider to refresh its project model. Rebuilding the
package is not part of normal consumer installation; the .NET SDK is needed only
for repository development tools.

Add this to your Unity assembly:

```cs
using System;
using Houtamelo.Spire;

[Serializable]
[DiscriminatedUnion]
public partial struct Result
{
    [Variant] public static partial Result Success(int value);
    [Variant] public static partial Result Failure(string message);
}
```

`Result.Success(42)` constructs a union. A serialized field such as
`[UnityEngine.SerializeField] private Result result;` gets a variant picker and
fields for the selected variant. The union and every containing type must be
`partial`. Mutable serializable structs opt into Unity serialization with
`[Serializable]`; reference storage defaults to Auto.

Use [Usage](Usage.md) for matching and storage choices,
[Serialization](Serialization.md) for persistence, or
[Troubleshooting](Troubleshooting.md) if generation does not appear.

To try the supplied scene, use Unity **6000.5.6f1** and install URP **17.5.0** and Unity Test Framework **1.7.0**
in your project, then import **Unity Showcase (URP)** from this package's Samples
tab. The sample is optional and does not change your render pipeline until you invoke its setup
menu. Follow its README before doing that.
