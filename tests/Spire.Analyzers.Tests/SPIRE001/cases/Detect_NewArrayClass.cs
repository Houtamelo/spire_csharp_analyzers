//@ should_fail
// Ensure that SPIRE001 IS triggered when creating non-empty array of [MustBeInit] class.
#nullable enable
public class Detect_NewArrayClass
{
    void Bad()
    {
        var arr = new MustInitClass[5]; //~ ERROR
    }
}
