//@ should_fail
// Deep hierarchy: abstract Base → abstract Middle : Base → sealed LeafA/LeafB : Middle, sealed LeafC : Base
// Switch covers LeafA and LeafC but misses LeafB — SPIRE009
#nullable enable
using Houtamelo.Spire;
namespace TestNs
{
    [EnforceExhaustiveness]
    public abstract class Base { }
    public abstract class Middle : Base { }
    public sealed class LeafA : Middle { }
    public sealed class LeafB : Middle { }
    public sealed class LeafC : Base { }

    class DeepConsumer
    {
        int Classify(Base b) => b switch //~ ERROR
        {
            LeafA => 1,
            LeafC => 3,
        };
    }
}
