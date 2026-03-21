//@ should_pass
// Ensure that SPIRE015 is NOT triggered when AliasedEnum.AlsoFirst and AliasedEnum.Second arms cover all distinct values.
public class NoReport_SwitchExpression_AliasedEnum_AllCoveredViaAliasArm
{
    public int Method(AliasedEnum value)
    {
        return value switch
        {
            AliasedEnum.AlsoFirst => 0,
            AliasedEnum.Second => 1,
        };
    }
}
