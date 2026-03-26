using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Signal
    {
        [Variant] public static partial Signal Wave(double amplitude);
        [Variant] public static partial Signal Noise(int level);
    }

    class Consumer
    {
        string Test(Signal s)
        {
            string Classify(Signal sig) => sig switch
            {
                { kind: Signal.Kind.Wave, amplitude: var bad } => "wave",
                { kind: Signal.Kind.Noise, level: var x } => "noise",
            };
            return Classify(s);
        }
    }
}
