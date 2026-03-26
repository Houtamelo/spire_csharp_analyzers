//@ should_pass
// Ensure that SPIRE015 is NOT triggered when case Red, Green, Blue, and default all fall through to a shared body.
public class NoReport_SwitchStatement_FallthroughToDefault_AllCovered
{
    public void Method(Color color)
    {
        switch (color)
        {
            case Color.Red:
            case Color.Green:
            case Color.Blue:
            default:
                break;
        }
    }
}
