using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct EventCommand
{
    [Variant] public static partial EventCommand Event(string? @event);
    [Variant] public static partial EventCommand Idle();
}

public static class Consumer
{
    public static string Match(EventCommand command) => command switch
    {
        { kind: EventCommand.Kind.Idle } => "idle",
        { IsEvent: true, @event: var (@event) } => throw new System.NotImplementedException()
    };
}
