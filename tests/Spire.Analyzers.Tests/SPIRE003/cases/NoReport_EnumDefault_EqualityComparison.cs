//@ should_pass
// Ensure that SPIRE003 is NOT triggered when comparing a [MustBeInit] enum value with default in an equality expression.
public class NoReport_EnumDefault_EqualityComparison
{
    void M(MustInitEnumNoZero someEnum)
    {
        bool isDefault = someEnum == default;
    }
}
