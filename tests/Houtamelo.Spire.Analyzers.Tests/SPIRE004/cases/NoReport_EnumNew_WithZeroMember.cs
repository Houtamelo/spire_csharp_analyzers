//@ should_pass
// Ensure that SPIRE004 is NOT triggered when using new T() on a [EnforceInitialization] enum that has a zero-valued member.
public class NoReport_EnumNew_WithZeroMember
{
    void M()
    {
        var x = new EnforceInitializationEnumWithZero();
    }
}
