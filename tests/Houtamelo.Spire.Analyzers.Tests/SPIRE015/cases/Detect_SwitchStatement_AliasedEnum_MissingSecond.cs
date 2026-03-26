//@ should_fail
// Ensure that SPIRE015 IS triggered when case AliasedEnum.First is present (covering AlsoFirst) but Second is missing.
public class Detect_SwitchStatement_AliasedEnum_MissingSecond
{
    public void Method(AliasedEnum value)
    {
        switch (value) //~ ERROR
        {
            case AliasedEnum.First:
                break;
        }
    }
}
