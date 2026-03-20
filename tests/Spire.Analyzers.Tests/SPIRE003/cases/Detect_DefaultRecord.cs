//@ should_fail
// Ensure that SPIRE003 IS triggered when using default(T) on a [MustBeInit] record class.
#nullable enable
public class Detect_DefaultRecord
{
    void Bad()
    {
        var x = default(MustInitRecord); //~ ERROR
    }
}
