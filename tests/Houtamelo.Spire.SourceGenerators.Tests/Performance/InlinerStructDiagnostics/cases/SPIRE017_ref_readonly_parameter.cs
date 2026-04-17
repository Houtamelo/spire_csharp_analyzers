//@ should_fail
// SPIRE017: ref readonly parameter is not supported by [InlinerStruct].
using Houtamelo.Spire;
namespace TestNs
{
    public static partial class Samples
    {
        [InlinerStruct]
        public static void Take(ref readonly int x) //~ ERROR
        { }
    }
}
