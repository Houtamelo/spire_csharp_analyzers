using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Signal
    {
        [Variant] public static partial Signal High(double voltage);
        [Variant] public static partial Signal Low(double voltage);
    }

    class Consumer
    {
        int Process(Signal s)
        {
            int Classify(Signal sig) => sig switch
            {
                (Signal.Kind.High, var v) => 1,
                (Signal.Kind.Low, double voltage) => throw new System.NotImplementedException()
            };
            return Classify(s);
        }
    }
}
