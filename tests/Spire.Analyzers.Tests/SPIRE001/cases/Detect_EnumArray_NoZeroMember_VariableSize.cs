//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a variable-size array of a [EnforceInitialization] enum with no zero-valued member.
public class Detect_EnumArray_NoZeroMember_VariableSize
{
    void M(int n)
    {
        var arr = new EnforceInitializationEnumNoZero[n]; //~ ERROR
    }
}
