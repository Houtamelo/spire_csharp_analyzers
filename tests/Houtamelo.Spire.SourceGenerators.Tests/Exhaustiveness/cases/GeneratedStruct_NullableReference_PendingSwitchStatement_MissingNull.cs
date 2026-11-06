//@ should_fail
// Ensure that SPIRE009 IS triggered when the pending five-variant switch omits ApplyEffect null values.
#nullable enable
using Houtamelo.Spire;

public readonly struct Point
{
}

public abstract class Effect
{
    public abstract int Apply(int value);
}

[DiscriminatedUnion]
partial struct GameplayCommand
{
    [Variant] public static partial GameplayCommand Idle();
    [Variant] public static partial GameplayCommand Teleport(Point target, float duration);
    [Variant] public static partial GameplayCommand Move(Point destination, float speed);
    [Variant] public static partial GameplayCommand Speak(string speech);
    [Variant] public static partial GameplayCommand ApplyEffect(
        [UnitySerializeReference] Effect? effect);
}

static class Consumer
{
    static void Execute(GameplayCommand command)
    {
        switch (command) //~ ERROR
        {
            case { kind: GameplayCommand.Kind.Idle }:
                throw new System.NotImplementedException();
            case { kind: GameplayCommand.Kind.Teleport }:
                throw new System.NotImplementedException();
            case { kind: GameplayCommand.Kind.Move, move: { destination: var destination, speed: var speed } }:
                throw new System.NotImplementedException();
            case { kind: GameplayCommand.Kind.Speak, speak: { speech: var speech } }:
                throw new System.NotImplementedException();
            case { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: { effect: not null } }:
                throw new System.NotImplementedException();
        }
    }
}
