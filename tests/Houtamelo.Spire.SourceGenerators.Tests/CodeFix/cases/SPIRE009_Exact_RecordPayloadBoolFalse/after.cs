#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
public abstract partial record Result
{
    public sealed partial record A(bool Flag) : Result;
    public sealed partial record B() : Result;
}

class Consumer
{
    int Read(Result result) => result switch
    {
        Result.A { Flag: true } => 1,
        Result.B => 2,
        Result.A { Flag: false } => throw new System.NotImplementedException(),
    };
}
