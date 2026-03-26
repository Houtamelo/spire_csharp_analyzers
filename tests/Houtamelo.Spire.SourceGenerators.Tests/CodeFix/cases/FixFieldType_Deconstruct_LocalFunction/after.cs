using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Pulse
    {
        [Variant] public static partial Pulse High(double voltage);
        [Variant] public static partial Pulse Low(int duration);
    }

    class Consumer
    {
        string Test(Pulse p)
        {
            string Classify(Pulse sig) => sig switch
            {
                (Pulse.Kind.High, double bad) => "high",
                (Pulse.Kind.Low, int x) => "low",
            };
            return Classify(p);
        }
    }
}
