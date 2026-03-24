//@ should_fail
// Ensure that SPIRE004 IS triggered when a method returns new MustInitEnumNoZero() from a return statement.
public class Detect_EnumNew_NoZeroMember_ReturnStatement
{
    MustInitEnumNoZero GetValue()
    {
        return new MustInitEnumNoZero(); //~ ERROR
    }
}
