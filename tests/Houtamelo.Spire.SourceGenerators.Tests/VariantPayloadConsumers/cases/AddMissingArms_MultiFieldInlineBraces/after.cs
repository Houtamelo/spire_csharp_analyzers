using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct GameplayCommand
{
    [Variant] public static partial GameplayCommand Move(float angle, float speed);
    [Variant] public static partial GameplayCommand Speak(string speech);
    [Variant] public static partial GameplayCommand Idle();
}

public static class Consumer
{
    public static string Match(GameplayCommand command) => command switch
    {
        { kind: GameplayCommand.Kind.Move, move: { angle: var angle, speed: var speed } } => $"{angle}:{speed}",
        { kind: GameplayCommand.Kind.Speak, speak: { speech: var speech } } => throw new System.NotImplementedException(),
        { kind: GameplayCommand.Kind.Idle } => throw new System.NotImplementedException()
    };
}
