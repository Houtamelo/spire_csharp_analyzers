//@ should_fail
// Ensure that SPIRE009 IS triggered when an IsApplyEffect arm omits the non-null payload partition.
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
        { IsApplyEffect: true, applyEffect: { effect: null } } => 0,
        { IsIdle: true } => 1,
    };
}
