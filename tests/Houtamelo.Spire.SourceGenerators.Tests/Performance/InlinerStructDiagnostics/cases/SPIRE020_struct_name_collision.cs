//@ should_fail
// SPIRE020: struct FooInliner already exists in the declaring type.
using Houtamelo.Spire;
namespace TestNs
{
    public partial class Host
    {
        public class FooInliner { }
        [InlinerStruct]
        public static int Foo() => 0; //~ ERROR
    }
}
