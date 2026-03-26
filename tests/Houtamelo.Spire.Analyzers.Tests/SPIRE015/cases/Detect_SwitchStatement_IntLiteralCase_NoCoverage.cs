//@ should_fail
// Ensure that SPIRE015 IS triggered when the switch uses case 0: (int literal, not enum-typed) on Color — no member coverage.
public class Detect_SwitchStatement_IntLiteralCase_NoCoverage
{
    public void Method(Color value)
    {
        switch (value) //~ ERROR
        {
            case 0:
                break;
        }
    }
}
