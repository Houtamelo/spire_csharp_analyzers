using Houtamelo.Spire;

namespace TestNs;

public partial class Outer
{
    public partial class Inner
    {
        [InlinerStruct]
        public static int Double(int x) => x * 2;
    }
}
