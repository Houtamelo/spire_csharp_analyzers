#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command Try(int? count);
        [Variant] public static partial Command Ready();
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            (Command.Kind.Ready, _) => "ready",
            { kind: Command.Kind.Try, count: var count } => "wildcard"
        };

        public static Command NullInput() => Command.Try(null);
        public static Command ValueInput() => Command.Try(7);

        public static string CaptureCount(int? count)
            => count is null ? "null" : "value";
    }
}
