//@ should_pass
// Ensure that SPIRE015 is NOT triggered when a switch statement on PlainEnum has only a default: arm.
public class NoReport_SwitchStatement_PlainEnum_OnlyDefault
{
    public void Method(PlainEnum value)
    {
        switch (value)
        {
            default:
                break;
        }
    }
}
