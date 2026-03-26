//@ should_fail
// Ensure that SPIRE001 IS triggered when creating non-empty array of [EnforceInitialization] record class.
#nullable enable
public class Detect_NewArrayRecord
{
    void Bad()
    {
        var arr = new EnforceInitializationRecord[5]; //~ ERROR
    }
}
