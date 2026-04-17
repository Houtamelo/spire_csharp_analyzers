using Houtamelo.Spire;

namespace TestNs;

public partial class Calc
{
    [InlinerStruct]
    public int Mul(int a, int b) => a * b;
}
