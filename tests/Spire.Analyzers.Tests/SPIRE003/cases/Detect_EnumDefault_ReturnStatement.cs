//@ should_fail
// Ensure that SPIRE003 IS triggered when a method returns default with return type MustInitEnumNoZero.
public class Detect_EnumDefault_ReturnStatement
{
    MustInitEnumNoZero GetValue()
    {
        return default; //~ ERROR
    }
}
