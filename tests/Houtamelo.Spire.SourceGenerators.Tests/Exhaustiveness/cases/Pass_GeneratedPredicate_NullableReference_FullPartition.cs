//@ should_pass
// Ensure that SPIRE009 is NOT triggered when predicate arms cover both nullable payload partitions.
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
        { IsApplyEffect: true, applyEffect: { effect: null } } => 0,
        { IsApplyEffect: true, applyEffect: { effect: not null } } => 1,
        { IsIdle: true } => 2,
    };
}
