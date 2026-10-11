#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    public static partial class Outer<T>
    {
        [DiscriminatedUnion(Layout.Additive)]
        public partial struct Command
        {
            [Variant] public static partial Command Ready();
            [Variant] public static partial Command Value(T value);
        }
    }

    public static class Probe
    {
        public static string Evaluate(Outer<int>.Command command) => command switch
        {
            { kind: Outer<int>.Command.Kind.Ready } => "ready",
        };

        public static Outer<int>.Command ValueInput() => Outer<int>.Command.Value(42);

        public static string CaptureInt(int value) => value.ToString();
    }
}
