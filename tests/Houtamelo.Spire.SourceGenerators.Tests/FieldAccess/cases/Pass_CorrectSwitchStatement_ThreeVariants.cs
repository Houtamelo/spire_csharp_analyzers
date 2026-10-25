//@ should_pass
// 3-variant switch statement — each case accesses its own field
using Houtamelo.Spire;
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
                case { kind: Animal.Kind.Dog, dog: { dogName: var name } }:
                    return name;
                case { kind: Animal.Kind.Cat, cat: { catAge: var age } }:
                    return age.ToString();
                case { kind: Animal.Kind.Bird, bird: { wingSpan: var span } }:
                    return span.ToString();
                default:
                    return "unknown";
            }
        }
    }
}
