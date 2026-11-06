//@ should_pass
// Ensure that SPIRE009 is NOT triggered when a predicate wildcard covers both nullable states.
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
    static int Read(GameplayCommand command) => command switch
    {
        { IsApplyEffect: true, applyEffect: { effect: var _ } } => 0,
        { IsIdle: true } => 1,
    };
}
