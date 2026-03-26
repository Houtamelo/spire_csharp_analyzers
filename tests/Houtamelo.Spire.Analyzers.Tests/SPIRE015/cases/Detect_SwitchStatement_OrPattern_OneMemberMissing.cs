//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch statement uses case Color.Red or Color.Green but Color.Blue is absent.
public class Detect_SwitchStatement_OrPattern_OneMemberMissing
{
    public void Method(Color color)
    {
        switch (color) //~ ERROR
        {
            case Color.Red or Color.Green:
                break;
        }
    }
}
