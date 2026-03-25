//@ should_fail
// Ensure that SPIRE003 IS triggered when using default on a [EnforceInitialization] class.
#nullable enable
public class Detect_DefaultClass
{
    void Bad()
    {
        var x = default(EnforceInitializationClass); //~ ERROR
    }
}
