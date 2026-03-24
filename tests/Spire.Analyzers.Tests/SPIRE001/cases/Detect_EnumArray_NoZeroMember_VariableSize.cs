//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a variable-size array of a [MustBeInit] enum with no zero-valued member.
public class Detect_EnumArray_NoZeroMember_VariableSize
{
    void M(int n)
    {
        var arr = new MustInitEnumNoZero[n]; //~ ERROR
    }
}
