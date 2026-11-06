# A1. Understanding union storage through a real workload

An optional technical follow-up: measure construction, matching, and copying in
Unity, then explain the generated unmanaged overlay and reference-slot policies.
The old public Layout strategies have been removed; historical benchmark tables
do not describe the current representation.

Use [BurstPathTracerSpire](../../examples/BurstPathTracerSpire/README.md) for an
unmanaged workload and the cooking/connection examples for managed fields.
Material variants show which parameters belong together and how the maximum
active payload affects storage. The runtime union needs no unsafe-code setting
or Unsafe DLL.

1. Show the relevant gameplay/rendering workload and record its existing profile.
2. Inspect the generated per-variant unmanaged helpers and explicit offset-zero
   overlay. Helpers use the compiler's default sequential layout.
3. Measure construction, processing, and copying independently, keeping workload
   size and observable outputs equivalent.
4. Explain ReferenceStorage.Typed, Shared, and Auto using an actual managed-data
   case. Typed reuses compatible types; Shared uses object slots and can box
   managed structs. Auto compares slot counts after reuse.
5. Show `case { IsVariant: true, variant: var (fields) }:` matching. Avoid attributing costs from
   callbacks, logging, collections, or JSON to the union itself.
6. Choose a representation for the measured case and explain its tradeoff.

Warm up the workload. Keep Editor Mono, Burst, and player backend measurements
separate. Burst runtime data must contain only supported unmanaged fields and
cannot use the generated Unity serialization cache; authoring needs a separately
declared union when both forms are required.

Native execution markers and pixel comparison establish compatibility and
correctness, not a speedup. Record timing and allocation evidence before including
performance claims. See [current storage](../discriminated-unions.md#storage) and
[integration evidence](../reviews/2026-10-27-spire-demo-integration.md).
