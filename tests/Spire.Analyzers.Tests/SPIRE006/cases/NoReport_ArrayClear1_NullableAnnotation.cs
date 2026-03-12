//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear(arr) is called with a PlainStruct?[] — Nullable<PlainStruct> is not [MustBeInit].
public class NoReport_ArrayClear1_NullableAnnotation
{
    public void Method()
    {
        var arr = new PlainStruct?[5];
        Array.Clear(arr);
    }
}
