using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Token
{
    [Variant] public static partial Token Active(bool flag, int count);
    [Variant] public static partial Token Idle();
}

class Consumer
{
    static int Read(Token token) => token switch
    {
        { IsActive: true, active: (true, _) } => 1,
        { IsIdle: true } => 2,
    };
}
