//@ should_fail
// Ensure that SPIRE003 IS triggered when using the default literal for a [EnforceInitialization] enum with no zero-valued member.
public class Detect_EnumDefault_NoZeroMember
{
    void M()
    {
        EnforceInitializationEnumNoZero x = default; //~ ERROR
    }
}
