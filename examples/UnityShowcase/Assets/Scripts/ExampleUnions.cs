using System;
using UnityEngine;

namespace Houtamelo.Spire.Showcase {

[Serializable]
[DiscriminatedUnion(Layout.Additive)]
public partial struct GameplayCommand {
    [Variant] public static partial GameplayCommand Idle();
    [Variant] public static partial GameplayCommand Move(Vector2 destination, float speed);
    [Variant] public static partial GameplayCommand Run(float angle, float speed);

    [Variant(FormerlySerializedAs = "Say")]
    public static partial GameplayCommand Speak(string speech);

    [Variant] public static partial GameplayCommand ApplyEffect([UnitySerializeReference] Effect? effect);
    [Variant] public static partial GameplayCommand RemoveEffect(string? effect_name);
}

[Serializable]
[DiscriminatedUnion(Layout.Additive)]
public partial struct Shape {
    [Variant] public static partial Shape Circle(float radius);
    [Variant] public static partial Shape Rectangle(float width, float height);
    [Variant] public static partial Shape Point();
}

[Serializable]
[DiscriminatedUnion(Layout.BoxedFields)]
public partial struct OperationResult<T> {
    [Variant] public static partial OperationResult<T> Success(T value);
    [Variant] public static partial OperationResult<T> Failure(string error);
}

[Serializable]
[DiscriminatedUnion(Layout.BoxedTuple)]
public partial struct BoxedTupleStats {
    [Variant] public static partial BoxedTupleStats Stats(int score, string label);
    [Variant] public static partial BoxedTupleStats Empty();
}

[Serializable]
[DiscriminatedUnion(Layout.UnsafeOverlap)]
public partial struct UnsafeStats {
    [Variant] public static partial UnsafeStats Samples(uint raw, float weight);
    [Variant] public static partial UnsafeStats Empty();
}

[Serializable]
[DiscriminatedUnion(Layout.Overlap)]
public partial struct OverlapShape {
    [Variant] public static partial OverlapShape Circle(float radius);
    [Variant] public static partial OverlapShape Rectangle(float width, float height);
    [Variant] public static partial OverlapShape Point();
}

[EnforceExhaustiveness]
public enum ActorMode {
    Idle   = 1,
    Acting = 2,
}

}
