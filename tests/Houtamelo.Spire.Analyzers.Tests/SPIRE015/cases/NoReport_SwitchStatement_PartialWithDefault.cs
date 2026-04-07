//@ should_pass
// SPIRE015 does NOT fire when a switch statement covers some members and has a default: arm — the catch-all opts out.
public class NoReport_SwitchStatement_PartialWithDefault
{
    public void Method(Color color)
    {
        switch (color)
        {
            case Color.Red:
                break;
            default:
                break;
        }
    }
}
