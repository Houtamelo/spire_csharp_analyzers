//@ should_pass
// Ensure that SPIRE015 is NOT triggered when all enum members equal 0 and any single member is handled.
[EnforceExhaustiveness]
public enum AllZeroEnum
{
    First = 0,
    Second = 0,
    Third = 0,
}

public class NoReport_SwitchStatement_Flags_AllZeroValuedCoveredByAnySingleCase
{
    public void Method(AllZeroEnum value)
    {
        switch (value)
        {
            case AllZeroEnum.Second:
                break;
        }
    }
}
