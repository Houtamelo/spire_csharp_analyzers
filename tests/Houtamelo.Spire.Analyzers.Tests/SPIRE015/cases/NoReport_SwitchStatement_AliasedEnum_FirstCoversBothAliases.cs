//@ should_pass
// Ensure that SPIRE015 is NOT triggered when case AliasedEnum.First and case AliasedEnum.Second cover all distinct values.
public class NoReport_SwitchStatement_AliasedEnum_FirstCoversBothAliases
{
    public void Method(AliasedEnum value)
    {
        switch (value)
        {
            case AliasedEnum.First:
                break;
            case AliasedEnum.Second:
                break;
        }
    }
}
