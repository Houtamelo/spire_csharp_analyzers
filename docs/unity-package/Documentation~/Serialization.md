# Unity serialization

Unity 2022 does not support native serialization of generic unions. Non-generic
unions use non-generic concrete wrappers. Newer Editors retain existing generic
union support, subject to the engine limitation below. The generated wrapper code is the same across
Unity versions; there is no Unity 2022-specific representation.
Nesting a struct union inside a generic containing type remains rejected by
`SPIRE_DU018` on every Unity version.

On Unity 6000.6.5f1 (Linux), generic unions instantiated with reference-type
arguments, such as `Command<string>`, still trigger an engine serialization
callback crash. Use 6000.5.6f1 for those serialized generic unions. Non-generic
unions can contain reference-type payload fields: the current native Editor
write/read checks pass for them and for tested unmanaged generic unions.
This recheck does not establish complete 6.6 Player or rendered Inspector support.

Known limitation for Burst and ECS: serialization adds a managed-reference cache,
so a serializable union cannot also be unmanaged runtime data. Manually declare a
separate union without `[Serializable]` when you need a Burst-compatible version,
and convert or bake from the authoring union. Use only Burst-supported fields in
the runtime type; removing `[SerializeField]` from a consumer does not remove the
cache from a serializable union type.

Apply `[Serializable]` to a mutable struct union. All reference-storage modes
support the same generated serialization payload and Inspector workflow. Unity
decides which variant parameter types it can serialize. Readonly structs and
record/class unions do not get this generated Unity serialization support.

The union stores `_kind` and one active managed-reference payload. Each variant
has a concrete wrapper under `CommandTypes.SerializedPayloads`, derived from
the shared `SpireSerializedPayload<T>` base. Its inherited public `value` field
contains a generated payload struct from
`CommandTypes.Variants`. Its private `_`-prefixed fields carry `[SerializeField]`
or, when requested, `[SerializeReference]`; public getters retain the factory parameter names.
A recognized payload identifies the variant even when `_kind` disagrees.
Generated callbacks explicitly implement `ISerializationCallbackReceiver` to
restore runtime fields after deserialization and reuse unchanged snapshots
before serialization. The Editor keeps a snapshot for Inspector stability;
players release it after deserialization. The existing payload reference also
tracks reconstructed state; serialization adds no hydration flag or other
instance field. Runtime serialization can allocate
another snapshot when necessary.

Assets written with the previous shared generic wrapper remain readable where
Unity supports that type. Saving upgrades them to the current concrete wrapper
without changing the payload fields. To move old assets to Unity 2022, load and
resave them in a newer supported Editor first; Unity 2022 cannot load the old
generic managed-reference representation itself.

Ordinary parameter fields use Unity's inline serialization. To preserve null,
derived types, cycles, or shared managed references, mark a variant parameter:

```cs
[Variant]
public static partial Command ApplyEffect([UnitySerializeReference] Effect? effect);
```

`Effect` and concrete implementations must satisfy Unity's managed-reference
serialization rules. Reference identity is shared within one host object;
Unity does not preserve the same managed instance across separate host objects.
For arrays/lists, Unity applies managed-reference behavior to their elements.
The generated union picker chooses union variants; it does not construct arbitrary
`Effect` subclasses. Construct those instances in C# or supply your own subtype
authoring tool.

Invalid or missing union payloads produce a warning:
`invalid union serialization data, defaulting to FirstVariant`, with your
union's first variant name. Ensure that variant's default values are meaningful
for your application.

Variant reordering does not change the serialized variant identity. To rename a
variant, retain its previous name:

```cs
[Variant(FormerlySerializedAs = "Say")]
public static partial Command Speak(string speech);
```

The generator emits current-name and former-name payload structs under `Variants`,
with corresponding concrete wrappers under `SerializedPayloads`. Both restore
the current Speak variant, and future serialization uses its current payload.
Only one former name is supported per variant. This alias does not cover field,
union type, namespace, or assembly renames, nor payload schema/type changes.
Changing `[UnitySerializeReference]` on an existing field also requires a
separate data migration. Read [Upgrading](Upgrading.md) before schema changes.
