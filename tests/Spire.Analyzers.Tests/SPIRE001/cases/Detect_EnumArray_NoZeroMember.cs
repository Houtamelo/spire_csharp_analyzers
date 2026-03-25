//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a fixed-size array of a [EnforceInitialization] enum with no zero-valued member.
public class Detect_EnumArray_NoZeroMember
{
    void M()
    {
        var arr = new EnforceInitializationEnumNoZero[5]; //~ ERROR
    }
}
