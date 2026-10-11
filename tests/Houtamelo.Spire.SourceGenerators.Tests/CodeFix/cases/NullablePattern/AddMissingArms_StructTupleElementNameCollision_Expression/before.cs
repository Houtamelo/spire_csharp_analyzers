#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    public sealed class Effect
    {
    }

    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command First((Effect? firstEffect, int firstCount) value);
        [Variant] public static partial Command Second((Effect? secondEffect, int secondCount) value);
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            (Command.Kind.First, var value) => "first",
        };

        public static Command NullInput() => Command.Second((null, 9));
        public static Command ValueInput() => Command.Second((new Effect(), 9));

        public static string CaptureValue((Effect? effect, int count) value)
            => value.effect is null ? "null" : "value";
    }
}
