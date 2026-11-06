#nullable enable
using Houtamelo.Spire;

public sealed class Inner
{
    public bool Flag { get; }

    public Inner(bool flag) => Flag = flag;
}

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command A(Inner? inner);
    [Variant] public static partial Command B();
}

class Consumer
{
    int Read(Command command) => command switch
    {
        { kind: Command.Kind.A, a: { inner: { Flag: true } } } => 1,
        { kind: Command.Kind.A, a: { inner: { Flag: false } } } => 2,
        { kind: Command.Kind.B } => 3,
        { IsA: true, a: { inner: null } } => throw new System.NotImplementedException(),
    };
}
