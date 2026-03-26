//@ should_fail
// Ensure that SPIRE015 IS triggered when the only arm is default: on Color (all three members uncovered).
public class Detect_SwitchStatement_DefaultDoesNotCoverMembers
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
