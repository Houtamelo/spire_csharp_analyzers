//@ should_pass
#nullable enable
// Record union with inlined call in switch expression — non-nullable, null case NOT required
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public abstract partial record Action
    {
        public sealed partial record Click(int x) : Action;
        public sealed partial record Scroll(int delta) : Action;
    }

    class InlinedSwitchExprConsumer
    {
        Action GetAction() => new Action.Click(0);

        string Handle()
        {
            return GetAction() switch
            {
                Action.Click c => $"click:{c.x}",
                Action.Scroll s => $"scroll:{s.delta}",
            };
        }
    }
}
