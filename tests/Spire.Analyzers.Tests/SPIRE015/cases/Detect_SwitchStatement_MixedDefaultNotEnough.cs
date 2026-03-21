//@ should_fail
// Ensure that SPIRE015 IS triggered when arms cover Red only and default: handles the rest — Green and Blue remain uncovered from the rule's perspective.
public class Detect_SwitchStatement_MixedDefaultNotEnough
{
    public void Method(Color color)
    {
        switch (color) //~ ERROR
        {
            case Color.Red:
                break;
            default:
                break;
        }
    }
}
