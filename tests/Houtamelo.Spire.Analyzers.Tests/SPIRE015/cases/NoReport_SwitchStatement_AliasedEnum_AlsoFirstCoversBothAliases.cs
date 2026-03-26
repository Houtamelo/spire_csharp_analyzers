//@ should_pass
// Ensure that SPIRE015 is NOT triggered when case AliasedEnum.AlsoFirst and case AliasedEnum.Second cover all distinct values.
public class NoReport_SwitchStatement_AliasedEnum_AlsoFirstCoversBothAliases
{
    public void Method(AliasedEnum value)
    {
        switch (value)
        {
            case AliasedEnum.AlsoFirst:
                break;
            case AliasedEnum.Second:
                break;
        }
    }
}
