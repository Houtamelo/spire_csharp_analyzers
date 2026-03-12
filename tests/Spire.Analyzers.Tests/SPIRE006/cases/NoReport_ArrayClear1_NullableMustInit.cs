//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear is called with a Nullable<MustInitStruct>[] — element type is Nullable<T>, not [MustBeInit] itself.
public class NoReport_ArrayClear1_NullableMustInit
{
    public void Method()
    {
        var arr = new MustInitStruct?[5];
        Array.Clear(arr);
    }
}
