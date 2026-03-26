//@ should_pass
// Ensure that SPIRE003 is NOT triggered when using default for a [EnforceInitialization] enum that has a zero-valued member.
public class NoReport_EnumDefault_WithZeroMember
{
    void M()
    {
        EnforceInitializationEnumWithZero x = default;
    }
}
