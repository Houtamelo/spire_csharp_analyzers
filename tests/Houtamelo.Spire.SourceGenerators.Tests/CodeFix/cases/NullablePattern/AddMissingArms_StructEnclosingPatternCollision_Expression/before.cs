#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command Ready();
        [Variant] public static partial Command Count(int value);
    }

    public static class Probe
    {
        public static string Evaluate(Command command)
        {
            object input = 1;
            if (input is int value)
            {
                return command switch
                {
                    { kind: Command.Kind.Ready } => value.ToString(),
                };
            }

            return "unmatched";
        }

        public static Command ValueInput() => Command.Count(42);

        public static string CaptureInt(int value) => value.ToString();
    }
}
