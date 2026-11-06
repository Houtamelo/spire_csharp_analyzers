//@ should_fail
// Ensure that SPIRE009 IS triggered when a nullable payload variant has no payload arm.
#nullable enable
using Houtamelo.Spire;

public abstract class Effect
{
    public abstract int Apply(int value);
}

[DiscriminatedUnion]
partial struct GameplayCommand
{
    [Variant] public static partial GameplayCommand ApplyEffect(
        [UnitySerializeReference] Effect? effect);
    [Variant] public static partial GameplayCommand Idle();
}

static class Consumer
{
    static int Read(GameplayCommand command) => command switch //~ ERROR
    {
        { kind: GameplayCommand.Kind.Idle } => 1,
    };
}
