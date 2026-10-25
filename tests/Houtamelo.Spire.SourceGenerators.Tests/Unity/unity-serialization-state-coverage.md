# Unity serialization state coverage

| Test | Contract |
|---|---|
| `SerializableUnionAddsOnlySerializedPayloadToTheRuntimeInstanceState` | Unity serialization adds exactly `_serializedPayload` compared with the equivalent non-Unity union; no hydration bool or generated auto-property field exists. |
| `SharedMarkerIsGeneratedOnceAndHydrationReusesItAcrossCopiesAndGenericUnions` | One nongeneric marker derives from `UnitySerializationPayloadBase`; preparation creates typed wrappers without eager factory allocation, explicit hydration preserves prepared runtime values while reusing that marker across unions, closed generics, nested unions, and struct copies, and a marker supplied to direct reconstruction falls back to the first variant defaults. |
| `GeneratedUnityCallbacksExecuteInPlayerAndEditorCompilations` | The emitted callbacks execute under both player and `UNITY_EDITOR` compilations; valid payload values survive `OnAfterDeserialize`, while the editor retains its typed wrapper and the player releases it. |
