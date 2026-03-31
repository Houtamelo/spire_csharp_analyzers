//@ should_pass
#nullable enable
// Record union with inlined method call — non-nullable return, null case NOT required
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public abstract partial record Signal
    {
        public sealed partial record Start(string name) : Signal;
        public sealed partial record Stop(int code) : Signal;
    }

    class InlinedCallConsumer
    {
        Signal GetSignal() => new Signal.Start("test");

        void Handle()
        {
            switch (GetSignal())
            {
                case Signal.Start s:
                    System.Console.WriteLine(s.name);
                    break;
                case Signal.Stop s:
                    System.Console.WriteLine(s.code);
                    break;
            }
        }
    }
}
