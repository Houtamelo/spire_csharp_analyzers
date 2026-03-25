//@ should_fail
// Ensure that SPIRE001 IS triggered when a multi-dimensional array of MustInitEnumNoZero is created with unnamed default(0) elements.
public class Detect_EnumMultiDimArray_NoZeroMember
{
    void M()
    {
        var grid = new MustInitEnumNoZero[3, 3]; //~ ERROR
    }
}
