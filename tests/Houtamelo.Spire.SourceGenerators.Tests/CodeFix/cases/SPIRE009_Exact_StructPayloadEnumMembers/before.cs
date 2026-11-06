using Houtamelo.Spire;

enum Color
{
    Red,
    Green,
    Blue,
}

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command A(Color color);
    [Variant] public static partial Command B();
}

class Consumer
{
    int Read(Command command) => command switch
    {
        { kind: Command.Kind.A, a: { color: Color.Red } } => 1,
        { kind: Command.Kind.B } => 2,
    };
}
