using Houtamelo.Spire;

public struct Inner
{
    public bool flag;
}

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command A(Inner inner);
    [Variant] public static partial Command B();
}

class Consumer
{
    int Read(Command command) => command switch
    {
        { kind: Command.Kind.A, a: { inner: { flag: true } } } => 1,
        { kind: Command.Kind.B } => 2,
    };
}
