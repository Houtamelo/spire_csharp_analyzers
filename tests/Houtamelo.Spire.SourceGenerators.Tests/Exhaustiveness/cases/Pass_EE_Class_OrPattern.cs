//@ should_pass
// Abstract class with all subtypes covered via or pattern — no diagnostic
#nullable enable
using Houtamelo.Spire;
namespace TestNs
{
    [EnforceExhaustiveness]
    public abstract class Permission { }
    public sealed class ReadPermission : Permission { }
    public sealed class WritePermission : Permission { }
    public sealed class ExecutePermission : Permission { }

    class Consumer
    {
        bool IsAllowed(Permission p) => p switch
        {
            ReadPermission or WritePermission => true,
            ExecutePermission => false,
        };
    }
}
