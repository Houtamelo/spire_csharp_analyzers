//@ should_pass
// Ensure that SPIRE007 is NOT triggered when Unsafe.SkipInit is called with Nullable<MustInitStruct>.
public class NoReport_SkipInit_NullableMustInit
{
    public void Method()
    {
        Unsafe.SkipInit(out MustInitStruct? s);
    }
}
