Recorded request batch: 2026-10-25. Implementation authorized after discussion.

Approved clarifications:
- Group payloads and storage under a sibling `{UnionName}Types` static class.
- The container matches the union's accessibility; its `Variants` container and payload structs are public within it. `Fields` and storage remain internal.
- Containers stay nongeneric. Variant payload structs copy the union's type parameters and constraints; generic-dependent fields retain existing typed storage behavior.
- Preserve the existing arity suffix for nongeneric helper containers of generic unions, avoiding collisions between `Command` and `Command<T>`.
- Use `Fields` and `Variants` aliases where accessible and unambiguous; retain qualified references when aliases would collide or are inaccessible.
- Preserve serialization aliases, nested-union placement, diagnostics, custom source emission, and one blank line between multiline function members.
- Keep serialized backing fields directly adjacent to their getters.

The source request text follows. Its pending-discussion note is historical; the complete batch is now approved.

Implementation findings:
- Unity serializes nested type identities with `/`; reflection uses `+`. Preserve the canonical Unity spelling in fixture expectations and the authored Showcase asset.
- A variant named `Unmanaged` collides with the new implementation-only `Fields.Unmanaged` container. Use a unique internal helper name for this reserved-name case while preserving the public `Variants.Unmanaged` name and every ordinary helper name.
- `[SerializeReference]` independently satisfies Unity's private-field serialization check; `[SerializeField]` is redundant on those fields. Evidence: https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/2022.3/Tools/Unity.SerializationLogic/UnitySerializationLogic.cs (`WillUnitySerialize`). Native round-trip coverage validates the installed Unity version.

Validation completed:
- Full generator suite: 649 passed, 0 failed (`tmp/grouped-union-types-results/grouped-generator-final-2.trx`).
- Behavioral suite: 73 passed, 0 failed (`tmp/grouped-union-types-results/behavioral.trx`).
- Release package build: 0 warnings, 0 errors. Changed C# files formatted; scoped diff checks passed.
- Unity 6000.5.6f1 Linux x64: serialization Write 126 checks, fresh-process Read 87 checks, Recovery 59 checks; all passed.
- Native Inspector: 16 cases, 147 checks passed, including focus and undo behavior.
- Release Mono and IL2CPP players: 60 runtime checks each, both passed with High managed stripping. Build reports include deliberate recovery-fixture warnings and an existing fixture SPIRE014 warning; neither backend has build errors.
- The first native run exposed Unity's `/` nested-type spelling; corrected serialized identities and reran successfully. Reflection identities retain `+`.
- Rebuilt the Unity export consumed by `examples/UnityShowcase/Packages/manifest.json`. Source Release, exported package, and native fixture analyzer DLL SHA256: `4efe439a54f4425cf32594dedd92680ab3e550267e1e0c95f9f381bcadc3ed87`.
- Showcase asset changes are limited to nine generated payload type identities; all serialized values and 22 reference-ID entries are preserved.

The original requests below are retained as the implementation record; `docs/PENDING.md` is cleared after completion.

---

```cs
[@EditorBrowsable(EditorBrowsableState.Never)]
public readonly bool __SpireSerializedStateCurrent(object? payload) {
    if (payload is global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Houtamelo.Spire.Showcase.GameplayCommandIdle> serializedPayload_Idle) {
        if (this.kind != Kind.Idle) return false;
        return true;
    }
    if (payload is global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Houtamelo.Spire.Showcase.GameplayCommandTeleport> serializedPayload_Teleport) {
        if (this.kind != Kind.Teleport) return false;
        return SpireSerialization.SpireSerializedValuesEqual((object?)this.teleport.target, (object?)serializedPayload_Teleport.value.target) && SpireSerialization.SpireSerializedValuesEqual((object?)this.teleport.duration, (object?)serializedPayload_Teleport.value.duration);
    }
    if (payload is global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Houtamelo.Spire.Showcase.GameplayCommandMove> serializedPayload_Move) {
        if (this.kind != Kind.Move) return false;
        return SpireSerialization.SpireSerializedValuesEqual((object?)this.move.destination, (object?)serializedPayload_Move.value.destination) && SpireSerialization.SpireSerializedValuesEqual((object?)this.move.speed, (object?)serializedPayload_Move.value.speed);
    }
    if (payload is global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Houtamelo.Spire.Showcase.GameplayCommandSpeak> serializedPayload_Speak) {
        if (this.kind != Kind.Speak) return false;
        return SpireSerialization.SpireSerializedValuesEqual((object?)this.speak.speech, (object?)serializedPayload_Speak.value.speech);
    }
    if (payload is global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Houtamelo.Spire.Showcase.GameplayCommandApplyEffect> serializedPayload_ApplyEffect) {
        if (this.kind != Kind.ApplyEffect) return false;
        return SpireSerialization.SpireSerializedManagedReferenceValuesEqual((object?)this.applyEffect.effect, (object?)serializedPayload_ApplyEffect.value.effect);
    }
    return false;
}
```

