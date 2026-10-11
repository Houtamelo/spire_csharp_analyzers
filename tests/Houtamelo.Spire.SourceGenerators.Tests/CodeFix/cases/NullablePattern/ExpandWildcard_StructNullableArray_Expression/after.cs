#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command Batch(string[]? items);
        [Variant] public static partial Command Ready();
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            (Command.Kind.Ready, _) => "ready",
            { kind: Command.Kind.Batch, items: var items } => "wildcard"
        };

        public static Command NullInput() => Command.Batch(null);
        public static Command ValueInput() => Command.Batch(new[] { "item" });

        public static string CaptureItems(string[]? items)
            => items is null ? "null" : "value";
    }
}
