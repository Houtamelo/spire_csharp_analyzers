//@ should_fail
// Class union switch where Ok arm has when guard — not exhaustive — SPIRE009
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public abstract partial class Outcome
    {
        public sealed partial class Succeeded : Outcome
        {
            public int Score { get; }
            public Succeeded(int score) { Score = score; }
        }
        public sealed partial class Failed : Outcome
        {
            public string Reason { get; }
            public Failed(string reason) { Reason = reason; }
        }
    }

    class OutcomeConsumer
    {
        string Describe(Outcome o) => o switch //~ ERROR
        {
            Outcome.Succeeded s when s.Score > 50 => $"high:{s.Score}",
            Outcome.Failed f => f.Reason,
        };
    }
}
