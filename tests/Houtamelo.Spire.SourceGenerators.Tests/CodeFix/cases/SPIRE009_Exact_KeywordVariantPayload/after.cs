using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command @event(bool flag);
    [Variant] public static partial Command B();
}

class Consumer
{
    int Read(Command command) => command switch
    {
        { kind: Command.Kind.@event, event_value: { flag: true } } => 1,
        { kind: Command.Kind.B } => 2,
        { Isevent: true, event_value: { flag: false } } => throw new System.NotImplementedException(),
    };
}
