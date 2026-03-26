//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch statement covers Red and has a default: arm but leaves Green and Blue uncovered.
public class Detect_SwitchStatement_PartialAndDefault
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
