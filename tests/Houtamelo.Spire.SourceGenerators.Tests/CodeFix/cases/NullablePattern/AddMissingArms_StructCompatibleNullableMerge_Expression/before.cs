#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command ApplyEffect(Effect? effect);
        [Variant] public static partial Command RemoveEffect(Effect effect);
    }

    public sealed class Effect
    {
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            (Command.Kind.ApplyEffect, var effect) => "apply",
        };

        public static Command NullInput() => Command.RemoveEffect(null!);
        public static Command ValueInput() => Command.RemoveEffect(new Effect());

        public static string CaptureEffect(Effect? effect)
            => effect is null ? "null" : "value";
    }
}
