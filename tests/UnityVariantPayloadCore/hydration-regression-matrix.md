# Unity serialization hydration regression matrix

The fixture covers the state redesign in both the Editor callback path and the
standalone player path. The player gate runs the same assertions on Mono and
IL2CPP.

| Area | Coverage | Fixture assertion |
|---|---|---|
| Instance state | Fieldless, reference, and closed generic serializable unions are compared with plain unions having the same user fields. | `_serializedPayload` is the only extra instance field; no per-instance hydration flag exists. |
| Shared marker | Ordinary, generic, nested, and copied hydrated values are inspected through the generated payload field. | Every hydrated value reuses one marker reference, including value-type copies. |
| Player preparation | A hydrated marker value is prepared for serialization. | Preparation replaces the marker with an active typed payload wrapper. |
| Repeated hydration | A rich payload contains a nested union, an array, a list, and a managed-reference cycle. | Explicit repeated hydration preserves values, nested markers, collections, and reference identity. |
| Invalid new payload | A previously hydrated union receives a payload wrapper for a different union type before `OnAfterDeserialize`. | Deserialization still falls back to the first variant and installs the shared marker. |
| Editor cache | A real prepared payload is passed through `OnAfterDeserialize`, repeated hydration, and equivalent preparation. | The real wrapper reference is retained for stable YAML and managed-reference IDs. |
| Recovery shape | Existing native recovery assets continue to cover null, missing, unknown, stale-kind, and invalid-kind payloads. | Invalid payloads recover without reusing a stale marker as an active wrapper. |
