//@ should_fail
// Ensure that SPIRE001 IS triggered when creating non-empty array of [EnforceInitialization] class.
#nullable enable
public class Detect_NewArrayClass
{
    void Bad()
    {
        var arr = new EnforceInitializationClass[5]; //~ ERROR
    }
}
