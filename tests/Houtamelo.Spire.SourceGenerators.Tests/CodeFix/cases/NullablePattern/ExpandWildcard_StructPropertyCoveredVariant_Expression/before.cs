#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command ApplyEffect(Effect? effect);
        [Variant] public static partial Command Ready();
    }

    public sealed class Effect
    {
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            { kind: Command.Kind.Ready } => "ready",
            _ => "wildcard",
        };

        public static Command NullInput() => Command.ApplyEffect(null);
        public static Command ValueInput() => Command.ApplyEffect(new Effect());

        public static string CaptureEffect(Effect? effect)
            => effect is null ? "null" : "value";
    }
}
