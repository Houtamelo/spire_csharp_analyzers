//@ should_fail
// Ensure that SPIRE015 IS triggered when case Color.Red when condition is the only arm — the guard disqualifies it from coverage.
public class Detect_SwitchStatement_WhenGuard_MemberNotCovered
{
    public void Method(Color color, bool condition)
    {
        switch (color) //~ ERROR
        {
            case Color.Red when condition:
                break;
        }
    }
}
