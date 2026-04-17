//@ should_fail
// SPIRE017: params parameter is not supported by [InlinerStruct].
using Houtamelo.Spire;
namespace TestNs
{
    public static partial class Samples
    {
        [InlinerStruct]
        public static void Take(params int[] xs) //~ ERROR
        { }
    }
}
