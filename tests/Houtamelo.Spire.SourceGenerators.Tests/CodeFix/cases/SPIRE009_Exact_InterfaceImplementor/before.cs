#nullable enable
using Houtamelo.Spire;

namespace Shared
{
    [EnforceExhaustiveness]
    public interface IShape
    {
    }
}

namespace Alpha
{
    public sealed class Circle : Shared.IShape
    {
    }
}

namespace Beta
{
    public sealed class Square : Shared.IShape
    {
    }
}

namespace Other
{
    public sealed class Circle
    {
    }
}

class Consumer
{
    int Read(Shared.IShape shape) => shape switch
    {
        global::Alpha.Circle => 1,
    };
}
