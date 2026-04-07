//@ should_fail
// A guarded discard `_ when cond =>` is NOT an unconditional catch-all — SPIRE015 still fires for missing members.
public class Detect_SwitchExpression_GuardedDiscard_NotCatchAll
{
    public string Method(Color color, bool condition)
    {
        return color switch //~ ERROR
        {
            Color.Red => "red",
            _ when condition => "guarded",
        };
    }
}
