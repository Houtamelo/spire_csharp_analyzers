#nullable enable
using Houtamelo.Spire;

namespace Shapes
{
    [EnforceExhaustiveness]
    public abstract class Base<T>
    {
    }

    public sealed class Leaf<T> : Base<T>
    {
    }
}

class Consumer
{
    int Read(Shapes.Base<int> value) => value switch
    {
        global::Shapes.Leaf<int> when false => 1,
    };
}
