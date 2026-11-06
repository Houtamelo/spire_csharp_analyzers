#nullable enable
using Houtamelo.Spire;

[EnforceExhaustiveness]
public abstract class Root
{
    public bool Flag { get; }

    protected Root(bool flag) => Flag = flag;
}

public sealed class Derived : Root
{
    public Derived(bool flag) : base(flag)
    {
    }
}

class Consumer
{
    int Read(Root value) => value switch
    {
        Derived { Flag: true } => 1,
    };
}
