//@ should_pass
// SPIRE015 does NOT fire when an unguarded default: arm is the sole clause — the catch-all opts out of the check.
public class NoReport_SwitchStatement_OnlyDefault
{
    public void Method(Color color)
    {
        switch (color)
        {
            default:
                break;
        }
    }
}
