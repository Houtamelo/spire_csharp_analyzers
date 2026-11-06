//@ should_fail
// Ensure that SPIRE009 IS triggered when a switch statement omits the non-null payload partition.
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
    static void Read(GameplayCommand command)
    {
        switch (command) //~ ERROR
        {
            case { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: { effect: null } }:
                break;
            case { kind: GameplayCommand.Kind.Idle }:
                break;
        }
    }
}
