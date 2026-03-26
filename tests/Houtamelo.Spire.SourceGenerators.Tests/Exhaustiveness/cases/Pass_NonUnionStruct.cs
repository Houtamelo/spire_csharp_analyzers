//@ should_pass
// Switch on a non-union struct — analyzer should ignore it
using Houtamelo.Spire;
namespace TestNs
{
    struct Point
    {
        public int X;
        public int Y;
    }

    class Consumer
    {
        int Test(Point p) => p switch
        {
            { X: 0, Y: 0 } => 1,
            _ => 2,
        };
    }
}
