#nullable enable
using Houtamelo.Spire;

[EnforceExhaustiveness]
public abstract class Root
{
}

public sealed class Wrapper : Root
{
    public Base<int> Value { get; }

    public Wrapper(Base<int> value) => Value = value;
}

[EnforceExhaustiveness]
public abstract class Base<T>
{
}

public sealed class LeafA<T> : Base<T>
{
}

public sealed class LeafB<T> : Base<T>
{
}

class Consumer
{
    int Read(Root value) => value switch
    {
        Wrapper { Value: LeafA<int> } => 1,
    };
}
