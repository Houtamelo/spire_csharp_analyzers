#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command<T>
    {
        [Variant] public static partial Command<T> Ready();
        [Variant] public static partial Command<T> Value(T value);
    }

    public static class Probe
    {
        public static string Evaluate(Command<int> command) => command switch
        {
            { kind: Command<int>.Kind.Ready } => "ready",
            { kind: Command<int>.Kind.Value, value: var value } => throw new System.NotImplementedException()
        };

        public static Command<int> ValueInput() => Command<int>.Value(42);

        public static string CaptureInt(int value) => value.ToString();
    }
}
