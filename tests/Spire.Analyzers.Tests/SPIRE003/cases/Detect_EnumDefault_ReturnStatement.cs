//@ should_fail
// Ensure that SPIRE003 IS triggered when a method returns default with return type EnforceInitializationEnumNoZero.
public class Detect_EnumDefault_ReturnStatement
{
    EnforceInitializationEnumNoZero GetValue()
    {
        return default; //~ ERROR
    }
}
