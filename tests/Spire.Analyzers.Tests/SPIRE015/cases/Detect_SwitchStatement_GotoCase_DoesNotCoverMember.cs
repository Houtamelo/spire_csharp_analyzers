//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch section for Red uses goto case Color.Green and Blue is absent.
public class Detect_SwitchStatement_GotoCase_DoesNotCoverMember
{
    public void Method(Color color)
    {
        switch (color) //~ ERROR
        {
            case Color.Red:
                goto case Color.Green;
            case Color.Green:
                break;
        }
    }
}
