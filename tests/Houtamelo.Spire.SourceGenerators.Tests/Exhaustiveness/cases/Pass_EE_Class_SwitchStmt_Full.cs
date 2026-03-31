//@ should_pass
// Abstract class switch statement with all subtypes covered — no diagnostic
#nullable enable
using Houtamelo.Spire;
namespace TestNs
{
    [EnforceExhaustiveness]
    public abstract class Fruit { }
    public sealed class Apple : Fruit { }
    public sealed class Banana : Fruit { }
    public sealed class Cherry : Fruit { }

    class Consumer
    {
        int Calories(Fruit f)
        {
            switch (f)
            {
                case Apple:
                    return 95;
                case Banana:
                    return 105;
                case Cherry:
                    return 50;
                default:
                    return 0;
            }
        }
    }
}
