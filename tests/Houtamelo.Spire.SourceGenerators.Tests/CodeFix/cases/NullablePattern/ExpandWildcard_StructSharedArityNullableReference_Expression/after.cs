#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command ApplyEffect(Effect? effect);
        [Variant] public static partial Command Log(string text);
    }

    public sealed class Effect
    {
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            (Command.Kind.Log, string text) => $"log:{text}",
            { kind: Command.Kind.ApplyEffect, effect: var effect } => "wildcard"
        };

        public static Command NullInput() => Command.ApplyEffect(null);
        public static Command ValueInput() => Command.ApplyEffect(new Effect());

        public static string CaptureEffect(Effect? effect)
            => effect is null ? "null" : "value";
    }
}
