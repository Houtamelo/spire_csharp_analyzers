//@ should_fail
// Ensure that SPIRE003 IS triggered when nullable is disabled and default is used on [EnforceInitialization] class.
#nullable disable
public class Detect_DefaultClass_NullableDisabled
{
    void Bad()
    {
        var x = default(EnforceInitializationClass); //~ ERROR
    }
}
