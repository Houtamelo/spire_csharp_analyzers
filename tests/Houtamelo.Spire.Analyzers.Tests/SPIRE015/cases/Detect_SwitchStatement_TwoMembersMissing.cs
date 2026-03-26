//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch statement covers only Red, leaving Green and Blue uncovered.
public class Detect_SwitchStatement_TwoMembersMissing
{
    public void Method(Color color)
    {
        switch (color) //~ ERROR
        {
            case Color.Red:
                break;
        }
    }
}
