//@ should_fail
// Ensure that SPIRE015 IS triggered when case Color.Red and Color.Green fall through to a shared body but Color.Blue is absent.
public class Detect_SwitchStatement_Fallthrough_OneMemberStillMissing
{
    public void Method(Color color)
    {
        switch (color) //~ ERROR
        {
            case Color.Red:
            case Color.Green:
                break;
        }
    }
}
