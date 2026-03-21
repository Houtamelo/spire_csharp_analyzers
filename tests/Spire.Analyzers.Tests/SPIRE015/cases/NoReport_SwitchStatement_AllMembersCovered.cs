//@ should_pass
// Ensure that SPIRE015 is NOT triggered when a switch statement covers all three members of Color (Red, Green, Blue).
public class NoReport_SwitchStatement_AllMembersCovered
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
        }
    }
}
