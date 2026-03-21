//@ should_pass
// Ensure that SPIRE015 is NOT triggered when case Color.Red or Color.Green and case Color.Blue together cover all members.
public class NoReport_SwitchStatement_OrPattern_AllCovered
{
    public void Method(Color color)
    {
        switch (color)
        {
            case Color.Red or Color.Green:
                break;
            case Color.Blue:
                break;
        }
    }
}
