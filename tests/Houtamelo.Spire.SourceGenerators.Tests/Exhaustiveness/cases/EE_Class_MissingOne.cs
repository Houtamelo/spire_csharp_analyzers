//@ should_fail
// Abstract class with 3 sealed subtypes, switch expression missing Bird — SPIRE009
#nullable enable
using Houtamelo.Spire;
namespace TestNs
{
    [EnforceExhaustiveness]
    public abstract class Animal { }
    public sealed class Dog : Animal { }
    public sealed class Cat : Animal { }
    public sealed class Bird : Animal { }

    class Consumer
    {
        int Test(Animal a) => a switch //~ ERROR
        {
            Dog => 1,
            Cat => 2,
        };
    }
}
