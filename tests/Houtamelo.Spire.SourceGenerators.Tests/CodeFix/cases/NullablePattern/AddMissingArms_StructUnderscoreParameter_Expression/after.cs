#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command Ready();
        [Variant] public static partial Command Value(int _);
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            { kind: Command.Kind.Ready } => "ready",
            { kind: Command.Kind.Value, @_: var _1 } => throw new System.NotImplementedException()
        };

        public static Command ValueInput() => Command.Value(42);

        public static string CaptureInt(int value) => value.ToString();
    }
}
