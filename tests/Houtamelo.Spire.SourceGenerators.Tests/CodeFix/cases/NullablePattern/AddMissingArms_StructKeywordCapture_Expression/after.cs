#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command Ready();
        [Variant] public static partial Command Event(string @event);
    }

    public static class Probe
    {
        public static string Evaluate(Command command) => command switch
        {
            { kind: Command.Kind.Ready } => "ready",
            { kind: Command.Kind.Event, @event: var @event } => throw new System.NotImplementedException()
        };

        public static Command ValueInput() => Command.Event("event");

        public static string CaptureEvent(string @event) => @event;
    }
}
