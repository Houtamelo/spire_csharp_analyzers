# 7. Typed material variants inside a Burst rendering job

Use [BurstPathTracerSpire](../../examples/BurstPathTracerSpire/README.md) to show
closed polymorphism in a real CPU rendering workload. Aras Pranckevičius's
ToyPathTracer is an independent example, not an official Unity demo.

The original Material constructor takes an enum and parameters for every surface.
Diffuse surfaces ignore roughness and refraction index, metal ignores refraction
index, and glass ignores albedo. Generated factories expose the relevant data for
each choice while NativeArray stores the unmanaged union directly.

1. Show the actual rendered scene and its diffuse, metal, and glass surfaces.
2. Inspect the original material data, constructor calls, and scattering branches.
3. Compare Lambert, Metal, and Dielectric factories and their typed payloads.
4. Follow a material through NativeArray into the actual Burst rendering job.
5. Show exhaustive handling and how an added surface exposes missing consumers.
6. Run native execution markers and the original-versus-Spire pixel comparison.
7. Return to the rendered scene and explain the resulting authoring/code benefit.

Use switch statements in Burst code. A C# switch expression can synthesize a boxed
SwitchExpressionException fallback even when Spire proves the variants exhaustive.
The runtime union has no Serializable attribute; serialized authoring data requires
a separately declared type and an explicit conversion.

The [integration report](../reviews/2026-10-27-spire-demo-integration.md) records
actual test evidence. Present timing comparisons only after measuring the same
workload and settings; native compatibility and clearer constructors do not by
themselves establish a speedup.
