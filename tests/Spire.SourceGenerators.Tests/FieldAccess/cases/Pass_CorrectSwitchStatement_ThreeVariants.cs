//@ should_pass
// 3-variant switch statement — each case accesses its own field
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Animal
    {
        [Variant] public static partial Animal Dog(string dogName);
        [Variant] public static partial Animal Cat(int catAge);
        [Variant] public static partial Animal Bird(double wingSpan);
    }
    class C
    {
        string Describe(Animal a)
        {
            switch (a)
            {
                case (Animal.Kind.Dog, _):
                    return a.dogName;
                case (Animal.Kind.Cat, _):
                    return a.catAge.ToString();
                case (Animal.Kind.Bird, _):
                    return a.wingSpan.ToString();
                default:
                    return "unknown";
            }
        }
    }
}
