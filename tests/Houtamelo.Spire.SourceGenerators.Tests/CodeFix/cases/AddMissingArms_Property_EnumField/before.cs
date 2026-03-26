using Houtamelo.Spire;
namespace TestNs
{
    enum Status { Active, Inactive }

    [DiscriminatedUnion]
    partial struct Account
    {
        [Variant] public static partial Account Guest(string name);
        [Variant] public static partial Account Member(Status status);
    }

    class Consumer
    {
        int Test(Account a) => a switch
        {
            { kind: Account.Kind.Guest, name: var n } => 1,
        };
    }
}
