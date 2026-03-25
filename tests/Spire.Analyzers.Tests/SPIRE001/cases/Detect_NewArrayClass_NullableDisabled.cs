//@ should_fail
// Ensure that SPIRE001 IS triggered in nullable-disabled context for [EnforceInitialization] class.
#nullable disable
public class Detect_NewArrayClass_NullableDisabled
{
    void Bad()
    {
        var arr = new EnforceInitializationClass[5]; //~ ERROR
    }
}
