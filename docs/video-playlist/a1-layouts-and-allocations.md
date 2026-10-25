# A1. Choosing layouts through a real workload

An optional technical follow-up: measure construction, matching, and copying in
Unity before choosing a union representation. Let the measurements determine
which comparisons deserve screen time.

## Example and feature grouping

Boss Room's
[ActionRequestData](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/1299ba4fc97d83b634e7d32a86c438ba668d7f9c/Assets/Scripts/Gameplay/Action/Input/ActionRequestData.cs)
holds position, direction, targets, an amount, and action-control flags. It is a
candidate source for an internal typed command workload, but also implements
Netcode serialization. Preserve the wire representation and adapt at its boundary;
Spire does not generate that networking integration.

Group Additive, Overlap, UnsafeOverlap, BoxedFields, BoxedTuple, property patterns,
and positional-pattern boxing. Explain generic support and runtime requirements
only when they affect the measured example.

## Demonstration sequence

1. Show the real action path and choose a workload that matters to gameplay.
2. Record its existing allocation and timing profile before refactoring.
3. Compare equivalent commands under candidate layouts, with the same payloads,
   operation count, and observable results.
4. Isolate construction, matching, and copying. Compare property matching with
   positional matching rather than attributing all costs to storage.
5. Inspect generated storage to explain the measurements and select a layout.
6. Replay the actual action path with the selected representation.

## Preparation and recording evidence

Warm up the workload and separate command costs from networking, UI, collection
growth, and serialization callbacks. Keep Editor Mono and player results separate;
measure IL2CPP independently if it appears in the video.

Ordinary Overlap boxes composite value types such as Vector3 in its current
storage classification. It has no Unsafe dependency and excludes generic unions.
UnsafeOverlap keeps those values in typed slots but requires the Unsafe API and
unsafe-code configuration. Both assign storage by resolved field name; neither
is guaranteed to use only the largest variant's footprint.

Do not present desktop benchmarks, Additive, or UnsafeOverlap as a universal Unity
performance result. See [layouts](../discriminated-unions.md#layout-strategies)
and [Unity requirements](../unity.md). This episode needs a separately validated
demo adaptation before filming.
