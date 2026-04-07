//@ should_pass
// SPIRE015 does NOT fire when default: is present, even if other cases use `goto default` — the catch-all opts out.
public class NoReport_SwitchStatement_GotoDefault
{
    public void Method(Color color)
    {
        switch (color)
        {
            case Color.Red:
                goto default;
            default:
                break;
        }
    }
}
