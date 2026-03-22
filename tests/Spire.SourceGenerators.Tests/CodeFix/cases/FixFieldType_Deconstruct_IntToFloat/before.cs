using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Reading
    {
        [Variant] public static partial Reading Sensor(float value);
        [Variant] public static partial Reading Counter(int count);
    }

    class Consumer
    {
        int Test(Reading r) => r switch
        {
            (Reading.Kind.Sensor, int bad) => 1,
            (Reading.Kind.Counter, int x) => 2,
        };
    }
}
