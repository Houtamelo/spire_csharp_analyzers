//@ should_fail
// All-fieldless union missing Green variant — SPIRE009
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct TrafficLight
    {
        [Variant] public static partial TrafficLight Red();
        [Variant] public static partial TrafficLight Yellow();
        [Variant] public static partial TrafficLight Green();
    }

    class TrafficLightConsumer
    {
        int Delay(TrafficLight light) => light switch //~ ERROR
        {
            (TrafficLight.Kind.Red, _) => 30,
            (TrafficLight.Kind.Yellow, _) => 5,
        };
    }
}