Use a switch expression here:
```cs
[@EditorBrowsable(EditorBrowsableState.Never)]
public readonly bool __SpireSerializedStateCurrent(object? payload) => (this._kind, payload) switch {
    (Kind.Idle, global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Houtamelo.Spire.Showcase.GameplayCommandIdle> serializedPayload_Idle) => true,
    (Kind.Teleport, global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Houtamelo.Spire.Showcase.GameplayCommandTeleport> serializedPayload_Teleport) => 
        SpireSerialization.SpireSerializedValuesEqual((object?)this.teleport.target, (object?)serializedPayload_Teleport.value.target) 
     && SpireSerialization.SpireSerializedValuesEqual((object?)this.teleport.duration, (object?)serializedPayload_Teleport.value.duration),
    (Kind.Move, global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Houtamelo.Spire.Showcase.GameplayCommandMove> serializedPayload_Move) => 
        SpireSerialization.SpireSerializedValuesEqual((object?)this.move.destination, (object?)serializedPayload_Move.value.destination) 
     && SpireSerialization.SpireSerializedValuesEqual((object?)this.move.speed, (object?)serializedPayload_Move.value.speed),
    (Kind.Speak, global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Houtamelo.Spire.Showcase.GameplayCommandSpeak> serializedPayload_Speak) =>
        SpireSerialization.SpireSerializedValuesEqual((object?)this.speak.speech, (object?)serializedPayload_Speak.value.speech),
    (Kind.ApplyEffect, payload is global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Houtamelo.Spire.Showcase.GameplayCommandApplyEffect> serializedPayload_ApplyEffect) => 
        SpireSerialization.SpireSerializedManagedReferenceValuesEqual((object?)this.applyEffect.effect, (object?)serializedPayload_ApplyEffect.value.effect),
    _ => false,
};
```

---

```cs
[@Preserve]
[@Serializable]
public struct GameplayCommandIdle {

    public readonly void Deconstruct() {
    }
}
```
2 Issues:
- There is an unecessary blank line between the class' opening brace and the first function (Deconstruct).
- If a function body is empty, the two braces should be in the same line.

Correct:
```cs
[@Preserve]
[@Serializable]
public struct GameplayCommandIdle {
    public readonly void Deconstruct() {}
}
```

---

```cs
[@Preserve]
[@Serializable]
public struct GameplayCommandApplyEffect {
    [@SerializeField] [@SerializeReference] global::Houtamelo.Spire.Showcase.Effect? _effect;
```

I believe `[@SerializeField]` is redundant when `[@SerializeReference]` is present, needs double checking.

---

Note: This request is pending discussion, do NOT implement.

```cs
internal static class GameplayCommandDataTypes {
    internal struct ManagedFields {
        [@NonSerialized] internal object? _s0;
    }

    [@StructLayout(LayoutKind.Auto)]
    internal struct UnmanagedFields_Teleport {
        [@NonSerialized] internal global::UnityEngine.Vector2 target;
        [@NonSerialized] internal float duration;
    }

    [@StructLayout(LayoutKind.Auto)]
    internal struct UnmanagedFields_Move {
        [@NonSerialized] internal global::UnityEngine.Vector2 destination;
        [@NonSerialized] internal float speed;
    }

    [@StructLayout(LayoutKind.Explicit)]
    internal struct UnmanagedFields {
        [global::System.Runtime.InteropServices.FieldOffset(0)]
        [@NonSerialized] internal UnmanagedFields_Teleport __teleport;
        [global::System.Runtime.InteropServices.FieldOffset(0)]
        [@NonSerialized] internal UnmanagedFields_Move __move;
    }
}
```

