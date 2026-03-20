//@ should_fail
// Ensure that SPIRE001 IS triggered when creating non-empty array of [MustBeInit] record class.
#nullable enable
public class Detect_NewArrayRecord
{
    void Bad()
    {
        var arr = new MustInitRecord[5]; //~ ERROR
    }
}
