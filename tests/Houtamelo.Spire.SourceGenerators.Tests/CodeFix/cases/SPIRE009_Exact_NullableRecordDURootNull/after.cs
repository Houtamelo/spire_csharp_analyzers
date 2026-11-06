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
    int Read(Option<int>? option) => option switch
    {
        Option<int>.Some => 1,
        Option<int>.None => 0,
        null => throw new System.NotImplementedException(),
    };
}
