//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch statement has only a default: arm with no explicit member arms on Color.
public class Detect_SwitchStatement_OnlyDefault
{
    public void Method(Color color)
    {
        switch (color) //~ ERROR
        {
            default:
                break;
        }
    }
}
