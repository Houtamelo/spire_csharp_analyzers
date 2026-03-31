//@ should_pass
// Plain abstract class without [EnforceExhaustiveness] — incomplete switch causes no SPIRE009
#nullable enable
namespace TestNs
{
    public abstract class Command { }
    public sealed class StartCommand : Command { }
    public sealed class StopCommand : Command { }
    public sealed class ResetCommand : Command { }

    class Consumer
    {
        string Execute(Command c) => c switch
        {
            StartCommand => "started",
            StopCommand => "stopped",
        };
    }
}
