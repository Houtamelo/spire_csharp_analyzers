# Unity 2022 rejects generated serialization payload wrappers

The original failure below is confirmed on Linux Editor `2022.3.62f3`.
The [concrete-wrapper follow-up](../reviews/2026-11-04-concrete-unity-wrappers.md)
resolves it for non-generic unions using per-variant non-generic subclasses of
the shared wrapper. Generic unions remain unsupported by Unity 2022's serializer;
newer Editors keep existing generic support. Do not lower the package minimum
from `6000.5.6f1` on this evidence alone.
Non-serialized unions and compiler/analyzer checks have separate results in the
[LTS follow-up](../reviews/2026-11-04-unity-lts-followup.md).

The generated cache uses a concrete instantiation of
`Houtamelo.Spire.Generated.SpireSerializedPayload<T>`, stored through a
`[SerializeReference]` field. Unity 2022 rejects that managed-reference object,
including when `T` is a payload of a non-generic union. This is not limited to
user-declared generic unions.

Native evidence from the corrected package:

- The native Write phase fails at check 41 because
  `shape._serializedPayload.value` is absent from `SerializedObject`.
- The general integration fixture likewise cannot find
  `single._serializedPayload.value._text`.
- Unity logs that `[SerializeReference]` cannot serialize the supplied generic
  payload type and suggests a non-generic subclass.
- The saved `SerializedUnionFixture.asset` contains `_serializedPayload` entries
  with `rid: -2`; the expected Move/Empty/Rich payload records are absent. Only
  the separately authored, non-generic graph-node references remain.
- A fresh-process diagnostic Read fails at check 4 with
  `Fielded payload values did not restore`. Its prerequisite baseline file was
  copied from the exact failed-Write asset, without changing payload assertions.

The same-process integration round-trip checks can pass despite this persisted
data being absent. Their passing results do not establish disk persistence;
the strict serialized-surface and fresh-process gates must remain in place.

Artifacts:

```text
tmp/unity-legacy-fixes-20261104/final/2022.3.62f3/native/write.log
tmp/unity-legacy-fixes-20261104/final/2022.3.62f3/native/unity-union-serialization-write-results.json
tmp/unity-legacy-fixes-20261104/final/2022.3.62f3/native/Assets/Resources/SerializedUnionPayloadCore/SerializedUnionFixture.asset
tmp/unity-legacy-fixes-20261104/final/2022.3.62f3/integration/unity-integration-results.json
```

The approved repair emits concrete cache classes on every Unity version, keeps
the shared generic base for old assets, upgrades legacy caches during save, and
resolves Inspector payload types through the inherited `value` field. Old assets
must be resaved in a newer supported Editor before moving them to Unity 2022.
This is separate from the `FocusController.IgnoreEvent` compatibility repair.
The original failures and artifacts above remain historical evidence; the new
native fixture verifies non-generic persistence without dropping generic cases
from the broader modern-Editor fixture.
