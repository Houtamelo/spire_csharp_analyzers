//@ should_pass
// No diagnostics: class union with two proper nested variant classes
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public abstract partial class Payment
    {
        public sealed partial class CreditCard : Payment { public string CardNumber { get; } public CreditCard(string n) => CardNumber = n; }
        public sealed partial class Cash : Payment { public decimal Amount { get; } public Cash(decimal a) => Amount = a; }
    }
}
