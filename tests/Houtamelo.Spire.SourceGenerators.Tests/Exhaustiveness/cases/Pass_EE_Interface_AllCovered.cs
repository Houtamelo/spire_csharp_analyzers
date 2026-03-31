//@ should_pass
// Interface with 3 sealed implementors, all covered — no SPIRE009
#nullable enable
using Houtamelo.Spire;
namespace TestNs
{
    [EnforceExhaustiveness]
    public interface ICommand { }
    public sealed class CreateCommand : ICommand { }
    public sealed class UpdateCommand : ICommand { }
    public sealed class DeleteCommand : ICommand { }

    class CommandHandler
    {
        string Handle(ICommand cmd) => cmd switch
        {
            CreateCommand => "create",
            UpdateCommand => "update",
            DeleteCommand => "delete",
        };
    }
}