I'm thinking of rearranging where types related to GameplayCommand are placed:
```cs
static class GameplayCommandTypes {
    internal static class Fields {
        internal struct Managed {
            [@NonSerialized] internal object? _s0;
        }
        
        [@StructLayout(LayoutKind.Explicit)]
        internal struct Unmanaged {
            [global::System.Runtime.InteropServices.FieldOffset(0)]
            [@NonSerialized] internal Teleport __teleport;
            [global::System.Runtime.InteropServices.FieldOffset(0)]
            [@NonSerialized] internal Move __move;
        
            [@StructLayout(LayoutKind.Auto)]
            internal struct Teleport {
                [@NonSerialized] internal global::UnityEngine.Vector2 target;
                [@NonSerialized] internal float duration;
            }
        
            [@StructLayout(LayoutKind.Auto)]
            internal struct Move {
                [@NonSerialized] internal global::UnityEngine.Vector2 destination;
                [@NonSerialized] internal float speed;
            }
        }
    }
    
    internal static class Variants {
        [@Preserve]
        [@Serializable]
        public struct Idle {
            public readonly void Deconstruct() { }
        }

        [@Preserve]
        [@Serializable]
        public struct Teleport {
            [@SerializeField] global::UnityEngine.Vector2 _target;
            public readonly global::UnityEngine.Vector2 target => _target;
            
            [@SerializeField] float _duration;
            public readonly float duration => _duration;

            public Teleport(global::UnityEngine.Vector2 target, float duration) {
                this._target = target;
                this._duration = duration;
            }

            public readonly void Deconstruct(out global::UnityEngine.Vector2 target, out float duration) {
                target = this._target;
                duration = this._duration;
            }
        }

        [@Preserve]
        [@Serializable]
        public struct Move {
            [@SerializeField] global::UnityEngine.Vector2 _destination;
            public readonly global::UnityEngine.Vector2 destination => _destination;
            
            [@SerializeField] float _speed;
            public readonly float speed => _speed;

            public Move(global::UnityEngine.Vector2 destination, float speed) {
                this._destination = destination;
                this._speed = speed;
            }

            public readonly void Deconstruct(out global::UnityEngine.Vector2 destination, out float speed) {
                destination = this._destination;
                speed = this._speed;
            }
        }

        [@Preserve]
        [@Serializable]
        public struct Speak {
            [@SerializeField] string _speech;
            public readonly string speech => _speech;

            public Speak(string speech) {
                this._speech = speech;
            }

            public readonly void Deconstruct(out string speech) {
                speech = this._speech;
            }
        }

        [@Preserve]
        [@Serializable]
        public struct Say {
            [@SerializeField] string _speech;
            public readonly string speech => _speech;

            public Say(string speech) {
                this._speech = speech;
            }

            public readonly void Deconstruct(out string speech) {
                speech = this._speech;
            }
        }

        [@Preserve]
        [@Serializable]
        public struct ApplyEffect {
            [@SerializeField] [@SerializeReference] global::Houtamelo.Spire.Showcase.Effect? _effect;
            public readonly global::Houtamelo.Spire.Showcase.Effect? effect => _effect;

            public ApplyEffect(global::Houtamelo.Spire.Showcase.Effect? effect) {
                this._effect = effect;
            }

            public readonly void Deconstruct(out global::Houtamelo.Spire.Showcase.Effect? effect) {
                effect = this._effect;
            }
        }
    }
}
```

*Note: I also changed the layout of blank lines: I intentionally eliminated the blank line between each serialized field and their getter.*

This new arrangement could be used to simplify some type paths, e.g. this:
```cs
if (payload is global::Houtamelo.Spire.Generated.SpireSerializedPayload<global::Houtamelo.Spire.Showcase.GameplayCommandIdle> serializedPayload_Idle) {
    if (this.kind != Kind.Idle) return false;
    return true;
}
```
Becomes:
```cs
using Variants = global::Houtamelo.Spire.Showcase.GameplayCommandTypes.Variants;

if (payload is global::Houtamelo.Spire.Generated.SpireSerializedPayload<Variants.Idle> serializedPayload_Idle) {
    if (this.kind != Kind.Idle) return false;
    return true;
}
```

E.g. 2, this:
```cs
public static partial GameplayCommand Teleport(global::UnityEngine.Vector2 target, float duration) {
    var teleportFields = new GameplayCommandDataTypes.UnmanagedFields_Teleport { target = target, duration = duration };
    var unmanagedFields = new GameplayCommandDataTypes.UnmanagedFields { __teleport = teleportFields };
    var managedFields = new GameplayCommandDataTypes.ManagedFields { _s0 = default! };
    return new GameplayCommand(Kind.Teleport, unmanagedFields, managedFields);
}

public static partial GameplayCommand ApplyEffect(global::Houtamelo.Spire.Showcase.Effect? effect) {
    GameplayCommandDataTypes.UnmanagedFields unmanagedFields = default;
    var managedFields = new GameplayCommandDataTypes.ManagedFields { _s0 = effect };
    return new GameplayCommand(Kind.ApplyEffect, unmanagedFields, managedFields);
}
```
Becomes:
```cs
using Fields = global::Houtamelo.Spire.Showcase.GameplayCommandTypes.Fields;

public static partial GameplayCommand Teleport(global::UnityEngine.Vector2 target, float duration) {
    var teleportFields = new Fields.Unmanaged.Teleport { target = target, duration = duration };
    var unmanagedFields = new Fields.Unmanaged { __teleport = teleportFields };
    var managedFields = new Fields.Managed { _s0 = default! };
    return new GameplayCommand(Kind.Teleport, unmanagedFields, managedFields);
}

public static partial GameplayCommand ApplyEffect(global::Houtamelo.Spire.Showcase.Effect? effect) {
    Fields.Unmanaged unmanagedFields = default;
    var managedFields = new Fields.Managed { _s0 = effect };
    return new GameplayCommand(Kind.ApplyEffect, unmanagedFields, managedFields);
}
```

---
