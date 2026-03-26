using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Measure
    {
        [Variant] public static partial Measure Length(int meters);
        [Variant] public static partial Measure Weight(float kilograms);
    }

    class Consumer
    {
        Measure _measure;

        int Tag => _measure switch
        {
            (Measure.Kind.Length, int bad) => 1,
            (Measure.Kind.Weight, float x) => 2,
        };
    }
}
