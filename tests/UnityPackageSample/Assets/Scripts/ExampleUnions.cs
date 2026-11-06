using UnityEngine;
using System;

namespace Houtamelo.Spire.Showcase {

[Serializable]
[DiscriminatedUnion]
public partial struct GameplayCommand {
    [Variant] public static partial GameplayCommand Idle();
    [Variant] public static partial GameplayCommand Teleport(Vector2 target, float duration);
    [Variant] public static partial GameplayCommand Move(Vector2 destination, float speed);

    [Variant(FormerlySerializedAs = "Say")]
    public static partial GameplayCommand Speak(string speech);

    [Variant] public static partial GameplayCommand ApplyEffect([UnitySerializeReference] Effect? effect);
}

[Serializable]
[DiscriminatedUnion]
public partial struct Shape {
    [Variant] public static partial Shape Circle(float radius);
    [Variant] public static partial Shape Rectangle(float width, float height);
    [Variant] public static partial Shape Point();
}

[Serializable]
[DiscriminatedUnion]
public partial struct OperationResult<T> {
    [Variant] public static partial OperationResult<T> Success(T value);
    [Variant] public static partial OperationResult<T> Failure(string error);
}

[EnforceExhaustiveness]
public enum ActorMode {
    Idle   = 1,
    Acting = 2,
}

}
