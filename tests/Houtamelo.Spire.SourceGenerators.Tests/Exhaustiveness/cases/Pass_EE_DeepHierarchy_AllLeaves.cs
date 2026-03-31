//@ should_pass
// Deep hierarchy: abstract Base → abstract Middle : Base → sealed LeafA/LeafB : Middle, sealed LeafC : Base
// All 3 leaf types covered — no SPIRE009
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
        int Classify(Base b) => b switch
        {
            LeafA => 1,
            LeafB => 2,
            LeafC => 3,
        };
    }
}
