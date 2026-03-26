//@ should_pass
// Ensure that SPIRE015 is NOT triggered when switch ((int)color) is used — the switched type is int, not Color.
public class NoReport_SwitchStatement_CastToInt_PlainSwitch
{
    public void Method(Color color)
    {
        switch ((int)color)
        {
            case 0:
                break;
        }
    }
}
