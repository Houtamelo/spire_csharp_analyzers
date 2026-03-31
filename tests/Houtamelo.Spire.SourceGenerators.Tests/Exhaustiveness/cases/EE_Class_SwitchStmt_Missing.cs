//@ should_fail
// Abstract class switch statement missing Sparrow subtype — SPIRE009
#nullable enable
using Houtamelo.Spire;
namespace TestNs
{
    [EnforceExhaustiveness]
    public abstract class Bird { }
    public sealed class Eagle : Bird { }
    public sealed class Parrot : Bird { }
    public sealed class Sparrow : Bird { }

    class Consumer
    {
        void Handle(Bird b)
        {
            switch (b) //~ ERROR
            {
                case Eagle:
                    break;
                case Parrot:
                    break;
            }
        }
    }
}
