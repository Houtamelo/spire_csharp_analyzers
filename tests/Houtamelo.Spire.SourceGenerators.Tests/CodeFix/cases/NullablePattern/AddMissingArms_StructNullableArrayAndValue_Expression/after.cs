#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command Batch(string[]? items, int? count);
        [Variant] public static partial Command Ready();
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            { kind: Command.Kind.Ready } => "ready",
            { kind: Command.Kind.Batch, items: var items, count: var count } => throw new System.NotImplementedException()
        };

        public static Command NullInput() => Command.Batch(null, null);
        public static Command ValueInput() => Command.Batch(new[] { "item" }, 3);

        public static string CaptureBatch(string[]? items, int? count)
            => $"{(items is null ? "null" : items.Length.ToString())}:{(count is null ? "null" : count.Value.ToString())}";
    }
}
