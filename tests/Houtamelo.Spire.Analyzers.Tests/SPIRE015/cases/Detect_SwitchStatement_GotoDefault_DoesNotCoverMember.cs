//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch section for Red uses goto default and no other member arms are present.
public class Detect_SwitchStatement_GotoDefault_DoesNotCoverMember
{
    public void Method(Color color)
    {
        switch (color) //~ ERROR
        {
            case Color.Red:
                goto default;
            default:
                break;
        }
    }
}
