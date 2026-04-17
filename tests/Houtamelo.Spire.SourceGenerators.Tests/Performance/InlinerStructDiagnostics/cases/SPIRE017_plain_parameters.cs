//@ should_pass
using Houtamelo.Spire;
namespace TestNs
{
    public static partial class Samples
    {
        [InlinerStruct]
        public static void Take(int x) { }
    }
}
