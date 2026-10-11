using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command ApplyEffect(Effect effect);
        [Variant] public static partial Command Move(int x, int y);
    }

    public sealed class Effect
    {
    }

    public static class Probe
    {
        public static string Evaluate(Command command)
        {
            switch (command)
            {
                case (Command.Kind.Move, int x, int y):
                    return $"move:{x + y}";
            }

            return "unmatched";
        }

        public static Command ValueInput() => Command.ApplyEffect(new Effect());

        public static string CaptureEffect(Effect effect)
            => effect is null ? "null" : "value";
    }
}
