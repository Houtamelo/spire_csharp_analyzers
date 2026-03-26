//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch statement covers Red and Green but not Blue of Color.
public class Detect_SwitchStatement_OneMemberMissing
{
    public void Method(Color color)
    {
        switch (color) //~ ERROR
        {
            case Color.Red:
                break;
            case Color.Green:
                break;
        }
    }
}
