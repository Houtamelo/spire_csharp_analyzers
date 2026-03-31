//@ should_fail
// Interface with 3 sealed implementors, switch expression missing Triangle — SPIRE009
#nullable enable
using Houtamelo.Spire;
namespace TestNs
{
    [EnforceExhaustiveness]
    public interface IShape { }
    public sealed class Circle : IShape { }
    public sealed class Square : IShape { }
    public sealed class Triangle : IShape { }

    class Consumer
    {
        int Test(IShape s) => s switch //~ ERROR
        {
            Circle => 1,
            Square => 2,
        };
    }
}
