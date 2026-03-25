//@ should_fail
// Ensure that SPIRE004 IS triggered when a method returns new EnforceInitializationEnumNoZero() from a return statement.
public class Detect_EnumNew_NoZeroMember_ReturnStatement
{
    EnforceInitializationEnumNoZero GetValue()
    {
        return new EnforceInitializationEnumNoZero(); //~ ERROR
    }
}
