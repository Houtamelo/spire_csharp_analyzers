# Unity metadata regression coverage

These real generator-driver cases use the installed Unity 6000.5.6f1 reference
assemblies. They cover structural contracts only; native persistence and Editor
interaction remain in the Unity integration fixtures.

| Case | Contract |
|---|---|
| `SerializableUnion_CSharp9_HasNoGeneratedNullableWarnings` | A C# 9 serializable union emits callbacks, payloads, and the shared wrapper without generated nullable warnings. |
| `NonSerializableUnion_HasNoUnitySerializationOrDrawerSurface` | A normal union has no Unity callbacks, serialized wrapper, Unity fields, or Editor drawer surface. |
| `SerializableAndFactoriesOnOtherPartialDeclarations_AreDiscoveredSemantically` | Qualified and aliased `SerializableAttribute` declarations opt in even when factories are declared on another partial declaration. |
| `SerializablePayloadFields_EmitExpectedUnityMetadataMatrix` | Enum and escaped keyword fields, unmarked arrays/lists, and marked reference fields receive the correct `SerializeField`/`SerializeReference` metadata. |

