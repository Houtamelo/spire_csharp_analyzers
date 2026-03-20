//@ should_fail
// Ensure that SPIRE001 IS triggered in nullable-disabled context for [MustBeInit] class.
#nullable disable
public class Detect_NewArrayClass_NullableDisabled
{
    void Bad()
    {
        var arr = new MustInitClass[5]; //~ ERROR
    }
}
