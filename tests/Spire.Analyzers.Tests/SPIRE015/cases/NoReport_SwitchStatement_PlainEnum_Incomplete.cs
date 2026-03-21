//@ should_pass
// Ensure that SPIRE015 is NOT triggered when an incomplete switch statement on PlainEnum (no attribute) is present.
public class NoReport_SwitchStatement_PlainEnum_Incomplete
{
    public void Method(PlainEnum value)
    {
        switch (value)
        {
            case PlainEnum.A:
                break;
        }
    }
}
