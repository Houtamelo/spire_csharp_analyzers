#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
public abstract partial record Option<T>
{
    public sealed partial record Some(T Value) : Option<T>;
    public sealed partial record None() : Option<T>;
}

class Consumer
{
    int Read(Option<int> option) => option switch
    {
        Option<int>.None => 0,
        Option<int>.Some(int Value) => throw new System.NotImplementedException()
    };
}
