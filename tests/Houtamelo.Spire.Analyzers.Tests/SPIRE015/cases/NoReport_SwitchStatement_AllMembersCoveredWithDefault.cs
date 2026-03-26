//@ should_pass
// Ensure that SPIRE015 is NOT triggered when a switch statement covers all three members of Color AND has a default: arm.
public class NoReport_SwitchStatement_AllMembersCoveredWithDefault
{
    public void Method(Color color)
    {
        switch (color)
        {
            case Color.Red:
                break;
            case Color.Green:
                break;
            case Color.Blue:
                break;
            default:
                break;
        }
    }
}
