//@ should_pass
// Ensure that SPIRE015 is NOT triggered when switching on an int value (not an enum at all).
public class NoReport_SwitchStatement_BuiltinInt_Incomplete
{
    public void Method(int value)
    {
        switch (value)
        {
            case 1:
                break;
            case 2:
                break;
        }
    }
}
