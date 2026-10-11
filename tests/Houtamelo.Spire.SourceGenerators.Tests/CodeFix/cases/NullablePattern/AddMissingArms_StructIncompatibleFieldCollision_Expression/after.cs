#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command Count(int value);
        [Variant] public static partial Command Text(string value);
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            (Command.Kind.Count, int value) => value.ToString(),
            { kind: Command.Kind.Text, text_value: var value } => throw new System.NotImplementedException()
        };

        public static Command ValueInput() => Command.Text("text");

        public static string CaptureText(string value) => value;
    }
}
