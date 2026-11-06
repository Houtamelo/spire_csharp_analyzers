#nullable enable
using Houtamelo.Spire;

namespace Shared
{
    [EnforceExhaustiveness]
    public abstract class Root
    {
    }
}

namespace Alpha
{
    public sealed class Node : Shared.Root
    {
    }
}

namespace Beta
{
    public sealed class Node : Shared.Root
    {
    }
}

class Consumer
{
    int Read(Shared.Root root) => root switch
    {
        global::Alpha.Node => 1,
        Beta.Node => throw new System.NotImplementedException(),
    };
}
