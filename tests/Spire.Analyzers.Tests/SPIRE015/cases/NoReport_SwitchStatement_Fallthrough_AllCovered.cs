//@ should_pass
// Ensure that SPIRE015 is NOT triggered when fallthrough labels case Red, Green, Blue share one body covering all members.
public class NoReport_SwitchStatement_Fallthrough_AllCovered
{
    public void Method(Color color)
    {
        switch (color)
        {
            case Color.Red:
            case Color.Green:
            case Color.Blue:
                break;
        }
    }
}
