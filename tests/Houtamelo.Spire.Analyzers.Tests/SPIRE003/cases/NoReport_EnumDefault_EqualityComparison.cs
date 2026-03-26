//@ should_pass
// Ensure that SPIRE003 is NOT triggered when comparing a [EnforceInitialization] enum value with default in an equality expression.
public class NoReport_EnumDefault_EqualityComparison
{
    void M(EnforceInitializationEnumNoZero someEnum)
    {
        bool isDefault = someEnum == default;
    }
}
