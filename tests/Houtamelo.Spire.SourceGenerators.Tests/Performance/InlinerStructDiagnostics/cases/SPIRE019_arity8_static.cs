//@ should_pass
using Houtamelo.Spire;
namespace TestNs
{
    public static partial class Samples
    {
        [InlinerStruct]
        public static void Take(int a, int b, int c, int d, int e, int f, int g, int h) { }
    }
}
